﻿using System.Collections.Generic;

using Amazon.Lambda.ApplicationLoadBalancerEvents;

using Cythral.CloudFormation.GithubWebhook.Entities;
using Cythral.CloudFormation.GithubWebhook.Exceptions;
using Cythral.CloudFormation.GithubWebhook;

using NUnit.Framework;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.Tests.GithubWebhook
{
    public class RequestValidatorTests
    {
        private static RequestValidator requestValidator = new RequestValidator();

        [Test]
        public void NonPostRequestsThrowMethodNotAllowed([Values("GET", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS")] string method)
        {
            var request = new ApplicationLoadBalancerRequest { HttpMethod = method };
            Assert.Throws(Is.InstanceOf<MethodNotAllowedException>(), () => requestValidator.Validate(request, validateSignature: false));
        }

        [Test]
        public void PostRequestsDontThrowMethodNotAllowed()
        {
            var request = new ApplicationLoadBalancerRequest { HttpMethod = "POST" };
            Assert.Throws(Is.Not.InstanceOf<MethodNotAllowedException>(), () => requestValidator.Validate(request, validateSignature: false));
        }

        [Test]
        public void NonPushEventsThrowEventNotAllowed([Values("PR_OPENED")] string evnt)
        {
            var request = new ApplicationLoadBalancerRequest
            {
                HttpMethod = "POST",
                Headers = new Dictionary<string, string> { ["x-github-event"] = evnt },
                Body = "{}"
            };

            Assert.Throws(Is.InstanceOf<EventNotAllowedException>(), () => requestValidator.Validate(request, validateSignature: false));
        }

        [Test]
        public void PushEventsDontThrowEventNotAllowed()
        {
            var request = new ApplicationLoadBalancerRequest
            {
                HttpMethod = "POST",
                Headers = new Dictionary<string, string> { ["x-github-event"] = "push" },
                Body = "{}"
            };

            Assert.Throws(Is.Not.InstanceOf<EventNotAllowedException>(), () => requestValidator.Validate(request, validateSignature: false));
        }

        [Test]
        public void NonJsonRequestsThrowBodyNotJson([Values("{badjson", "thisis: yaml")] string body)
        {
            var request = new ApplicationLoadBalancerRequest
            {
                HttpMethod = "POST",
                Headers = new Dictionary<string, string> { ["x-github-event"] = "push" },
                Body = body
            };

            Assert.Throws(Is.InstanceOf<BodyNotJsonException>(), () => requestValidator.Validate(request, validateSignature: false));
        }

        [Test]
        public void JsonRequestsDontThrowBodyNotJson()
        {
            var request = new ApplicationLoadBalancerRequest
            {
                HttpMethod = "POST",
                Headers = new Dictionary<string, string> { ["x-github-event"] = "push" },
                Body = "{}"
            };

            Assert.Throws(Is.Not.InstanceOf<BodyNotJsonException>(), () => requestValidator.Validate(request, validateSignature: false));
        }

        [Test]
        public void RequestsWithoutContentsThrowException()
        {
            var request = new ApplicationLoadBalancerRequest
            {
                HttpMethod = "POST",
                Headers = new Dictionary<string, string> { ["x-github-event"] = "push" },
                Body = Serialize(new PushEvent
                {
                    Repository = new Repository { }
                })
            };

            Assert.Throws(Is.InstanceOf<NoContentsUrlException>(), () => requestValidator.Validate(request, validateSignature: false));
        }

        [Test]
        public void RequestsWithContentsUrlsDontThrowException()
        {
            var request = new ApplicationLoadBalancerRequest
            {
                HttpMethod = "POST",
                Headers = new Dictionary<string, string> { ["x-github-event"] = "push" },
                Body = Serialize(new PushEvent
                {
                    Repository = new Repository
                    {
                        ContentsUrl = "https://api.github.com/repos/Codertocat/Hello-World/contents/{+path}"
                    }
                })
            };

            Assert.Throws(Is.Not.InstanceOf<NoContentsUrlException>(), () => requestValidator.Validate(request, validateSignature: false));
        }

        public static IEnumerable<ApplicationLoadBalancerRequest> UnexpectedOwnerRequests()
        {
            yield return new ApplicationLoadBalancerRequest
            {
                HttpMethod = "POST",
                Headers = new Dictionary<string, string> { ["x-github-event"] = "push" },
                Body = Serialize(new PushEvent
                {
                    Repository = new Repository
                    {
                        ContentsUrl = "https://api.github.com/repos/MaliciousUser/Hello-World/contents/{+path}"
                    }
                })
            };

            yield return new ApplicationLoadBalancerRequest
            {
                HttpMethod = "POST",
                Headers = new Dictionary<string, string> { ["x-github-event"] = "push" },
                Body = Serialize(new PushEvent
                {
                    Repository = new Repository
                    {
                        Owner = new User { Name = "MaliciousUser" },
                        ContentsUrl = "https://api.github.com/repos/Codertocat/Hello-World/contents/{+path}"
                    }
                })
            };
        }

        [Test]
        public void RequestsWithUnexpectedOwnerThrowException([ValueSource("UnexpectedOwnerRequests")] ApplicationLoadBalancerRequest request)
        {
            Assert.Throws(Is.InstanceOf<UnexpectedOwnerException>(), () => requestValidator.Validate(request, "Codertocat", validateSignature: false));
        }

        [Test]
        public void RequestsWithOkOwnerDontThrowException()
        {
            var request = new ApplicationLoadBalancerRequest
            {
                HttpMethod = "POST",
                Headers = new Dictionary<string, string> { ["x-github-event"] = "push" },
                Body = Serialize(new PushEvent
                {
                    Repository = new Repository
                    {
                        Owner = new User { Name = "Codertocat" },
                        ContentsUrl = "https://api.github.com/repos/Codertocat/Hello-World/contents/{+path}"
                    }
                })
            };

            Assert.Throws(Is.Not.InstanceOf<UnexpectedOwnerException>(), () => requestValidator.Validate(request, "Codertocat", validateSignature: false));
        }

        [Test]
        public void RequestsWithNoSignatureThrowException()
        {
            var request = new ApplicationLoadBalancerRequest
            {
                HttpMethod = "POST",
                Headers = new Dictionary<string, string> { ["x-github-event"] = "push" },
                Body = Serialize(new PushEvent
                {
                    Repository = new Repository
                    {
                        ContentsUrl = "https://api.github.com/repos/Codertocat/Hello-World/contents/{+path}"
                    }
                })
            };

            Assert.Throws(Is.InstanceOf<InvalidSignatureException>(), () => requestValidator.Validate(request));
        }

        [Test]
        public void RequestsWithBadSignatureThrowException()
        {
            var request = new ApplicationLoadBalancerRequest
            {
                HttpMethod = "POST",
                Headers = new Dictionary<string, string>
                {
                    ["x-github-event"] = "push",
                    ["x-hub-signature"] = "sha1=81e2a24bcf4284e90324378736dcb27a43cc79ed"
                },
                Body = Serialize(new PushEvent
                {
                    Repository = new Repository
                    {
                        ContentsUrl = "https://api.github.com/repos/Codertocat/Hello-World/contents/{+path}"
                    }
                })
            };

            Assert.Throws(Is.InstanceOf<InvalidSignatureException>(), () => requestValidator.Validate(request, signingKey: "test_key"));
        }

        [Test]
        public void RequestsWithGoodSignatureDoesntThrow()
        {
            var request = new ApplicationLoadBalancerRequest
            {
                HttpMethod = "POST",
                Headers = new Dictionary<string, string>
                {
                    ["x-github-event"] = "push",
                    ["x-hub-signature"] = "sha1=18f5e4779090fcaf575a4a4c9948d950efc81fcd"
                },
                Body = Serialize(new PushEvent
                {
                    Repository = new Repository
                    {
                        ContentsUrl = "https://api.github.com/repos/Codertocat/Hello-World/contents/{+path}"
                    }
                })
            };

            Assert.Throws(Is.Not.InstanceOf<InvalidSignatureException>(), () => requestValidator.Validate(request, signingKey: "test_key"));
        }
    }
}
