using System;
using System.Net;
using static System.Net.HttpStatusCode;

namespace Cythral.CloudFormation.Cicd.Exceptions {
    public class InvalidSignatureException : RequestValidationException {

        public override HttpStatusCode StatusCode => BadRequest;

        public InvalidSignatureException(string message) : base(message) {}
    }
}