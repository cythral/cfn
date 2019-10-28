using System;
using System.Net;
using static System.Net.HttpStatusCode;

namespace Cythral.CloudFormation.Cicd.Exceptions {
    public class BodyNotJsonException : RequestValidationException {

        public override HttpStatusCode StatusCode => BadRequest;

        public BodyNotJsonException() : base("The received body was not in JSON format.") {}
    }
}