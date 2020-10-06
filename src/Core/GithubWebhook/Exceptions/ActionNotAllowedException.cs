using System.Net;

using static System.Net.HttpStatusCode;

namespace Cythral.CloudFormation.GithubWebhook.Exceptions
{
    public class ActionNotAllowedException : RequestValidationException
    {

        public override HttpStatusCode StatusCode => BadRequest;

        public ActionNotAllowedException() : base("Action is not allowed.") { }
    }
}