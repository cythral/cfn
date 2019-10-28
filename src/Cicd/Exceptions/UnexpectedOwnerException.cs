using System;
using System.Net;
using static System.Net.HttpStatusCode;

namespace Cythral.CloudFormation.Cicd.Exceptions {
    public class UnexpectedOwnerException : RequestValidationException {

        public override HttpStatusCode StatusCode => BadRequest;

        public UnexpectedOwnerException(string message) : base(message) {}
    }
}