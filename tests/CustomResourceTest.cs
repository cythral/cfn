using System;
using System.Threading;
using System.Threading.Tasks;
using Cythral.CloudFormation.CustomResource;
using NUnit.Framework;
using RichardSzalay.MockHttp;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace Tests {
    [CustomResource(typeof(object))]
    public partial class ExampleCustomResource {
        static public bool Passing { get; set; } = true;
        public Task<object> Create() {
            ThrowIfNotPassing();

            return Task.FromResult((object) new {
                Status = "Created"
            });
        }

        public Task<object> Update() {
            ThrowIfNotPassing();

            return Task.FromResult((object) new {
                Status = "Updated"
            });
        }

        public Task<object> Delete() {
            ThrowIfNotPassing();

            return Task.FromResult((object) new {
                Status = "Deleted"
            });
        }

        public void ThrowIfNotPassing() {
            if(!Passing) {
                throw new Exception("Expected this error message");
            }
        }
    }

    public class CustomResourceTest {
        [Test]
        public async Task TestHandleCallsCreate() {
            var expectedPayload = new Response() {
                Data = new {
                    Status = "Created"
                }
            };

            var mockHttp = new MockHttpMessageHandler();
            mockHttp
                .Expect("http://example.com")
                .WithPartialContent(Serialize(expectedPayload));
            
            var request = new Request<object>() {
                RequestType = RequestType.Create,
                ResponseURL = "http://example.com"
            };

            await ExampleCustomResource.Handle(request, mockHttp.ToHttpClient());
            mockHttp.VerifyNoOutstandingExpectation();
        }

        [Test]
        public async Task TestHandleCallsUpdate() {
            var expectedPayload = new Response() {
                Data = new {
                    Status = "Updated"
                }
            };

            var mockHttp = new MockHttpMessageHandler();
            mockHttp
                .Expect("http://example.com")
                .WithContent(Serialize(expectedPayload));

            var request = new Request<object>() {
                RequestType = RequestType.Update,
                ResponseURL = "http://example.com"
            };

            await ExampleCustomResource.Handle(request, mockHttp.ToHttpClient());
            mockHttp.VerifyNoOutstandingExpectation();
        }

        [Test]
        public async Task TestHandleCallsDelete() {
            var expectedPayload = new Response() {
                Data = new {
                    Status = "Deleted"
                }
            };

            var mockHttp = new MockHttpMessageHandler();
            mockHttp
                .Expect("http://example.com")
                .WithContent(Serialize(expectedPayload));

            var request = new Request<object>() {
                RequestType = RequestType.Delete,
                ResponseURL = "http://example.com"
            };

            await ExampleCustomResource.Handle(request, mockHttp.ToHttpClient());
            mockHttp.VerifyNoOutstandingExpectation();
        }

        [Test]
        public async Task TestHandleRespondsOnError() {
            var expectedPayload = new Response() {
                Status = ResponseStatus.FAILED,
                Reason = "Expected this error message",
                Data = new object()
            };

            var mockHttp = new MockHttpMessageHandler();
            mockHttp
                .Expect("http://example.com")
                .WithContent(Serialize(expectedPayload));
        
            var request = new Request<object>() {
                RequestType = RequestType.Create,
                ResponseURL = "http://example.com"
            };

            ExampleCustomResource.Passing = false;
            await ExampleCustomResource.Handle(request, mockHttp.ToHttpClient());
            mockHttp.VerifyNoOutstandingExpectation();
        }

        private string Serialize(object toSerialize) {
            var serializers = new JsonConverter[] { new StringEnumConverter() };
            return JsonConvert.SerializeObject(toSerialize, serializers);
        }
    }
}