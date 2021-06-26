using System.Net;

namespace Cythral.CloudFormation.UpdateTargets
{
    public class DnsResolver
    {
        public virtual IPHostEntry Resolve(string hostname)
        {
            return Dns.GetHostEntry(hostname);
        }
    }
}