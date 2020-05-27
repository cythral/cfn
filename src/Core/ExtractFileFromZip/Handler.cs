using System.Threading.Tasks;
using static System.Text.Json.JsonSerializer;

using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;

using Cythral.CloudFormation.AwsUtils.SimpleStorageService;

namespace Cythral.CloudFormation.ExtractFileFromZip
{
    public class Handler
    {
        private static S3GetObjectFacade s3GetObjectFacade = new S3GetObjectFacade();

        [LambdaSerializer(typeof(DefaultLambdaJsonSerializer))]
        public static async Task<object> Handle(Request request, ILambdaContext context = null)
        {
            var stringContent = await s3GetObjectFacade.GetZipEntryInObject(request.ZipLocation, request.Filename);
            return request.Filename.EndsWith(".json") ? Deserialize<object>(stringContent) : stringContent;
        }
    }
}