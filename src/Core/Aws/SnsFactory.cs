using System.Threading.Tasks;

using Amazon.SimpleNotificationService;

namespace Cythral.CloudFormation.Aws
{
    public class SnsFactory
    {
        public virtual Task<IAmazonSimpleNotificationService> Create()
        {
            return Task.FromResult((IAmazonSimpleNotificationService)new AmazonSimpleNotificationServiceClient());
        }
    }
}