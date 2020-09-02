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
        }

        public virtual PushEvent Validate(ApplicationLoadBalancerRequest request)
        {
            PushEvent payload;

            ValidateMethod(request);
            ValidateEvent(request);
            ValidateBodyFormat(request, out payload);
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

        private static void ValidateEvent(ApplicationLoadBalancerRequest request)
        {
            string evnt = null;
            request.Headers?.TryGetValue("x-github-event", out evnt);

            if (evnt.ToLower() != "push")
            {
                throw new EventNotAllowedException($"Event '{evnt}' not allowed");
            }
        }

        private static void ValidateBodyFormat(ApplicationLoadBalancerRequest request, out PushEvent payload)
        {
            try
            {
                payload = Deserialize<PushEvent>(request.Body);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message + "\n" + e.StackTrace);
                throw new BodyNotJsonException();
            }
        }

        private static void ValidateContentsUrlPresent(PushEvent payload)
        {
            if (payload.Repository?.ContentsUrl == null)
            {
                throw new NoContentsUrlException();
            }
        }

        private void ValidateOwner(PushEvent payload)
        {
            var expectedOwner = config.GithubOwner;

            if (expectedOwner == null)
            {
                return;
            }

            var matcher = new Regex("https:\\/\\/api\\.github\\.com\\/repos\\/([a-zA-Z0-9\\-\\._]+)\\/([a-zA-Z0-9\\-\\._]+)\\/contents\\/{\\+path}");
            var repositoryOwner = payload.Repository?.Owner?.Name;
            var contentsUrlMatches = matcher.Match(payload.Repository?.ContentsUrl).Groups[1]?.Captures;
            var contentsUrlOwner = contentsUrlMatches.Count == 1 ? contentsUrlMatches[0].Value : null;

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
            string givenSignature = null;
            string actualSignature = null;

            request.Headers.TryGetValue("x-hub-signature", out givenSignature);
            actualSignature = ComputeSignature(request.Body);

            if (givenSignature != actualSignature)
            {
                throw new InvalidSignatureException($"Signatures do not match.  Actual signature: {actualSignature}.  Given signature: {givenSignature}");
            }
        }

        private string ComputeSignature(string value)
        {
            var key = config.GithubSigningSecret;
            var valueBytes = Encoding.ASCII.GetBytes(value ?? "");
            var keyBytes = Encoding.ASCII.GetBytes(key ?? "");

            using var hasher = new HMACSHA1(keyBytes);
            var hashArray = hasher.ComputeHash(valueBytes);
            return $"sha1={string.Join("", Array.ConvertAll(hashArray, hashElement => hashElement.ToString("x2")))}";
        }
    }
}
