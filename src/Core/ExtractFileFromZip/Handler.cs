using System.Threading.Tasks;
using static System.Text.Json.JsonSerializer;

using Amazon.Lambda.Core;

using Cythral.CloudFormation.Aws;

namespace Cythral.CloudFormation.ExtractFileFromZip
{
    public class Handler
    {
        private static S3GetObjectFacade s3GetObjectFacade = new S3GetObjectFacade();

        public static async Task<object> Handle(Request request, ILambdaContext context = null)
        {
            var stringContent = await s3GetObjectFacade.GetZipEntryInObject(request.ZipLocation, request.Filename);
            return request.Filename.EndsWith(".json") ? Deserialize<object>(stringContent) : stringContent;
        }
    }
}