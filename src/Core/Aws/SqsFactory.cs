using System.Threading.Tasks;

using Amazon.SQS;

namespace Cythral.CloudFormation.Aws
{
    public class SqsFactory
    {
        public virtual Task<IAmazonSQS> Create()
        {
            return Task.FromResult((IAmazonSQS)new AmazonSQSClient());
        }
    }
}