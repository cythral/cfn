using System.Net;

namespace Cythral.CloudFormation.UpdateTargets.DnsResolver
{
    public class DefaultDnsResolver : IDnsResolver
    {
        public IPHostEntry Resolve(string hostname)
        {
            return Dns.GetHostEntry(hostname);
        }
    }
}