using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using Amazon.Lambda.ApplicationLoadBalancerEvents;

using Cythral.CloudFormation.Events;
using Cythral.CloudFormation.Exceptions;

using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.Facades
{
    public class RequestValidator
    {
        public static PushEvent Validate(
            ApplicationLoadBalancerRequest request,
            string expectedOwner = null,
            bool validateSignature = true,
            string signingKey = null
        )
        {
            PushEvent payload;

            ValidateMethod(request);
            ValidateEvent(request);
            ValidateBodyFormat(request, out payload);
            ValidateContentsUrlPresent(payload);
            ValidateRef(payload);
            ValidateOwner(payload, expectedOwner);
            ValidateSignature(request, signingKey);

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

        private static void ValidateOwner(PushEvent payload, string expectedOwner)
        {
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

        private static void ValidateSignature(ApplicationLoadBalancerRequest request, string signingKey)
        {
            string givenSignature = null;
            string actualSignature = null;

            request.Headers.TryGetValue("x-hub-signature", out givenSignature);
            actualSignature = ComputeSignature(signingKey, request.Body);

            if (givenSignature != actualSignature)
            {
                throw new InvalidSignatureException($"Signatures do not match.  Actual signature: {actualSignature}.  Given signature: {givenSignature}");
            }
        }

        private static void ValidateRef(PushEvent payload)
        {
            var actual = payload.Ref;
            var expected = $"refs/heads/{payload.Repository?.DefaultBranch}";

            if (actual != null && actual != expected)
            {
                throw new UnexpectedRefException($"Unexpected ref {actual}.  Expected: {expected}");
            }
        }

        private static string ComputeSignature(string key, string value)
        {
            byte[] valueBytes = Encoding.ASCII.GetBytes(value ?? "");
            byte[] keyBytes = Encoding.ASCII.GetBytes(key ?? "");

            using (var hasher = new HMACSHA1(keyBytes))
            {
                var hashArray = hasher.ComputeHash(valueBytes);
                return $"sha1={string.Join("", Array.ConvertAll(hashArray, hashElement => hashElement.ToString("x2")))}";
            }
        }
    }
}
