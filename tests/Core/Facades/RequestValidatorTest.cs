using System.Collections.Generic;

using Amazon.Lambda.ApplicationLoadBalancerEvents;

using Cythral.CloudFormation.Entities;
using Cythral.CloudFormation.Events;
using Cythral.CloudFormation.Exceptions;
using Cythral.CloudFormation.Facades;

using NUnit.Framework;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.Tests.Facades
{
    class RequestValidatorTest
    {
        [Test]
        public void NonPostRequestsThrowMethodNotAllowed([Values("GET", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS")] string method)
        {
            var request = new ApplicationLoadBalancerRequest { HttpMethod = method };
            Assert.Throws(Is.InstanceOf<MethodNotAllowedException>(), () => RequestValidator.Validate(request, validateSignature: false));
        }

        [Test]
        public void PostRequestsDontThrowMethodNotAllowed()
        {
            var request = new ApplicationLoadBalancerRequest { HttpMethod = "POST" };
            Assert.Throws(Is.Not.InstanceOf<MethodNotAllowedException>(), () => RequestValidator.Validate(request, validateSignature: false));
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

            Assert.Throws(Is.InstanceOf<EventNotAllowedException>(), () => RequestValidator.Validate(request, validateSignature: false));
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

            Assert.Throws(Is.Not.InstanceOf<EventNotAllowedException>(), () => RequestValidator.Validate(request, validateSignature: false));
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

            Assert.Throws(Is.InstanceOf<BodyNotJsonException>(), () => RequestValidator.Validate(request, validateSignature: false));
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

            Assert.Throws(Is.Not.InstanceOf<BodyNotJsonException>(), () => RequestValidator.Validate(request, validateSignature: false));
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

            Assert.Throws(Is.InstanceOf<NoContentsUrlException>(), () => RequestValidator.Validate(request, validateSignature: false));
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

            Assert.Throws(Is.Not.InstanceOf<NoContentsUrlException>(), () => RequestValidator.Validate(request, validateSignature: false));
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
            Assert.Throws(Is.InstanceOf<UnexpectedOwnerException>(), () => RequestValidator.Validate(request, "Codertocat", validateSignature: false));
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

            Assert.Throws(Is.Not.InstanceOf<UnexpectedOwnerException>(), () => RequestValidator.Validate(request, "Codertocat", validateSignature: false));
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

            Assert.Throws(Is.InstanceOf<InvalidSignatureException>(), () => RequestValidator.Validate(request));
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

            Assert.Throws(Is.InstanceOf<InvalidSignatureException>(), () => RequestValidator.Validate(request, signingKey: "test_key"));
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
                    ["x-hub-signature"] = "sha1=ca7acf8d405303b2e4a08486a005ef29a730c69b"
                },
                Body = Serialize(new PushEvent
                {
                    Repository = new Repository
                    {
                        ContentsUrl = "https://api.github.com/repos/Codertocat/Hello-World/contents/{+path}"
                    }
                })
            };

            Assert.Throws(Is.Not.InstanceOf<InvalidSignatureException>(), () => RequestValidator.Validate(request, signingKey: "test_key"));
        }

        [Test]
        public void RequestsOnNonDefaultBranchThrowException()
        {
            var request = new ApplicationLoadBalancerRequest
            {
                HttpMethod = "POST",
                Headers = new Dictionary<string, string>
                {
                    ["x-github-event"] = "push",
                    ["x-hub-signature"] = "sha1=5a038b59d634709f777af46b499eff9e3ca48a20",
                },
                Body = Serialize(new PushEvent
                {
                    Ref = "pr/test",
                    Repository = new Repository
                    {
                        ContentsUrl = "https://api.github.com/repos/Codertocat/Hello-World/contents/{+path}",
                        DefaultBranch = "master"
                    }
                })
            };

            Assert.Throws(Is.InstanceOf<UnexpectedRefException>(), () => RequestValidator.Validate(request, signingKey: "test_key"));
        }

        [Test]
        public void RequestsOnDefaultBranchDontThrowException()
        {
            var request = new ApplicationLoadBalancerRequest
            {
                HttpMethod = "POST",
                Headers = new Dictionary<string, string>
                {
                    ["x-github-event"] = "push",
                },
                Body = Serialize(new PushEvent
                {
                    Ref = "refs/heads/master",
                    Repository = new Repository
                    {
                        DefaultBranch = "master"
                    }
                })
            };

            Assert.Throws(Is.Not.InstanceOf<UnexpectedRefException>(), () => RequestValidator.Validate(request, signingKey: "test_key"));
        }
    }
}
