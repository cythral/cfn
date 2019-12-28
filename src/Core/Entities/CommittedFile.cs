using System.Net;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;

using static System.Net.Http.HttpMethod;

namespace Cythral.CloudFormation.Entities
{
    public class CommittedFile
    {
        public string Contents { get; private set; }
        public bool Exists { get; private set; } = true;
        public static Func<HttpClient> DefaultHttpClientFactory { get; set; } = () => new HttpClient();

        public static async Task<CommittedFile> FromContentsUrl(
            string contentsUrl,
            string filename,
            Config config,
            string gitRef = null,
            Func<HttpClient> httpClientFactory = null
        )
        {
            httpClientFactory = httpClientFactory ?? DefaultHttpClientFactory;

            var httpClient = httpClientFactory();
            var baseUrl = contentsUrl.Replace("{+path}", "").TrimEnd(new char[] { '/' });
            var uri = gitRef == null ? $"{baseUrl}/{filename}" : $"{baseUrl}/{filename}?ref={gitRef}";
            var request = new HttpRequestMessage { Method = Get, RequestUri = new Uri(uri) };

            request.Headers.UserAgent.Add(new ProductInfoHeaderValue("brighid", "v1"));
            request.Headers.Authorization = new AuthenticationHeaderValue("token", config["GITHUB_TOKEN"]);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.VERSION.raw"));

            var result = await httpClient.SendAsync(request);
            var body = await result.Content?.ReadAsStringAsync();

            if (result.StatusCode != HttpStatusCode.OK)
            {
                Console.WriteLine($"Unexpected response code {(int)result.StatusCode}: {body}");
                return null;
            }


            return new CommittedFile { Contents = body };
        }

        public override string ToString() => Contents;
        public static implicit operator string(CommittedFile file) => file.Contents;
    }
}