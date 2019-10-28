using System;
using System.Net;
using static System.Net.HttpStatusCode;

namespace Cythral.CloudFormation.Cicd.Exceptions {
    public class EventNotAllowedException : RequestValidationException {

        public override HttpStatusCode StatusCode => BadRequest;

        public EventNotAllowedException(string message) : base(message) {}
    }
}