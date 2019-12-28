using System.Net;

namespace Cythral.CloudFormation.Facades
{
    public class DnsResolver : IDnsResolver
    {
        public IPHostEntry Resolve(string hostname)
        {
            return Dns.GetHostEntry(hostname);
        }
    }
}