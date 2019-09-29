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

        private const string CONSTRUCTOR_DEFINITION_TEMPLATE = @"
            public {1}(Request<{0}> request, System.Net.Http.HttpClient httpClient = null) {{
                Request = request;
                HttpClient = httpClient ?? new System.Net.Http.HttpClient();
            }}
        ";

        private const string HANDLER_DEFINITION_TEMPLATE = @"
            [Amazon.Lambda.Core.LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]
            public static async System.Threading.Tasks.Task Handle(Cythral.CloudFormation.CustomResource.Request<{0}> request, Amazon.Lambda.Core.ILambdaContext context = null) {{
                var client = HttpClientProvider.Provide();
                var resource = new {1}(request, client);
                
                try {{
                    object data;

                    switch(request.RequestType) {{
                        case RequestType.Create:
                            resource.Validate();
                            data = await resource.Create();
                            break;
                        case RequestType.Update:
                            resource.Validate();
                            data = await resource.Update();
                            break;
                        case RequestType.Delete:
                            data = await resource.Delete();
                            break;
                        default:
                            data = new object();
                            break;
                    }}

                    var status = Cythral.CloudFormation.CustomResource.ResponseStatus.SUCCESS;
                    await resource.Respond(status, data);
                }} catch(Exception e) {{
                    var status = Cythral.CloudFormation.CustomResource.ResponseStatus.FAILED;
                    var message = e.Message;
                    await resource.Respond(status, null, message);
                }}
            }}
        ";

        private const string RESPOND_DEFINITION_TEMPLATE = @"
            public async System.Threading.Tasks.Task<bool> Respond(Cythral.CloudFormation.CustomResource.ResponseStatus status, object data = null, string reason = null, string id = null) {{
                var response = new Cythral.CloudFormation.CustomResource.Response();
                response.Status = status;
                response.StackId = Request.StackId;
                response.LogicalResourceId = Request.LogicalResourceId;
                response.PhysicalResourceId = id ?? Request.LogicalResourceId;
                response.Reason = reason;
                response.RequestId = Request.RequestId;
                response.Data = data ?? new object();
                
                var enumConverter = new Newtonsoft.Json.Converters.StringEnumConverter();
                var converters = new Newtonsoft.Json.JsonConverter[] {{ enumConverter }};
                var serializedResponse = Newtonsoft.Json.JsonConvert.SerializeObject(response, converters);
                var payload = new System.Net.Http.StringContent(serializedResponse);
                payload.Headers.Remove(""Content-Type"");

                Console.WriteLine(serializedResponse);

                try {{
                    await HttpClient.PutAsync(Request.ResponseURL, payload);
                    return true;
                }} catch(Exception e) {{
                    Console.WriteLine(e.ToString());
                    return false;
                }}
            }} 
        ";

        private string HandlerDefinition {
            get {
                return String.Format(HANDLER_DEFINITION_TEMPLATE, ResourcePropertiesTypeName, ClassName);
            }
        }

        private string ConstructorDefinition {
            get {
                return String.Format(CONSTRUCTOR_DEFINITION_TEMPLATE, ResourcePropertiesTypeName, ClassName);
            }
        }

        private string RespondDefinition {
            get {
                return String.Format(RESPOND_DEFINITION_TEMPLATE);
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

        public static Dictionary<string,Resource> Resources = new Dictionary<string,Resource>();

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
                .WithTypeConverter(new GetAttTagConverter())
                .Build();

                var yaml = serializer.Serialize(Resources);
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
                    Handler = $"{context.Compilation.Assembly.Name}::{context.ProcessingNode.GetFullName()}::Handler",
                    Role = new GetAttTag() { Name = $"{ClassName}Role", Attribute = "Arn" },
                    Code = $"{context.Compilation.AssemblyName}.zip",
                    Runtime = "netcoreapp2.1" // todo: add autodetection here / some way to change this
                }
            });
        }

        private void AddRoleResource(TransformationContext context) {
            var role = new Role()
            .AddTrustedServiceEntity("lambda.amazonaws.com");
            
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
                    var statement = new ValidationGenerator(symbol, attribute).GenerateStatement();
                    yield return statement;
                }
            }
        }
        
    }

}