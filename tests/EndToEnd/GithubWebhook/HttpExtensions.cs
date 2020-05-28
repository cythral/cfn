using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.IO;
using static System.Text.Json.JsonSerializer;

namespace Cythral.CloudFormation.Tests.EndToEnd
{
    public static class HttpExtensions
    {
        public static async Task<HttpResponseMessage> PostWithSignature(this HttpClient client, string url, string key, object body, string evnt = "push")
        {
            var bodyContent = Serialize(body);
            var signature = ComputeSignature(key, bodyContent);

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Add("x-hub-signature", signature);
            request.Headers.Add("x-github-event", evnt);
            request.Content = new StringContent(bodyContent, Encoding.UTF8, "application/json");

            return await client.SendAsync(request);
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