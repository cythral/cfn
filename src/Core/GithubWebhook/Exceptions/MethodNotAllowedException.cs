using System.Net;
using static System.Net.HttpStatusCode;

namespace Cythral.CloudFormation.GithubWebhook.Exceptions
{
    public class MethodNotAllowedException : RequestValidationException
    {
        public override HttpStatusCode StatusCode => MethodNotAllowed;
        public MethodNotAllowedException(string message) : base(message) { }
    }
}