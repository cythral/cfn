using System.Net;

namespace Cythral.CloudFormation.UpdateTargets.DnsResolver
{
    public interface IDnsResolver
    {
        IPHostEntry Resolve(string hostname);
    }
}