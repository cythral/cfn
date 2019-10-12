using System;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Reflection;
using System.ComponentModel.DataAnnotations;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

using CodeGeneration.Roslyn.Engine;
using Newtonsoft.Json;
using Validation;
using YamlDotNet.Serialization;

namespace Cythral.CloudFormation.CustomResource {

    using Yaml;

    
    public class Generator : ICodeGenerator {

        private INamedTypeSymbol ResourcePropertiesType;

        private string ResourcePropertiesTypeName;

        private ClassDeclarationSyntax OriginalClass;

        private string ClassName => OriginalClass.Identifier.ValueText;

        private AttributeData Data;

        private string HandlerDefinition {
            get {
                return String.Format(@"
                    public static async System.Threading.Tasks.Task Handle(System.IO.Stream stream, Amazon.Lambda.Core.ILambdaContext context = null) {{
                        var response = new Response();
                        var client = HttpClientProvider.Provide();

                        try {{
                            stream.Seek(0, System.IO.SeekOrigin.Begin);

                            var request = await System.Text.Json.JsonSerializer.DeserializeAsync<Request<{0}>>(stream, SerializerOptions);
                            var resource = new {1}(request, client);

                            switch(request.RequestType) {{
                                case RequestType.Create:
                                    resource.Validate();
                                    response = await resource.Create();
                                    break;
                                case RequestType.Update:
                                    resource.Validate();
                                    response = await resource.Update();
                                    break;
                                case RequestType.Delete:
                                    response = await resource.Delete();
                                    break;
                                default:
                                    break;
                            }}

                            await resource.Respond(response);

                        }} catch(Exception e) {{
                            stream.Seek(0, System.IO.SeekOrigin.Begin);

                            var request = await System.Text.Json.JsonSerializer.DeserializeAsync<Cythral.CloudFormation.CustomResource.Request<object>>(stream, SerializerOptions);
                            
                            response.Status = Cythral.CloudFormation.CustomResource.ResponseStatus.FAILED;
                            response.Reason = e.Message;

                            await Respond(request, response, client);
                        }}
                    }}
                ", ResourcePropertiesTypeName, ClassName);
            }
        }

        private string ConstructorDefinition {
            get {
                return String.Format(@"
                    public {1}(Request<{0}> request, System.Net.Http.HttpClient httpClient = null) {{
                        Request = request;
                        HttpClient = httpClient ?? new System.Net.Http.HttpClient();
                    }}
                ", ResourcePropertiesTypeName, ClassName);
            }
        }

        private string RespondDefinition {
            get {
                return String.Format(@"
                    public async System.Threading.Tasks.Task<bool> Respond(Response response) {{
                        return await Respond(Request, response, HttpClient);
                    }}
                ", ResourcePropertiesTypeName, ClassName);
            }
        }

        private string StaticRespondDefinition {
            get {
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

        private string RequestPropertyDefinition {
            get {
                return String.Format("public readonly Cythral.CloudFormation.CustomResource.Request<{0}> Request;", ResourcePropertiesTypeName);
            }
        }

        private string HttpClientPropertyDefinition {
            get {
                return String.Format("public readonly System.Net.Http.HttpClient HttpClient;");
            }
        }

        private string HttpClientProviderDefinition {
            get {
                return String.Format("public static Cythral.CloudFormation.CustomResource.IHttpClientProvider HttpClientProvider = new DefaultHttpClientProvider();");
            }
        }

        private string SerializerOptionsDefinition {
            get {
                return String.Format(@"
                    private static System.Text.Json.JsonSerializerOptions SerializerOptions {{
                        get {{
                            var options = new System.Text.Json.JsonSerializerOptions();
                            options.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
                            return options;
                        }}
                    }}
                ");
            }
        }

        public static Dictionary<string,Resource> Resources = new Dictionary<string,Resource>();

        public static Dictionary<string,Output> Outputs = new Dictionary<string,Output>();

        public Generator(AttributeData attributeData) {
            Requires.NotNull(attributeData, nameof(attributeData));
            Data = attributeData;
            ResourcePropertiesTypeName = Data.ConstructorArguments[0].Value.ToString();
            ResourcePropertiesType = (INamedTypeSymbol) Data.ConstructorArguments[0].Value;
        }

        public Task<SyntaxList<MemberDeclarationSyntax>> GenerateAsync(TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken) {
            var result = GeneratePartialClass();

            OriginalClass = (ClassDeclarationSyntax) context.ProcessingNode;
            AddResources(context);

            return Task.FromResult(SyntaxFactory.List(result));

            IEnumerable<MemberDeclarationSyntax> GeneratePartialClass() {
                var partialClass = SyntaxFactory
                .ClassDeclaration(ClassName)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword))
                .WithIdentifier(SyntaxFactory.Identifier(ClassName))
                .AddMembers(
                    SyntaxFactory.ParseMemberDeclaration(HttpClientPropertyDefinition),
                    SyntaxFactory.ParseMemberDeclaration(RequestPropertyDefinition),
                    SyntaxFactory.ParseMemberDeclaration(HttpClientProviderDefinition),
                    SyntaxFactory.ParseMemberDeclaration(ConstructorDefinition),
                    SyntaxFactory.ParseMemberDeclaration(RespondDefinition),
                    SyntaxFactory.ParseMemberDeclaration(StaticRespondDefinition),
                    SyntaxFactory.ParseMemberDeclaration(SerializerOptionsDefinition),
                    SyntaxFactory.ParseMemberDeclaration(HandlerDefinition),
                    GenerateValidateMethod()
                );

                yield return partialClass;
            }
        }

        public static void OnComplete(CompilationGenerator context) {
            // todo: handle generating cloudformation templates here
            var outputDirectory = context.IntermediateOutputDirectory;
            var filePath = outputDirectory + "/" + context.AssemblyName + ".template.yml";

            try {
                var yamlDotNet = Assembly.Load("YamlDotNet");
                var serializer = ((SerializerBuilder) yamlDotNet.CreateInstance("YamlDotNet.Serialization.SerializerBuilder"))
                .WithTagMapping("!GetAtt", typeof(GetAttTag))
                .WithTagMapping("!Sub", typeof(SubTag))
                .WithTypeConverter(new GetAttTagConverter())
                .WithTypeConverter(new SubTagConverter())
                .Build();

                var yaml = serializer.Serialize(new { 
                    Resources = Resources,
                    Outputs = Outputs
                });

                System.IO.File.WriteAllText(filePath, yaml);
            } catch(Exception e) {
                System.IO.File.WriteAllText(filePath, e.Message);
            }
        }

        private void AddResources(TransformationContext context) {
            AddRoleResource(context);
            
            Resources.Add(ClassName + "Lambda", new Resource() {
                Type = "AWS::Lambda::Function",
                Properties = new {
                    FunctionName = ClassName,
                    Handler = $"{context.Compilation.Assembly.Name}::{context.ProcessingNode.GetFullName()}::Handle",
                    Role = new GetAttTag() { Name = $"{ClassName}Role", Attribute = "Arn" },
                    Code = $"{context.Compilation.AssemblyName}.zip",
                    Runtime = "dotnetcore2.1", // todo: add autodetection here / some way to change this
                    Timeout = 300,
                }
            });

            Outputs.Add(ClassName + "LambdaArn", new Output(
                value: new GetAttTag { Name = $"{ClassName}Lambda", Attribute = "Arn" },
                name: new SubTag { Expression = $"${{AWS::StackName}}:{ClassName}LambdaArn" }
            ));
        }

        private void AddRoleResource(TransformationContext context) {
            var role = new Role()
            .AddTrustedServiceEntity("lambda.amazonaws.com")
            .AddManagedPolicy("arn:aws:iam::aws:policy/AWSLambdaExecute");
            
            var collector = new PermissionsCollector(context);

            try {
                collector.Visit(context.ProcessingNode);
            } catch(Exception) {}
            
            if(collector.Permissions.Count() > 0) {
                role.AddPolicy(
                    new Policy($"{ClassName}PrimaryPolicy")
                    .AddStatement(Action: collector.Permissions)
                );
            }

            Resources.Add($"{ClassName}Role", role);
        }

        private MemberDeclarationSyntax GenerateValidateMethod() {
            var bodyStatements = GenerateValidationCalls();
            var body = SyntaxFactory.Block(bodyStatements);
            return SyntaxFactory.MethodDeclaration(SyntaxFactory.ParseTypeName("void"), "Validate")
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PublicKeyword))
                .WithBody(body);
        }

        private IEnumerable<StatementSyntax> GenerateValidationCalls() {
            foreach(var symbol in ResourcePropertiesType.GetMembers()) {
                if(symbol.Kind != SymbolKind.Property && symbol.Kind != SymbolKind.Field) {
                    continue;
                }

                foreach(var attribute in symbol.GetAttributes()) {
                    if(attribute.AttributeClass.BaseType.Name != "ValidationAttribute") {
                        continue;
                    }

                    var statement = new ValidationGenerator(symbol, attribute).GenerateStatement();
                    yield return statement;
                }
            }
        }
    }

}