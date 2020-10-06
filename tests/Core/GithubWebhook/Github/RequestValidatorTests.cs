extern alias GithubWebhook;

using System.Collections.Generic;

using Amazon.Lambda.ApplicationLoadBalancerEvents;

using GithubWebhook::Cythral.CloudFormation.GithubWebhook.Exceptions;
using GithubWebhook::Cythral.CloudFormation.GithubWebhook.Github;
using GithubWebhook::Cythral.CloudFormation.GithubWebhook.Github.Entities;

using Microsoft.Extensions.Options;

using NUnit.Framework;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.GithubWebhook.Github.Tests
{
    using Config = GithubWebhook::Cythral.CloudFormation.GithubWebhook.Config;

    public class RequestValidatorTests
    {

        [Test]
        public void NonPostRequestsThrowMethodNotAllowed([Values("GET", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS")] string method)
        {
            var request = new ApplicationLoadBalancerRequest { HttpMethod = method };
            var options = Options.Create(new Config { GithubOwner = "Codertocat", GithubSigningSecret = "test_key" });
            var requestValidator = new RequestValidator(options);

            Assert.Throws(Is.InstanceOf<MethodNotAllowedException>(), () => requestValidator.Validate(request));
        }

        [Test]
        public void PostRequestsDontThrowMethodNotAllowed()
        {
            var request = new ApplicationLoadBalancerRequest { HttpMethod = "POST" };

            var options = Options.Create(new Config { GithubOwner = "Codertocat", GithubSigningSecret = "test_key" });
            var requestValidator = new RequestValidator(options);

            Assert.Throws(Is.Not.InstanceOf<MethodNotAllowedException>(), () => requestValidator.Validate(request));
        }

        [Test]
        public void OnlyPushAndPullRequestEventsAreAllowed([Values("package_pushed")] string evnt)
        {
            var request = new ApplicationLoadBalancerRequest
            {
                HttpMethod = "POST",
                Headers = new Dictionary<string, string> { ["x-github-event"] = evnt },
                Body = "{}"
            };

            var options = Options.Create(new Config { GithubOwner = "Codertocat", GithubSigningSecret = "test_key" });
            var requestValidator = new RequestValidator(options);

            Assert.Throws(Is.InstanceOf<EventNotAllowedException>(), () => requestValidator.Validate(request));
        }

        [Test]
        public void AllowedEventsDontThrow([Values("push", "pull_request")] string @event)
        {
            var request = new ApplicationLoadBalancerRequest
            {
                HttpMethod = "POST",
                Headers = new Dictionary<string, string> { ["x-github-event"] = @event },
                Body = "{}"
            };

            var options = Options.Create(new Config { GithubOwner = "Codertocat", GithubSigningSecret = "test_key" });
            var requestValidator = new RequestValidator(options);

            Assert.Throws(Is.Not.InstanceOf<EventNotAllowedException>(), () => requestValidator.Validate(request));
        }

        [Test]
        public void PullRequestEventsThatAreNotOpenedOrSynchronizeActionsThrow()
        {
            var request = new ApplicationLoadBalancerRequest
            {
                HttpMethod = "POST",
                Headers = new Dictionary<string, string> { ["x-github-event"] = "pull_request" },
                Body = @"{
                    ""action"": ""bad""
                }"
            };

            var options = Options.Create(new Config { GithubOwner = "Codertocat", GithubSigningSecret = "test_key" });
            var requestValidator = new RequestValidator(options);

            Assert.Throws(Is.InstanceOf<ActionNotAllowedException>(), () => requestValidator.Validate(request));
        }

        [Test]
        public void PullRequestEventsThatAreOpenedOrSynchronizeActionsDDoNotThrow([Values("opened", "synchronize")] string action)
        {
            var request = new ApplicationLoadBalancerRequest
            {
                HttpMethod = "POST",
                Headers = new Dictionary<string, string> { ["x-github-event"] = "pull_request" },
                Body = $@"{{
                    ""action"": ""{action}""
                }}"
            };

            var options = Options.Create(new Config { GithubOwner = "Codertocat", GithubSigningSecret = "test_key" });
            var requestValidator = new RequestValidator(options);

            Assert.Throws(Is.Not.InstanceOf<ActionNotAllowedException>(), () => requestValidator.Validate(request));
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

            var options = Options.Create(new Config { GithubOwner = "Codertocat", GithubSigningSecret = "test_key" });
            var requestValidator = new RequestValidator(options);

            Assert.Throws(Is.InstanceOf<BodyNotJsonException>(), () => requestValidator.Validate(request));
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

            var options = Options.Create(new Config { GithubOwner = "Codertocat", GithubSigningSecret = "test_key" });
            var requestValidator = new RequestValidator(options);

            Assert.Throws(Is.Not.InstanceOf<BodyNotJsonException>(), () => requestValidator.Validate(request));
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

            var options = Options.Create(new Config { GithubOwner = "Codertocat", GithubSigningSecret = "test_key" });
            var requestValidator = new RequestValidator(options);

            Assert.Throws(Is.InstanceOf<NoContentsUrlException>(), () => requestValidator.Validate(request));
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

            var options = Options.Create(new Config { GithubOwner = "Codertocat", GithubSigningSecret = "test_key" });
            var requestValidator = new RequestValidator(options);

            Assert.Throws(Is.Not.InstanceOf<NoContentsUrlException>(), () => requestValidator.Validate(request));
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
            var options = Options.Create(new Config { GithubOwner = "Codertocat", GithubSigningSecret = "test_key" });
            var requestValidator = new RequestValidator(options);

            Assert.Throws(Is.InstanceOf<UnexpectedOwnerException>(), () => requestValidator.Validate(request));
        }

        [Test]
        public void RequestsWithOkOwnerDontThrowException()
        {
            var owner = "Codertocat";
            var request = new ApplicationLoadBalancerRequest
            {
                HttpMethod = "POST",
                Headers = new Dictionary<string, string> { ["x-github-event"] = "push" },
                Body = Serialize(new PushEvent
                {
                    Repository = new Repository
                    {
                        Owner = new User { Name = owner },
                        ContentsUrl = "https://api.github.com/repos/Codertocat/Hello-World/contents/{+path}"
                    }
                })
            };

            var options = Options.Create(new Config { GithubOwner = owner, GithubSigningSecret = "test_key" });
            var requestValidator = new RequestValidator(options);

            Assert.Throws(Is.Not.InstanceOf<UnexpectedOwnerException>(), () => requestValidator.Validate(request));
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

            var options = Options.Create(new Config { GithubSigningSecret = "test_key" });
            var requestValidator = new RequestValidator(options);

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

            var options = Options.Create(new Config { GithubSigningSecret = "test_key" });
            var requestValidator = new RequestValidator(options);

            Assert.Throws(Is.InstanceOf<InvalidSignatureException>(), () => requestValidator.Validate(request));
        }

        [Test]
        public void RequestsWithGoodSignatureDontThrow()
        {
            var request = new ApplicationLoadBalancerRequest
            {
                HttpMethod = "POST",
                Headers = new Dictionary<string, string>
                {
                    ["x-github-event"] = "push",
                    ["x-hub-signature"] = "sha1=9df818278c8fd1d5013f435a6f135f47238c71f7"
                },
                Body = Serialize(new PushEvent
                {
                    Repository = new Repository
                    {
                        ContentsUrl = "https://api.github.com/repos/Codertocat/Hello-World/contents/{+path}"
                    }
                })
            };

            var options = Options.Create(new Config { GithubSigningSecret = "test_key" });
            var requestValidator = new RequestValidator(options);

            Assert.Throws(Is.Not.InstanceOf<InvalidSignatureException>(), () => requestValidator.Validate(request));
        }
    }
}
