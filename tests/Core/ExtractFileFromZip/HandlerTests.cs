using System.Text.Json;
using System.Threading.Tasks;

using Cythral.CloudFormation.AwsUtils.SimpleStorageService;
using Cythral.CloudFormation.ExtractFileFromZip;

using FluentAssertions;

using NSubstitute;

using NUnit.Framework;

using static NSubstitute.Arg;

using Handler = Cythral.CloudFormation.ExtractFileFromZip.Handler;

namespace Cythral.CloudFormation.Tests.ExtractFileFromZip
{
    public class HandlerTests
    {
        public class ExampleObject
        {
            public string A { get; set; } = string.Empty;
        }

        private static S3GetObjectFacade s3GetObjectFacade = Substitute.For<S3GetObjectFacade>();

        private const string zipLocation = "zipLocation";
        private const string filename = "filename";
        private const string contents = "contents";

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
            var s3GetObjectFacade = Substitute.For<S3GetObjectFacade>();
            var handler = new Handler(s3GetObjectFacade);

            s3GetObjectFacade.GetZipEntryInObject(Any<string>(), Any<string>()).Returns(contents);
            var result = await handler.Handle(request);

            result.Should().Be(contents);
            await s3GetObjectFacade.Received().GetZipEntryInObject(Arg.Is(zipLocation), Arg.Is(filename));
        }

        [Test]
        public async Task HandleDeserializesTheContentsIfFilenameEndsWithJson()
        {
            var jsonFilename = "test.json";
            var request = CreateRequest();
            request.Filename = jsonFilename;

            var s3GetObjectFacade = Substitute.For<S3GetObjectFacade>();
            var handler = new Handler(s3GetObjectFacade);

            s3GetObjectFacade.GetZipEntryInObject(string.Empty, string.Empty).ReturnsForAnyArgs("{\"A\": \"B\"}");
            var result = (JsonElement?)await handler.Handle(request);

            result?.GetProperty("A").ToString().Should().Be("B");
            await s3GetObjectFacade.Received().GetZipEntryInObject(Is(zipLocation), Is(jsonFilename));
        }
    }
}
