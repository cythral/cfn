using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Text.Json;

using Amazon.Lambda.ApplicationLoadBalancerEvents;

using Cythral.CloudFormation.Entities;
using Cythral.CloudFormation.Aws;
using Cythral.CloudFormation.ExtractFileFromZip;

using NUnit.Framework;
using NSubstitute;

using RichardSzalay.MockHttp;

using static System.Net.HttpStatusCode;
using static System.Text.Json.JsonSerializer;

using Handler = Cythral.CloudFormation.ExtractFileFromZip.Handler;

namespace Cythral.CloudFormation.Tests.ExtractFileFromZip
{
    public class HandlerTests
    {
        public class ExampleObject
        {
            public string A { get; set; }
        }

        private static S3GetObjectFacade s3GetObjectFacade = Substitute.For<S3GetObjectFacade>();

        private const string zipLocation = "zipLocation";
        private const string filename = "filename";
        private const string contents = "contents";

        [SetUp]
        public void SetupS3GetObjectFacade()
        {
            TestUtils.SetPrivateStaticField(typeof(Handler), "s3GetObjectFacade", s3GetObjectFacade);
            s3GetObjectFacade.ClearReceivedCalls();
            s3GetObjectFacade.GetZipEntryInObject(Arg.Any<string>(), Arg.Any<string>()).Returns(contents);
        }

        private Request CreateRequest()
        {
            return new Request
            {
                ZipLocation = zipLocation,
                Filename = filename
            };
        }

        [Test]
        public async Task HandleReturnsContents()
        {
            var request = CreateRequest();
            var result = await Handler.Handle(request);

            Assert.That(result, Is.EqualTo(contents));
            await s3GetObjectFacade.Received().GetZipEntryInObject(Arg.Is(zipLocation), Arg.Is(filename));
        }

        [Test]
        public async Task HandleDeserializesTheContentsIfFilenameEndsWithJson()
        {
            var jsonFilename = "test.json";
            var request = CreateRequest();
            request.Filename = jsonFilename;

            s3GetObjectFacade.GetZipEntryInObject(null, null).ReturnsForAnyArgs("{\"A\": \"B\"}");
            var result = await Handler.Handle(request);

            Assert.That(((JsonElement)result).GetProperty("A").ToString(), Is.EqualTo("B"));
            await s3GetObjectFacade.Received().GetZipEntryInObject(Arg.Is(zipLocation), Arg.Is(jsonFilename));
        }
    }
}
