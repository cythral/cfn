extern alias GithubWebhook;

using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using FluentAssertions;

using GithubWebhook::Cythral.CloudFormation.GithubWebhook.Github;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using NSubstitute;

using NUnit.Framework;

using static System.Net.HttpStatusCode;
using static NSubstitute.Arg;

namespace Cythral.CloudFormation.GithubWebhook.Github.Tests
{
    using Config = GithubWebhook::Cythral.CloudFormation.GithubWebhook.Config;
    using GithubHttpClient = GithubWebhook::Cythral.CloudFormation.GithubWebhook.GithubHttpClient;

    public class GithubFileFetcherTests
    {
        private const string url = "http://url/test/{+path}";
        private const string filename = "filename";
        private const string gitRef = "gitRef";
        private const string expectedContentString = "contents";
        private const string expectedUrl = "http://url/test/filename";
        private const string expectedUrlWithRef = "http://url/test/filename?ref=gitRef";
        private static StringContent expectedContent = new StringContent(expectedContentString);

        [Test]
        public async Task GetMethodIsUsed()
        {
            var config = new Config();
            var options = Options.Create(config);
            var logger = Substitute.For<ILogger<GithubFileFetcher>>();
            var httpClient = Substitute.For<GithubHttpClient>(options);

            httpClient.SendAsync(Any<HttpRequestMessage>()).Returns(new HttpResponseMessage
            {
                StatusCode = OK,
                Content = expectedContent,
            });

            var fetcher = new GithubFileFetcher(httpClient, options, logger);
            var result = await fetcher.Fetch(url, filename);

            await httpClient.Received().SendAsync(Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get));
        }

        [Test]
        public async Task UriIsSetCorrectly()
        {
            var config = new Config();
            var options = Options.Create(config);
            var logger = Substitute.For<ILogger<GithubFileFetcher>>();
            var httpClient = Substitute.For<GithubHttpClient>(options);

            httpClient.SendAsync(Any<HttpRequestMessage>()).Returns(new HttpResponseMessage
            {
                StatusCode = OK,
                Content = expectedContent,
            });

            var fetcher = new GithubFileFetcher(httpClient, options, logger);
            var result = await fetcher.Fetch(url, filename);

            await httpClient.Received().SendAsync(Is<HttpRequestMessage>(req => req.RequestUri == new Uri(expectedUrl)));
        }

        [Test]
        public async Task GitRefIsAddedToUri()
        {
            var config = new Config();
            var options = Options.Create(config);
            var logger = Substitute.For<ILogger<GithubFileFetcher>>();
            var httpClient = Substitute.For<GithubHttpClient>(options);

            httpClient.SendAsync(Any<HttpRequestMessage>()).Returns(new HttpResponseMessage
            {
                StatusCode = OK,
                Content = expectedContent
            });

            var fetcher = new GithubFileFetcher(httpClient, options, logger);
            var result = await fetcher.Fetch(url, filename, gitRef);

            await httpClient.Received().SendAsync(Is<HttpRequestMessage>(req => req.RequestUri == new Uri(expectedUrlWithRef)));
        }

        [Test]
        public async Task AcceptHeaderContainsGithubRaw()
        {
            var config = new Config();
            var options = Options.Create(config);
            var logger = Substitute.For<ILogger<GithubFileFetcher>>();
            var httpClient = Substitute.For<GithubHttpClient>(options);

            httpClient.SendAsync(Any<HttpRequestMessage>()).Returns(new HttpResponseMessage
            {
                StatusCode = OK,
                Content = expectedContent
            });

            var fetcher = new GithubFileFetcher(httpClient, options, logger);
            var result = await fetcher.Fetch(url, filename, gitRef);

            await httpClient.Received().SendAsync(Is<HttpRequestMessage>(req =>
                req.Headers.Accept.Any(header => header.MediaType == "application/vnd.github.v3.raw")
            ));
        }

        [Test]
        public async Task ContentIsReturned()
        {
            var config = new Config();
            var options = Options.Create(config);
            var logger = Substitute.For<ILogger<GithubFileFetcher>>();
            var httpClient = Substitute.For<GithubHttpClient>(options);

            httpClient.SendAsync(Any<HttpRequestMessage>()).Returns(new HttpResponseMessage
            {
                StatusCode = OK,
                Content = expectedContent
            });

            var fetcher = new GithubFileFetcher(httpClient, options, logger);
            var result = await fetcher.Fetch(url, filename, gitRef);

            result.Should().BeEquivalentTo(expectedContentString);
        }

        [Test]
        public async Task NoContentIsReturnedIfStatusCodeIsNotOk()
        {
            var config = new Config();
            var options = Options.Create(config);
            var logger = Substitute.For<ILogger<GithubFileFetcher>>();
            var httpClient = Substitute.For<GithubHttpClient>(options);

            httpClient.SendAsync(Any<HttpRequestMessage>()).Returns(new HttpResponseMessage
            {
                StatusCode = NotFound,
                Content = expectedContent
            });

            var fetcher = new GithubFileFetcher(httpClient, options, logger);
            var result = await fetcher.Fetch(url, filename, gitRef);

            result.Should().BeNull();
        }
    }
}