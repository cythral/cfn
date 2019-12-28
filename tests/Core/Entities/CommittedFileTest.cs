using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;

using Cythral.CloudFormation.Entities;

using NUnit.Framework;

using RichardSzalay.MockHttp;

namespace Cythral.CloudFormation.Tests.Entities
{
    public class CommittedFileTest
    {
        [Test]
        public async Task FromContentsUrlRequestsFileViaHttp()
        {
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
            .WithHeaders("Accept", "application/vnd.github.VERSION.raw")
            .Respond("text/plain", templateContents);

            var template = await CommittedFile.FromContentsUrl(contentsUrl, templateName, config, null, httpFactory);

            Assert.That(template?.ToString(), Is.EqualTo(templateContents));
            mockHttp.VerifyNoOutstandingExpectation();
        }

        [Test]
        public async Task FromContentsUrlRequestsGitRef()
        {
            var contentsUrl = "https://api.github.com/repos/Codertocat/Hello-World/contents/{+path}";
            var mockHttp = new MockHttpMessageHandler();
            Func<HttpClient> httpFactory = () => new HttpClient(mockHttp);
            var templateName = "cicd.template.yml";
            var githubToken = "xx508xx63817x752xx74004x30705xx92x58349x5x78f5xx34xxxxx51";
            var templateContents = "example template";
            var gitRef = "develop";

            var config = new Config();
            config["GITHUB_TOKEN"] = githubToken;

            mockHttp
            .Expect($"https://api.github.com/repos/Codertocat/Hello-World/contents/{templateName}?ref={gitRef}")
            .WithHeaders("Authorization", $"token {githubToken}")
            .WithHeaders("Accept", "application/vnd.github.VERSION.raw")
            .Respond("text/plain", templateContents);

            var template = await CommittedFile.FromContentsUrl(contentsUrl, templateName, config, gitRef, httpFactory);

            Assert.That(template?.ToString(), Is.EqualTo(templateContents));
            mockHttp.VerifyNoOutstandingExpectation();
        }

        [Test]
        public async Task FromContentsUrlDoesNotThrow()
        {
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

            var template = await CommittedFile.FromContentsUrl(contentsUrl, templateName, config, null, httpFactory);

            Assert.That(template, Is.EqualTo(null));
        }
    }
}
