using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.ComponentModel.DataAnnotations;
using Cythral.CloudFormation.CustomResource;
using CodeGeneration.Roslyn.Engine;
using NUnit.Framework;
using RichardSzalay.MockHttp;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Tests {

    public class ModelWithRequiredProps {
        [Required(ErrorMessage="Message is required")]
        public string Message;
    }


    [CustomResourceAttribute(typeof(ModelWithRequiredProps))]
    public partial class CustomResourceWithRequiredProps {
        public static bool Passing { get; set; } = true;

        public Task<Response> Create() {
            ThrowIfNotPassing();

            return Task.FromResult(new Response {
                Data = new {
                    Status = "Created"
                }
            });
        }

        public Task<Response> Update() {
            ThrowIfNotPassing();

            return Task.FromResult(new Response {
                Data = new {
                    Status = "Updated"
                }
            });
        }

        public Task<Response> Delete() {
            ThrowIfNotPassing();

            return Task.FromResult(new Response {
                Data = new {
                    Status = "Deleted"
                }
            });
        }

        public void ThrowIfNotPassing() {
            if(!Passing) {
                throw new Exception("Expected this error message");
            }
        }
    }

    public class CustomResourceWithRequiredPropsTest {
        [Test]
        public async Task TestHandleShouldFailIfRequiredPropIsMissing() {
            var expectedPayload = new Response() {
                Status = ResponseStatus.FAILED,
                Reason = "Message is required",
            };
            
            var mockHttp = new MockHttpMessageHandler();
            var httpProvider = new FakeHttpClientProvider(mockHttp);
            CustomResourceWithRequiredProps.HttpClientProvider = httpProvider;

            mockHttp
                .Expect("http://example.com")
                .WithContent(Serialize(expectedPayload));
            
            var request = new Request<ModelWithRequiredProps>() {
                RequestType = RequestType.Create,
                ResponseURL = "http://example.com",
                ResourceProperties = new ModelWithRequiredProps()
            };

            await CustomResourceWithRequiredProps.Handle(request);
            mockHttp.VerifyNoOutstandingExpectation();
        }

        [Test]
        public async Task TestHandleShouldSucceedIfAllPropsArePresent() {
            var expectedPayload = new Response() {
                Data = new {
                    Status = "Created"
                }
            };
            
            var mockHttp = new MockHttpMessageHandler();
            var httpProvider = new FakeHttpClientProvider(mockHttp);
            CustomResourceWithRequiredProps.HttpClientProvider = httpProvider;

            mockHttp
                .Expect("http://example.com")
                .WithContent(Serialize(expectedPayload));
            
            var request = new Request<ModelWithRequiredProps>() {
                RequestType = RequestType.Create,
                ResponseURL = "http://example.com",
                ResourceProperties = new ModelWithRequiredProps() {
                    Message = "Test message"
                }
            };

            await CustomResourceWithRequiredProps.Handle(request);
            mockHttp.VerifyNoOutstandingExpectation();
        }

        private string Serialize(object toSerialize) {
            var serializers = new JsonConverter[] { new StringEnumConverter() };
            return JsonConvert.SerializeObject(toSerialize, serializers);
        }
        
    }
}