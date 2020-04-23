using System.Net;

namespace Cythral.CloudFormation.UpdateTargets.DnsResolver
{
    public class DnsResolverFactory
    {
        public virtual IDnsResolver Create()
        {
            return new DefaultDnsResolver();
        }
    }
}