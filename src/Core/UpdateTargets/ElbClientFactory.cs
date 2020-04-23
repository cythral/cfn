using Amazon.ElasticLoadBalancingV2;

namespace Cythral.CloudFormation.UpdateTargets
{
    public class ElbClientFactory
    {
        public virtual IAmazonElasticLoadBalancingV2 Create()
        {
            return new AmazonElasticLoadBalancingV2Client();
        }
    }
}