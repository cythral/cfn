using System.Net;
using static System.Net.HttpStatusCode;

namespace Cythral.CloudFormation.Exceptions
{
    public class EventNotAllowedException : RequestValidationException
    {

        public override HttpStatusCode StatusCode => BadRequest;

        public EventNotAllowedException(string message) : base(message) { }
    }
}