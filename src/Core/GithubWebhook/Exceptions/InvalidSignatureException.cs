using System.Net;
using static System.Net.HttpStatusCode;

namespace Cythral.CloudFormation.GithubWebhook.Exceptions
{
    public class InvalidSignatureException : RequestValidationException
    {

        public override HttpStatusCode StatusCode => BadRequest;

        public InvalidSignatureException(string message) : base(message) { }
    }
}