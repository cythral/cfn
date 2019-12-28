using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using CodeGeneration.Roslyn.Engine;

using Cythral.CloudFormation.CustomResource;
using Cythral.CloudFormation.CustomResource.Attributes;

using NUnit.Framework;

using RichardSzalay.MockHttp;

namespace Tests
{

    public class ModelWithMinLengthProps
    {
        [MinLength(4)]
        public virtual string Message { get; set; }
    }


    [CustomResource(ResourcePropertiesType = typeof(ModelWithMinLengthProps))]
    public partial class CustomResourceWithMinLengthProps : TestCustomResource
    {
        public static MockHttpMessageHandler MockHttp = new MockHttpMessageHandler();

        static CustomResourceWithMinLengthProps()
        {
            HttpClientProvider = new FakeHttpClientProvider(MockHttp);
        }
    }

    public class MinLengthAttributeValidationTest
    {
        [Test]
        public async Task TestHandleShouldFailIfPropDoesntValidate()
        {
            CustomResourceWithMinLengthProps.MockHttp
            .Expect("http://example.com")
            .WithJsonPayload(new Response
            {
                Status = ResponseStatus.FAILED,
                Reason = "The field Message must be a string or array type with a minimum length of '4'."
            });

            var request = new Request<ModelWithMinLengthProps>
            {
                RequestType = RequestType.Create,
                ResponseURL = "http://example.com",
                ResourceProperties = new ModelWithMinLengthProps
                {
                    Message = "tea"
                }
            };

            await CustomResourceWithMinLengthProps.Handle(request.ToStream());
            CustomResourceWithMinLengthProps.MockHttp.VerifyNoOutstandingExpectation();
        }

        [Test]
        public async Task TestHandleShouldSucceedIfAllPropsMeetExpectations()
        {
            CustomResourceWithMinLengthProps.MockHttp
            .Expect("http://example.com")
            .WithJsonPayload(new Response()
            {
                Data = new
                {
                    Status = "Created"
                }
            });

            var request = new Request<ModelWithMinLengthProps>()
            {
                RequestType = RequestType.Create,
                ResponseURL = "http://example.com",
                ResourceProperties = new ModelWithMinLengthProps()
                {
                    Message = "Test message"
                }
            };

            await CustomResourceWithMinLengthProps.Handle(request.ToStream());
            CustomResourceWithMinLengthProps.MockHttp.VerifyNoOutstandingExpectation();
        }
    }
}