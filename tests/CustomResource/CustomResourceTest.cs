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
using Amazon.S3;

namespace Tests {
    

    [CustomResource(typeof(object))]
    public partial class ExampleCustomResource : TestCustomResource {
        public static bool Passing { get; set; } = true;            

        public static MockHttpMessageHandler MockHttp = new MockHttpMessageHandler();

        static ExampleCustomResource() {
            HttpClientProvider = new FakeHttpClientProvider(MockHttp);
        }

        public override void ThrowIfNotPassing() {
            if(!Passing) {
                throw new Exception("Expected this error message");
            }
        }
    }


    // used for "update requires replacement" tests
    public class MessageResourceProperties {
        [UpdateRequiresReplacement]
        public string Message { get; set; }
    }

    [CustomResource(typeof(MessageResourceProperties))]
    public partial class MessageCustomResource : TestCustomResource {
        public static bool Passing { get; set; } = true;            

        public static MockHttpMessageHandler MockHttp = new MockHttpMessageHandler();

        static MessageCustomResource() {
            HttpClientProvider = new FakeHttpClientProvider(MockHttp);
        }
    }

    public class CustomResourceTest {
        [Test]
        public async Task TestHandleCallsCreate() {
            ExampleCustomResource.MockHttp
            .Expect("http://example.com")
            .WithJsonPayload(new Response {
                Data = new {
                    Status = "Created"
                }
            });
            
            var request = new Request<object> {
                RequestType = RequestType.Create,
                ResponseURL = "http://example.com"
            };

            await ExampleCustomResource.Handle(request.ToStream());
            ExampleCustomResource.MockHttp.VerifyNoOutstandingExpectation();
        }

        [Test]
        public async Task TestHandleCallsUpdate() {
            ExampleCustomResource.MockHttp
            .Expect("http://example.com")
            .WithJsonPayload(new Response {
                Data = new {
                    Status = "Updated"
                }
            });

            var request = new Request<object> {
                RequestType = RequestType.Update,
                ResponseURL = "http://example.com"
            };

            await ExampleCustomResource.Handle(request.ToStream());
            ExampleCustomResource.MockHttp.VerifyNoOutstandingExpectation();
        }

        [Test]
        public async Task TestHandleCallsDelete() {
            ExampleCustomResource.MockHttp
            .Expect("http://example.com")
            .WithJsonPayload(new Response {
                Data = new {
                    Status = "Deleted"
                }
            });

            var request = new Request<object> {
                RequestType = RequestType.Delete,
                ResponseURL = "http://example.com"
            };

            await ExampleCustomResource.Handle(request.ToStream());
            ExampleCustomResource.MockHttp.VerifyNoOutstandingExpectation();
        }

        [Test]
        public async Task TestHandleRespondsOnError() {
            ExampleCustomResource.MockHttp
            .Expect("http://example.com")
            .WithJsonPayload(new Response {
                Status = ResponseStatus.FAILED,
                Reason = "Expected this error message",
            });
        
            var request = new Request<object> {
                RequestType = RequestType.Create,
                ResponseURL = "http://example.com"
            };

            ExampleCustomResource.Passing = false;
            await ExampleCustomResource.Handle(request.ToStream());
            ExampleCustomResource.MockHttp.VerifyNoOutstandingExpectation();
        }
        

        [Test]
        public async Task TestUpdateTriggersReplacement() {
            MessageCustomResource.MockHttp
            .Expect("http://example.com")
            .WithJsonPayload(new Response {
                Status = ResponseStatus.SUCCESS,
                Data = new {
                    Status = "Created"
                }
            });

            var request = new Request<MessageResourceProperties> {
                RequestType = RequestType.Update,
                ResponseURL = "http://example.com",
                ResourceProperties = new MessageResourceProperties {
                    Message = "A"
                },
                OldResourceProperties = new MessageResourceProperties {
                    Message = "B"
                }
            };

            var resource = await MessageCustomResource.Handle(request.ToStream());
            Console.WriteLine(resource.Request.ResourceProperties.Message);
            MessageCustomResource.MockHttp.VerifyNoOutstandingExpectation();
            
            Assert.True(resource.DeleteWasCalled);
            Assert.True(resource.CreateWasCalled);
            Assert.False(resource.UpdateWasCalled);
        }
    }
}