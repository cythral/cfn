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

    public class ModelWithMaxLengthProps {
        [MaxLength(12)]
        public string Message;
    }


    [CustomResource(typeof(ModelWithMaxLengthProps))]
    public partial class CustomResourceWithMaxLengthProps {
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

    public class MaxLengthAttributeValidationTest {
        [Test]
        public async Task TestHandleShouldFailIfPropDoesntValidate() {
            var expectedPayload = new Response() {
                Status = ResponseStatus.FAILED,
                Reason = "The field Message must be a string or array type with a maximum length of '12'.",
                Data = new object()
            };
            
            var mockHttp = new MockHttpMessageHandler();
            var httpProvider = new FakeHttpClientProvider(mockHttp);
            CustomResourceWithMaxLengthProps.HttpClientProvider = httpProvider;

            mockHttp
                .Expect("http://example.com")
                .WithContent(Serialize(expectedPayload));
            
            var request = new Request<ModelWithMaxLengthProps>() {
                RequestType = RequestType.Create,
                ResponseURL = "http://example.com",
                ResourceProperties = new ModelWithMaxLengthProps() {
                    Message = "This message is waaay too long"
                }
            };

            await CustomResourceWithMaxLengthProps.Handle(request);
            mockHttp.VerifyNoOutstandingExpectation();
        }

        [Test]
        public async Task TestHandleShouldSucceedIfAllPropsMeetExpectations() {
            var expectedPayload = new Response() {
                Data = new {
                    Status = "Created"
                }
            };
            
            var mockHttp = new MockHttpMessageHandler();
            var httpProvider = new FakeHttpClientProvider(mockHttp);
            CustomResourceWithMaxLengthProps.HttpClientProvider = httpProvider;

            mockHttp
                .Expect("http://example.com")
                .WithContent(Serialize(expectedPayload));
            
            var request = new Request<ModelWithMaxLengthProps>() {
                RequestType = RequestType.Create,
                ResponseURL = "http://example.com",
                ResourceProperties = new ModelWithMaxLengthProps() {
                    Message = "Test message"
                }
            };

            await CustomResourceWithMaxLengthProps.Handle(request);
            mockHttp.VerifyNoOutstandingExpectation();
        }

        private string Serialize(object toSerialize) {
            var serializers = new JsonConverter[] { new StringEnumConverter() };
            return JsonConvert.SerializeObject(toSerialize, serializers);
        }

    }
}