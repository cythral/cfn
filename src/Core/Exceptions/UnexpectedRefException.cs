using System.Net;
using static System.Net.HttpStatusCode;

namespace Cythral.CloudFormation.Exceptions
{
    public class UnexpectedRefException : RequestValidationException
    {
        public override HttpStatusCode StatusCode => BadRequest;

        public UnexpectedRefException(string message) : base(message) { }

    }
}
