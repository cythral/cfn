using System.Net;
using static System.Net.HttpStatusCode;

namespace Cythral.CloudFormation.GithubWebhook.Exceptions
{
    public class EventNotAllowedException : RequestValidationException
    {

        public override HttpStatusCode StatusCode => BadRequest;

        public EventNotAllowedException(string message) : base(message) { }
    }
}