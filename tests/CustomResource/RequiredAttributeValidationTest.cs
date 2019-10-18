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

    public class ModelWithRequiredProps {
        [Required(ErrorMessage="Message is required")]
        public virtual string Message { get; set; }
    }


    [CustomResource(typeof(ModelWithRequiredProps))]
    public partial class CustomResourceWithRequiredProps : TestCustomResource {      
        public static MockHttpMessageHandler MockHttp = new MockHttpMessageHandler();

        static CustomResourceWithRequiredProps() {
            HttpClientProvider = new FakeHttpClientProvider(MockHttp);
        }
    }

    public class CustomResourceWithRequiredPropsTest {
        [Test]
        public async Task TestHandleShouldFailIfRequiredPropIsMissing() {
            CustomResourceWithRequiredProps.MockHttp
            .Expect("http://example.com")
            .WithJsonPayload(new Response {
                Status = ResponseStatus.FAILED,
                Reason = "Message is required",
            });
            
            var request = new Request<ModelWithRequiredProps> {
                RequestType = RequestType.Create,
                ResponseURL = "http://example.com",
                ResourceProperties = new ModelWithRequiredProps()
            };

            await CustomResourceWithRequiredProps.Handle(request.ToStream());
            CustomResourceWithRequiredProps.MockHttp.VerifyNoOutstandingExpectation();
        }

        [Test]
        public async Task TestHandleShouldSucceedIfAllPropsArePresent() {
            CustomResourceWithRequiredProps.MockHttp
            .Expect("http://example.com")
            .WithJsonPayload(new Response() {
                Data = new {
                    Status = "Created"
                }
            });
            
            var request = new Request<ModelWithRequiredProps> {
                RequestType = RequestType.Create,
                ResponseURL = "http://example.com",
                ResourceProperties = new ModelWithRequiredProps {
                    Message = "Test message"
                }
            };

            await CustomResourceWithRequiredProps.Handle(request.ToStream());
            CustomResourceWithRequiredProps.MockHttp.VerifyNoOutstandingExpectation();
        }
    }
}