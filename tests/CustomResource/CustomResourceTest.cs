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
using Amazon.S3;

namespace Tests {

    public class FakeHttpClientProvider : IHttpClientProvider {
        public readonly MockHttpMessageHandler httpMock;

        public FakeHttpClientProvider(MockHttpMessageHandler httpMock) {
            this.httpMock = httpMock;
        }

        public HttpClient Provide() {
            return httpMock.ToHttpClient();
        }
    }

    [CustomResourceAttribute(typeof(object))]
    public partial class ExampleCustomResource {
        public static bool Passing { get; set; } = true;

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
            var httpProvider = new FakeHttpClientProvider(mockHttp);
            ExampleCustomResource.HttpClientProvider = httpProvider;

            mockHttp
                .Expect("http://example.com")
                .WithPartialContent(Serialize(expectedPayload));
            
            var request = new Request<object>() {
                RequestType = RequestType.Create,
                ResponseURL = "http://example.com"
            };

            await ExampleCustomResource.Handle(request);
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
            var httpProvider = new FakeHttpClientProvider(mockHttp);
            ExampleCustomResource.HttpClientProvider = httpProvider;

            mockHttp
                .Expect("http://example.com")
                .WithContent(Serialize(expectedPayload));

            var request = new Request<object>() {
                RequestType = RequestType.Update,
                ResponseURL = "http://example.com"
            };

            await ExampleCustomResource.Handle(request);
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
            var httpProvider = new FakeHttpClientProvider(mockHttp);
            ExampleCustomResource.HttpClientProvider = httpProvider;
            mockHttp
                .Expect("http://example.com")
                .WithContent(Serialize(expectedPayload));

            var request = new Request<object>() {
                RequestType = RequestType.Delete,
                ResponseURL = "http://example.com"
            };

            await ExampleCustomResource.Handle(request);
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
            var httpProvider = new FakeHttpClientProvider(mockHttp);
            ExampleCustomResource.HttpClientProvider = httpProvider;

            mockHttp
                .Expect("http://example.com")
                .WithContent(Serialize(expectedPayload));
        
            var request = new Request<object>() {
                RequestType = RequestType.Create,
                ResponseURL = "http://example.com"
            };

            ExampleCustomResource.Passing = false;
            await ExampleCustomResource.Handle(request);
            mockHttp.VerifyNoOutstandingExpectation();
        }

        private string Serialize(object toSerialize) {
            var serializers = new JsonConverter[] { new StringEnumConverter() };
            return JsonConvert.SerializeObject(toSerialize, serializers);
        }
        
    }
}