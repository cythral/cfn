using System.Net;

namespace Cythral.CloudFormation
{
    public interface IDnsResolver
    {
        IPHostEntry Resolve(string hostname);
    }
}