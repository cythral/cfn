using System.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Cythral.CloudFormation.Cicd;
using Cythral.CloudFormation.Cicd.Events;
using Cythral.CloudFormation.Cicd.Entities;
using Cythral.CloudFormation.Cicd.Exceptions;
using Amazon.Lambda.ApplicationLoadBalancerEvents;
using FluentAssertions;
using NSubstitute;
using RichardSzalay.MockHttp;

using static System.Net.HttpStatusCode;
using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.Tests.Cicd {
    public class WebhookTest {
        [Test]
        public async Task HandleReturns400IfNoTemplate() {
            Webhook.Config = new Config() {
                ["GITHUB_TOKEN"] = "exampletoken",
                ["GITHUB_OWNER"] = "Codertocat",
                ["TEMPLATE_FILENAME"] = "cicd.template.yml",
                ["CONFIG_FILENAME"] = "cicd.config.yml",
                ["STACK_SUFFIX"] = "cicd",
                ["GITHUB_SIGNING_SECRET"] = ""
            };
            
            var contentsUrl = "https://api.github.com/repos/Codertocat/Hello-World/contents/{+path}";
            var request = new ApplicationLoadBalancerRequest {
                HttpMethod = "POST",
                Headers = new Dictionary<string,string> {
                    ["x-github-event"] = "push",
                    ["x-hub-signature"] = "sha1=98207d2da9b60e0c2cca87719d6d3cffa7a4dfa9"
                },
                Body = Serialize(new PushEvent {
                    Repository = new Repository {
                        Owner = new User { Name = "Codertocat" },
                        ContentsUrl = contentsUrl
                    }
                })
            };

            var httpMock = new MockHttpMessageHandler();
            CommittedFile.DefaultHttpClientFactory = () => new HttpClient(httpMock);

            httpMock
            .Expect($"https://api.github.com/repos/Codertocat/Hello-World/contents/cicd.template.yml")
            .Respond(HttpStatusCode.NotFound, "text/plain", "Template not found");

            var response = await Webhook.Handle(request);
            Assert.That(response.StatusCode, Is.EqualTo((int) NotFound));
        }
    }
}