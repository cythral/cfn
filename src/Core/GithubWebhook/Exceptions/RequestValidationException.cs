using System;
using System.Net;

namespace Cythral.CloudFormation.GithubWebhook.Exceptions
{
    public abstract class RequestValidationException : Exception
    {
        public abstract HttpStatusCode StatusCode { get; }

        public RequestValidationException(string message) : base(message) { }
    }
}