using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.ComponentModel.DataAnnotations;
using Cythral.CloudFormation.CustomResource;
using Cythral.CloudFormation.CustomResource.Attributes;
using CodeGeneration.Roslyn.Engine;
using NUnit.Framework;
using RichardSzalay.MockHttp;

namespace Tests {

    public class ModelWithMaxLengthProps {
        [MaxLength(12)]
        public virtual string Message { get; set; }
    }


    [CustomResource(ResourcePropertiesType=typeof(ModelWithMaxLengthProps))]
    public partial class CustomResourceWithMaxLengthProps : TestCustomResource {       
        public static MockHttpMessageHandler MockHttp = new MockHttpMessageHandler();

        static CustomResourceWithMaxLengthProps() {
            HttpClientProvider = new FakeHttpClientProvider(MockHttp);
        }
    }

    public class MaxLengthAttributeValidationTest {
        [Test]
        public async Task TestHandleShouldFailIfPropDoesntValidate() {
            CustomResourceWithMaxLengthProps.MockHttp
            .Expect("http://example.com")
            .WithJsonPayload(new Response {
                Status = ResponseStatus.FAILED,
                Reason = "The field Message must be a string or array type with a maximum length of '12'.",
            });
            
            var request = new Request<ModelWithMaxLengthProps> {
                RequestType = RequestType.Create,
                ResponseURL = "http://example.com",
                ResourceProperties = new ModelWithMaxLengthProps {
                    Message = "This message is waaay too long"
                }
            };

            await CustomResourceWithMaxLengthProps.Handle(request.ToStream());
            CustomResourceWithMaxLengthProps.MockHttp.VerifyNoOutstandingExpectation();
        }

        [Test]
        public async Task TestHandleShouldSucceedIfAllPropsMeetExpectations() {
            CustomResourceWithMaxLengthProps.MockHttp
            .Expect("http://example.com")
            .WithJsonPayload(new Response {
                Data = new {
                    Status = "Created"
                }
            });
            
            var request = new Request<ModelWithMaxLengthProps> {
                RequestType = RequestType.Create,
                ResponseURL = "http://example.com",
                ResourceProperties = new ModelWithMaxLengthProps {
                    Message = "Test message"
                }
            };

            await CustomResourceWithMaxLengthProps.Handle(request.ToStream());
            CustomResourceWithMaxLengthProps.MockHttp.VerifyNoOutstandingExpectation();
        }
    }
}