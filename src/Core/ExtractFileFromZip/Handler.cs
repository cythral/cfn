using System.Threading;
using System.Threading.Tasks;

using Amazon.Lambda.Core;

using Cythral.CloudFormation.AwsUtils.SimpleStorageService;

using Lambdajection.Attributes;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.ExtractFileFromZip
{
    [Lambda(typeof(Startup))]
    public partial class Handler
    {
        private readonly S3GetObjectFacade s3GetObjectFacade;

        public Handler(
            S3GetObjectFacade s3GetObjectFacade
        )
        {
            this.s3GetObjectFacade = s3GetObjectFacade;
        }

        public async Task<object?> Handle(Request request, CancellationToken cancellationToken = default)
        {
            var stringContent = await s3GetObjectFacade.GetZipEntryInObject(request.ZipLocation, request.Filename);
            return request.Filename.EndsWith(".json") ? Deserialize<object>(stringContent) : stringContent;
        }
    }
}