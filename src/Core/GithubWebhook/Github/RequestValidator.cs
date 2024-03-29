using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using Amazon.Lambda.ApplicationLoadBalancerEvents;

using Cythral.CloudFormation.GithubWebhook.Exceptions;
using Cythral.CloudFormation.GithubWebhook.Github;

using Microsoft.Extensions.Options;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.GithubWebhook.Github
{
    public class RequestValidator
    {
        private readonly Config config;

        public RequestValidator(IOptions<Config> options)
        {
            this.config = options.Value;
        }

        internal RequestValidator()
        {
            // used for testing
            config = null!;
        }

        public virtual GithubEvent Validate(ApplicationLoadBalancerRequest request)
        {
            GithubEvent payload = null!;
            string eventType;

            ValidateMethod(request);
            ValidateEvent(request, out eventType);

            switch (eventType)
            {
                case "push":
                    {
                        ValidatePushBodyFormat(request, out var pushPayload);
                        payload = pushPayload;
                        break;
                    }

                case "pull_request":
                    {
                        ValidatePullRequestBodyFormat(request, out var pullPayload);
                        payload = pullPayload;
                        break;
                    }
            }

            ValidateContentsUrlPresent(payload);
            ValidateOwner(payload);
            ValidateSignature(request);

            return payload;
        }

        private static void ValidateMethod(ApplicationLoadBalancerRequest request)
        {
            if (request.HttpMethod.ToLower() != "post")
            {
                throw new MethodNotAllowedException($"Method '{request.HttpMethod}' not allowed");
            }
        }

        private static void ValidateEvent(ApplicationLoadBalancerRequest request, out string eventType)
        {
            var @event = "";

            request.Headers?.TryGetValue("x-github-event", out @event);
            @event = @event?.ToLower();

            if (@event != "push" && @event != "pull_request")
            {
                throw new EventNotAllowedException($"Event '{@event}' not allowed");
            }

            eventType = @event;
        }

        private static void ValidatePushBodyFormat(ApplicationLoadBalancerRequest request, out PushEvent payload)
        {
            try
            {
                payload = Deserialize<PushEvent>(request.Body) ?? throw new Exception("Body unexpectedly null.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + "\n" + e.StackTrace);
                throw new BodyNotJsonException();
            }
        }

        private static void ValidatePullRequestBodyFormat(ApplicationLoadBalancerRequest request, out PullRequestEvent payload)
        {
            try
            {
                payload = Deserialize<PullRequestEvent>(request.Body) ?? throw new Exception("Body unexpectedly null.");
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + "\n" + e.StackTrace);
                throw new BodyNotJsonException();
            }

            if (payload.Action != "opened" && payload.Action != "synchronize")
            {
                throw new ActionNotAllowedException();
            }
        }

        private static void ValidateContentsUrlPresent(GithubEvent payload)
        {
            if (string.IsNullOrEmpty(payload.Repository?.ContentsUrl))
            {
                throw new NoContentsUrlException();
            }
        }

        private static string? GetOwner(GithubEvent githubEvent)
        {
            switch (githubEvent)
            {
                case PushEvent pushEvent: return pushEvent.Repository?.Owner?.Name;
                case PullRequestEvent pullRequestEvent: return pullRequestEvent.Repository?.Owner?.Login;
            }

            return "";
        }

        private void ValidateOwner(GithubEvent payload)
        {
            var expectedOwner = config.GithubOwner;

            if (expectedOwner == null)
            {
                return;
            }

            var matcher = new Regex("https:\\/\\/api\\.github\\.com\\/repos\\/([a-zA-Z0-9\\-\\._]+)\\/([a-zA-Z0-9\\-\\._]+)\\/contents\\/{\\+path}");
            var repositoryOwner = GetOwner(payload);
            var contentsUrlMatches = matcher.Match(payload.Repository?.ContentsUrl ?? string.Empty).Groups[1]?.Captures;
            var contentsUrlOwner = contentsUrlMatches?.Count == 1 ? contentsUrlMatches[0].Value : null;

            if (repositoryOwner != expectedOwner)
            {
                throw new UnexpectedOwnerException($"Unexpected repository owner '{repositoryOwner}'");
            }

            if (contentsUrlOwner != expectedOwner)
            {
                throw new UnexpectedOwnerException($"Unexpected contents url owner: '{contentsUrlOwner}'");
            }
        }

        private void ValidateSignature(ApplicationLoadBalancerRequest request)
        {
            request.Headers.TryGetValue("x-hub-signature", out var givenSignature);
            var actualSignature = ComputeSignature(request.Body);

            if (givenSignature != actualSignature)
            {
                throw new InvalidSignatureException($"Signatures do not match.  Actual signature: {actualSignature}.  Given signature: {givenSignature}");
            }
        }

        private string ComputeSignature(string value)
        {
            var key = config.GithubSigningSecret;
            var valueBytes = Encoding.UTF8.GetBytes(value ?? "");
            var keyBytes = Encoding.UTF8.GetBytes(key ?? "");

            using var hasher = new HMACSHA1(keyBytes);
            var hashArray = hasher.ComputeHash(valueBytes);
            return $"sha1={string.Join("", Array.ConvertAll(hashArray, hashElement => hashElement.ToString("x2")))}";
        }
    }
}
