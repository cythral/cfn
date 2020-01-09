using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;
using static Microsoft.CodeAnalysis.CSharp.SyntaxKind;

using CodeGeneration.Roslyn.Engine;

using Validation;

using YamlDotNet.Serialization;

namespace Cythral.CloudFormation.CustomResource
{

    using Yaml;


    public class Generator : ICodeGenerator
    {

        private INamedTypeSymbol ResourcePropertiesType;

        private string ResourcePropertiesTypeName;

        private object[] Grantees { get; set; }

        private GranteeType GranteeType { get; set; } = GranteeType.Literal;

        private ClassDeclarationSyntax OriginalClass;

        private string ClassName => OriginalClass.Identifier.ValueText;

        private AttributeData Data;

        private string ConstructorDefinition
        {
            get
            {
                return String.Format(@"
                    public {0}(System.Net.Http.HttpClient httpClient = null, Amazon.Lambda.Core.ILambdaContext context = null) {{
                        HttpClient = httpClient ?? new System.Net.Http.HttpClient();
                        Context = context;
                    }}
                ", ClassName);
            }
        }

        private string RespondDefinition
        {
            get
            {
                return String.Format(@"
                    public virtual async System.Threading.Tasks.Task<bool> Respond(Response response) {{
                        return await Respond(Request, response, HttpClient);
                    }}
                ", ResourcePropertiesTypeName, ClassName);
            }
        }

        private string StaticRespondDefinition
        {
            get
            {
                return String.Format(@"
                    public static async System.Threading.Tasks.Task<bool> Respond<T>(Request<T> request, Response response, System.Net.Http.HttpClient client) {{
                        response.StackId = request.StackId;
                        response.LogicalResourceId = request.LogicalResourceId;
                        response.RequestId = request.RequestId;
                        
                        if(response.PhysicalResourceId == null) {{
                            response.PhysicalResourceId = request.PhysicalResourceId;
                        }}
                        
                        var serializedResponse = System.Text.Json.JsonSerializer.Serialize(response, SerializerOptions);
                        var payload = new System.Net.Http.StringContent(serializedResponse);
                        payload.Headers.Remove(""Content-Type"");

                        Console.WriteLine(serializedResponse);

                        try {{
                            await client.PutAsync(request.ResponseURL, payload);
                            return true;
                        }} catch(Exception e) {{
                            Console.WriteLine(e.ToString());
                            return false;
                        }}
                    }}
                ", ResourcePropertiesTypeName, ClassName);
            }
        }

        private string RequestPropertyDefinition
        {
            get
            {
                return String.Format("public Cythral.CloudFormation.CustomResource.Request<{0}> Request {{ get; set; }}", ResourcePropertiesTypeName);
            }
        }

        private string ContextPropertyDefinition
        {
            get
            {
                return String.Format("public readonly Amazon.Lambda.Core.ILambdaContext Context;");
            }
        }

        private string HttpClientPropertyDefinition
        {
            get
            {
                return String.Format("public readonly System.Net.Http.HttpClient HttpClient;");
            }
        }

        private string HttpClientProviderDefinition
        {
            get
            {
                return String.Format("public static Cythral.CloudFormation.CustomResource.IHttpClientProvider HttpClientProvider = new DefaultHttpClientProvider();");
            }
        }

        private string SerializerOptionsDefinition
        {
            get
            {
                return String.Format(@"
                    private static System.Text.Json.JsonSerializerOptions SerializerOptions {{
                        get {{
                            var options = new System.Text.Json.JsonSerializerOptions();
                            options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                            options.Converters.Add(new Cythral.CloudFormation.CustomResource.AwsConstantClassConverterFactory());
                            return options;
                        }}
                    }}
                ");
            }
        }

        public static Dictionary<string, Resource> Resources = new Dictionary<string, Resource>();

        public static Dictionary<string, Output> Outputs = new Dictionary<string, Output>();

        public Generator(AttributeData attributeData)
        {
            Requires.NotNull(attributeData, nameof(attributeData));
            Data = attributeData;

            foreach (var arg in Data.NamedArguments)
            {

                switch (arg.Key)
                {
                    case "Grantees":
                        Grantees = (from typeConstant in arg.Value.Values select typeConstant.Value).ToArray();
                        break;

                    case "GranteeType":
                        GranteeType = (GranteeType)arg.Value.Value;
                        break;

                    case "ResourcePropertiesType":
                        ResourcePropertiesTypeName = arg.Value.Value.ToString();
                        ResourcePropertiesType = (INamedTypeSymbol)arg.Value.Value;
                        break;
                }
            }
        }

        public Task<SyntaxList<MemberDeclarationSyntax>> GenerateAsync(TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken)
        {
            var result = GeneratePartialClass();
            OriginalClass = (ClassDeclarationSyntax)context.ProcessingNode;
            AddResources(context);

            return Task.FromResult(SyntaxFactory.List(result));

            IEnumerable<MemberDeclarationSyntax> GeneratePartialClass()
            {
                var partialClass = SyntaxFactory
                .ClassDeclaration(ClassName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword))
                .WithIdentifier(SyntaxFactory.Identifier(ClassName))
                .AddMembers(
                    SyntaxFactory.ParseMemberDeclaration(HttpClientPropertyDefinition),
                    SyntaxFactory.ParseMemberDeclaration(RequestPropertyDefinition),
                    SyntaxFactory.ParseMemberDeclaration(ContextPropertyDefinition),
                    SyntaxFactory.ParseMemberDeclaration(HttpClientProviderDefinition),
                    SyntaxFactory.ParseMemberDeclaration(ConstructorDefinition),
                    SyntaxFactory.ParseMemberDeclaration(RespondDefinition),
                    SyntaxFactory.ParseMemberDeclaration(StaticRespondDefinition),
                    SyntaxFactory.ParseMemberDeclaration(SerializerOptionsDefinition),
                    GeneratePhysicalResourceIdProperty(),
                    GenerateHandleMethod(),
                    GenerateValidateMethod()
                );

                yield return partialClass;
            }
        }

        public static void OnComplete(CompilationGenerator context)
        {
            var outputDirectory = context.BuildProperties["OutDir"];
            var description = context.BuildProperties["StackDescription"];
            var filePath = outputDirectory + "/" + context.AssemblyName + ".template.yml";

            try
            {
                var yamlDotNet = Assembly.Load("YamlDotNet");
                var serializer = ((SerializerBuilder)yamlDotNet.CreateInstance("YamlDotNet.Serialization.SerializerBuilder"))
                .WithTagMapping("!GetAtt", typeof(GetAttTag))
                .WithTagMapping("!Sub", typeof(SubTag))
                .WithTypeConverter(new GetAttTagConverter())
                .WithTypeConverter(new SubTagConverter())
                .WithTypeConverter(new ImportValueTagConverter())
                .Build();

                var yaml = serializer.Serialize(new
                {
                    Description = description,
                    Resources = Resources,
                    Outputs = Outputs
                });

                System.IO.File.WriteAllText(filePath, yaml);
            }
            catch (Exception e)
            {
                System.IO.File.WriteAllText(filePath, e.Message);
            }
        }

        private void AddResources(TransformationContext context)
        {
            AddRoleResource(context);

            var version = context.BuildProperties["TargetFrameworkVersion"].Replace("v", "");

            Resources.Add(ClassName + "Lambda", new Resource
            {
                Type = "AWS::Lambda::Function",
                Properties = new
                {
                    FunctionName = ClassName,
                    Handler = $"{context.Compilation.Assembly.Name}::{context.ProcessingNode.GetFullName()}::Handle",
                    Role = new GetAttTag() { Name = $"{ClassName}Role", Attribute = "Arn" },
                    Code = $"publish",
                    Runtime = $"dotnetcore{version}",
                    Timeout = 300,
                }
            });

            if (Grantees != null && Grantees?.Count() > 0)
            {
                for (int i = 0; i < Grantees.Count(); i++)
                {
                    var grantee = Grantees[i];

                    Resources.Add($"{ClassName}Permission{i}", new Resource
                    {
                        Type = "AWS::Lambda::Permission",
                        Properties = new
                        {
                            FunctionName = new GetAttTag { Name = $"{ClassName}Lambda", Attribute = "Arn" },
                            Principal = GranteeType == GranteeType.Import ? (object)new ImportValueTag((string)grantee) : grantee,
                            Action = "lambda:InvokeFunction"
                        }
                    });
                }
            }

            Outputs.Add(ClassName + "LambdaArn", new Output(
                value: new GetAttTag { Name = $"{ClassName}Lambda", Attribute = "Arn" },
                name: new SubTag { Expression = $"${{AWS::StackName}}:{ClassName}LambdaArn" }
            ));
        }

        private void AddRoleResource(TransformationContext context)
        {
            var role = new Role()
            .AddTrustedServiceEntity("lambda.amazonaws.com")
            .AddManagedPolicy("arn:aws:iam::aws:policy/AWSLambdaExecute");

            var collector = new PermissionsCollector(context);

            try
            {
                collector.Visit(context.ProcessingNode);
            }
            catch (Exception) { }

            if (collector.Permissions.Count() > 0)
            {
                role.AddPolicy(
                    new Policy($"{ClassName}PrimaryPolicy")
                    .AddStatement(Action: collector.Permissions)
                );
            }

            Resources.Add($"{ClassName}Role", role);
        }

        private MemberDeclarationSyntax GenerateHandleMethod()
        {
            var body = Block(GenerateHandleMethodBody());
            var returnType = ParseTypeName("System.Threading.Tasks.Task<System.IO.Stream>");
            return MethodDeclaration(returnType, "Handle")
                .AddModifiers(Token(PublicKeyword), Token(StaticKeyword), Token(AsyncKeyword))
                .AddParameterListParameters(
                    //        attribute lists              modifiers              type                                                  identifier              default value
                    Parameter(List<AttributeListSyntax>(), new SyntaxTokenList(), ParseTypeName("System.IO.Stream"), ParseToken("stream"), null),
                    Parameter(List<AttributeListSyntax>(), new SyntaxTokenList(), ParseTypeName("Amazon.Lambda.Core.ILambdaContext"), ParseToken("context"), EqualsValueClause(ParseExpression("null")))

                )
                .WithBody(body);
        }

        private IEnumerable<StatementSyntax> GenerateHandleMethodBody()
        {
            yield return ParseStatement("var response = new Response();");
            yield return ParseStatement("var client = HttpClientProvider.Provide();");
            yield return ParseStatement($"var resource = new {ClassName}(client, context);");

            var catchDeclaration = CatchDeclaration(ParseTypeName("Exception"), ParseToken("e"));

            yield return TryStatement(
                ParseToken("try"),
                Block(GenerateHandleMethodTryBlock()),
                List(new List<CatchClauseSyntax> {
                    CatchClause(ParseToken("catch"), catchDeclaration, null, Block(GenerateHandleMethodCatchBlock()))
                }),
                null
            );

        }

        private IEnumerable<StatementSyntax> GenerateHandleMethodTryBlock()
        {
            yield return ParseStatement("stream.Seek(0, System.IO.SeekOrigin.Begin);");

            yield return ParseStatement($"var request = await System.Text.Json.JsonSerializer.DeserializeAsync<Request<{ResourcePropertiesTypeName}>>(stream, SerializerOptions);");
            yield return ParseStatement("Console.WriteLine($\"Received request: {System.Text.Json.JsonSerializer.Serialize(request, SerializerOptions)}\");");
            yield return ParseStatement("resource.Request = request;");
            yield return ParseStatement("resource.PhysicalResourceId = request?.PhysicalResourceId;");

            var cases = new List<SwitchSectionSyntax> {
                GenerateHandleMethodCreateCase(),
                GenerateHandleMethodUpdateCase(),
                GenerateHandleMethodDeleteCase()
            };

            var methods = OriginalClass.Members.Where(member => member is MethodDeclarationSyntax).Cast<MethodDeclarationSyntax>();
            if (methods.Any(member => member.Identifier.Text == "Wait"))
            {
                cases.Add(GenerateHandleMethodWaitCase());
            }

            yield return SwitchStatement(
                ParseExpression("(request.RequestType)"),
                List(cases)
            );

            yield return IfStatement(
                ParseExpression("response != null"),
                Block(new StatementSyntax[] {
                    ParseStatement("response.PhysicalResourceId = response.PhysicalResourceId ?? resource.PhysicalResourceId;"),
                    ParseStatement("await resource.Respond(response);")
                })
            );

            yield return ParseStatement("var outStream = new System.IO.MemoryStream();");
            yield return ParseStatement("await System.Text.Json.JsonSerializer.SerializeAsync(outStream, resource);");
            yield return ParseStatement("return outStream;");
        }

        private SwitchSectionSyntax GenerateHandleMethodCreateCase()
        {
            return SwitchSection(
                List(new List<SwitchLabelSyntax> {
                    CaseSwitchLabel(ParseExpression("RequestType.Create")),
                }),
                List(new List<StatementSyntax> {
                    ParseStatement("resource.Validate();"),
                    ParseStatement("response = await resource.Create();"),
                    ParseStatement("break;")
                })
            );
        }

        private SwitchSectionSyntax GenerateHandleMethodUpdateCase()
        {
            return SwitchSection(
                List(new List<SwitchLabelSyntax> {
                    CaseSwitchLabel(ParseExpression("RequestType.Update")),
                }),
                List(new List<StatementSyntax> {
                    ParseStatement("resource.Validate();"),
                    IfStatement(
                        ParseExpression("resource.Request.RequiresReplacement"),
                        Block(List(new List<StatementSyntax> {
                            ParseStatement("Console.WriteLine($\"Resource {resource.Request.PhysicalResourceId} requires replacement. Creating a new one instead of updating...\");"),
                            ParseStatement("response = await resource.Create();")
                        })),
                        ElseClause(
                            Block(List(new List<StatementSyntax> {
                                ParseStatement("Console.WriteLine($\"Updating Resource: {resource.Request.PhysicalResourceId}\");"),
                                ParseStatement("response = await resource.Update();")
                            }))
                        )
                    ),
                    ParseStatement("break;")
                })
            );
        }

        private SwitchSectionSyntax GenerateHandleMethodDeleteCase()
        {
            return SwitchSection(
                List(new List<SwitchLabelSyntax> {
                    CaseSwitchLabel(ParseExpression("RequestType.Delete")),
                }),
                List(new List<StatementSyntax> {
                    ParseStatement("response = await resource.Delete();"),
                    ParseStatement("break;")
                })
            );
        }

        private SwitchSectionSyntax GenerateHandleMethodWaitCase()
        {
            return SwitchSection(
                List(new List<SwitchLabelSyntax> {
                    CaseSwitchLabel(ParseExpression("RequestType.Wait")),
                }),
                List(new List<StatementSyntax> {
                    ParseStatement("response = await resource.Wait();"),
                    ParseStatement("break;")
                })
            );
        }

        private IEnumerable<StatementSyntax> GenerateHandleMethodCatchBlock()
        {
            yield return ParseStatement("Console.WriteLine(e.Message + \"\n\" + e.StackTrace);");
            yield return ParseStatement("stream.Seek(0, System.IO.SeekOrigin.Begin);");
            yield return ParseStatement("var request = await System.Text.Json.JsonSerializer.DeserializeAsync<Cythral.CloudFormation.CustomResource.Request<object>>(stream, SerializerOptions);");
            yield return ParseStatement("response.Status = Cythral.CloudFormation.CustomResource.ResponseStatus.FAILED;");
            yield return ParseStatement("response.Reason = e.Message;");
            yield return ParseStatement("response.PhysicalResourceId = resource.PhysicalResourceId;");
            yield return ParseStatement("await Respond(request, response, client);");
            yield return ParseStatement("return null;");
        }

        private MemberDeclarationSyntax GenerateValidateMethod()
        {
            var bodyStatements = GenerateValidationCalls();
            var body = SyntaxFactory.Block(bodyStatements);
            return SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName("void"), "Validate")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .WithBody(body);
        }

        private IEnumerable<StatementSyntax> GenerateValidationCalls()
        {
            foreach (var symbol in ResourcePropertiesType.GetMembers())
            {
                if (symbol.Kind != SymbolKind.Property && symbol.Kind != SymbolKind.Field)
                {
                    continue;
                }

                foreach (var attribute in symbol.GetAttributes())
                {
                    if (attribute.AttributeClass.BaseType.Name != "ValidationAttribute")
                    {
                        continue;
                    }

                    var statement = new ValidationGenerator(symbol, attribute).GenerateStatement();
                    yield return statement;
                }
            }
        }

        private MemberDeclarationSyntax GeneratePhysicalResourceIdProperty()
        {
            return PropertyDeclaration(
                List<AttributeListSyntax>(),
                TokenList(
                    Token(PublicKeyword)
                ),
                ParseTypeName("string"),
                null,
                ParseToken("PhysicalResourceId"),
                AccessorList(
                    Token(OpenBraceToken),
                    List(new List<AccessorDeclarationSyntax>
                    {
                        AccessorDeclaration(SyntaxKind.GetAccessorDeclaration, List<AttributeListSyntax>(), TokenList(), Token(GetKeyword), null, null, Token(SemicolonToken)),
                        AccessorDeclaration(SyntaxKind.SetAccessorDeclaration, List<AttributeListSyntax>(), TokenList(), Token(SetKeyword), null, null, Token(SemicolonToken)),
                    }),
                    Token(CloseBraceToken)
                )
            );
        }
    }

}