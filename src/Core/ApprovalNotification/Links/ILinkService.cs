using System.Threading.Tasks;

namespace Cythral.CloudFormation.ApprovalNotification.Links
{
    public interface ILinkService
    {
        Task<string> Shorten(string url);
    }
}