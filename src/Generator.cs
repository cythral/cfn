using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeGeneration.Roslyn;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Validation;
using Newtonsoft.Json;

namespace Cythral.CloudFormation.CustomResource {
    public class Generator : ICodeGenerator {

        private string ResourcePropertiesType;
        
        private string ClassName;

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
                            data = await resource.Create();
                            break;
                        case RequestType.Update:
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
                return String.Format(HANDLER_DEFINITION_TEMPLATE, ResourcePropertiesType, ClassName);
            }
        }

        private string ConstructorDefinition {
            get {
                return String.Format(CONSTRUCTOR_DEFINITION_TEMPLATE, ResourcePropertiesType, ClassName);
            }
        }

        private string RespondDefinition {
            get {
                return String.Format(RESPOND_DEFINITION_TEMPLATE);
            }
        }

        private string RequestPropertyDefinition {
            get {
                return String.Format("public readonly Cythral.CloudFormation.CustomResource.Request<{0}> Request;", ResourcePropertiesType);
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

        public Generator(AttributeData attributeData) {
            Requires.NotNull(attributeData, nameof(attributeData));
            ResourcePropertiesType = attributeData.ConstructorArguments[0].Value.ToString();
        }

        public Task<SyntaxList<MemberDeclarationSyntax>> GenerateAsync(TransformationContext context, IProgress<Diagnostic> progress, CancellationToken cancellationToken) {
            var result = GeneratePartialClass();
            return Task.FromResult(SyntaxFactory.List(result));

            IEnumerable<MemberDeclarationSyntax> GeneratePartialClass() {
                var originalClass = (ClassDeclarationSyntax) context.ProcessingNode;
                ClassName = originalClass.Identifier.ValueText;

                yield return SyntaxFactory
                .ClassDeclaration(originalClass.Identifier.ValueText)
                .AddModifiers(SyntaxFactory.Token(SyntaxKind.PartialKeyword))
                .WithIdentifier(SyntaxFactory.Identifier(ClassName))
                .AddMembers(
                    SyntaxFactory.ParseMemberDeclaration(HttpClientPropertyDefinition),
                    SyntaxFactory.ParseMemberDeclaration(RequestPropertyDefinition),
                    SyntaxFactory.ParseMemberDeclaration(HttpClientProviderDefinition),
                    SyntaxFactory.ParseMemberDeclaration(ConstructorDefinition),
                    SyntaxFactory.ParseMemberDeclaration(RespondDefinition),
                    SyntaxFactory.ParseMemberDeclaration(HandlerDefinition)
                );
            }
        }
    }
}