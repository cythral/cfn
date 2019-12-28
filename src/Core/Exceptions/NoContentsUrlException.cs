using System.Net;
using static System.Net.HttpStatusCode;

namespace Cythral.CloudFormation.Exceptions
{
    public class NoContentsUrlException : RequestValidationException
    {

        public override HttpStatusCode StatusCode => BadRequest;

        public NoContentsUrlException() : base("The payload did not have a contents url.") { }
    }
}