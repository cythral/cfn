using System.Net;
using System.Text;
using System.IO;
using System.Data;
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
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using FluentAssertions;
using NSubstitute;
using RichardSzalay.MockHttp;

using static System.Net.HttpStatusCode;
using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.Tests.Cicd.Entities {
    public class CommittedFileTest {
        [Test]
        public async Task FromContentsUrlRequestsFileViaHttp() {
            var contentsUrl = "https://api.github.com/repos/Codertocat/Hello-World/contents/{+path}";
            var mockHttp = new MockHttpMessageHandler();
            Func<HttpClient> httpFactory = () => new HttpClient(mockHttp);
            var templateName = "cicd.template.yml";
            var githubToken = "xx508xx63817x752xx74004x30705xx92x58349x5x78f5xx34xxxxx51";
            var templateContents = "example template";

            var config = new Config();
            config["GITHUB_TOKEN"] = githubToken;

            mockHttp
            .Expect($"https://api.github.com/repos/Codertocat/Hello-World/contents/{templateName}")
            .WithHeaders("Authorization", $"token {githubToken}")
            .Respond("text/plain", templateContents);
            
            var template = await CommittedFile.FromContentsUrl(contentsUrl, templateName, config, httpFactory);

            Assert.That(template.ToString(), Is.EqualTo(templateContents));
            mockHttp.VerifyNoOutstandingExpectation();
        }

        [Test]
        public async Task FromContentsUrlDoesNotThrow() {
            var contentsUrl = "https://api.github.com/repos/Codertocat/Hello-World/contents/{+path}";
            var mockHttp = new MockHttpMessageHandler();
            Func<HttpClient> httpFactory = () => new HttpClient(mockHttp);
            var templateName = "cicd.template.yml";
            var githubToken = "xx508xx63817x752xx74004x30705xx92x58349x5x78f5xx34xxxxx51";
            
            var config = new Config();
            config["GITHUB_TOKEN"] = githubToken;

            mockHttp
            .Expect($"https://api.github.com/repos/Codertocat/Hello-World/contents/{templateName}")
            .Respond(HttpStatusCode.NotFound, "text/plain", "Template doesn't exist");

            var template = await CommittedFile.FromContentsUrl(contentsUrl, templateName, config, httpFactory);

            Assert.That(template, Is.EqualTo(null));
        }
    }
}
