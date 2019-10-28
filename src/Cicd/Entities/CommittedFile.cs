using System.Net;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Cythral.CloudFormation.Cicd.Entities {
    public class CommittedFile {
        public string Contents { get; private set; }
        public bool Exists { get; private set; } = true;
        public static Func<HttpClient> DefaultHttpClientFactory { get; set; } = () => new HttpClient();

        public static async Task<CommittedFile> FromContentsUrl(string contentsUrl, string filename, Config config, Func<HttpClient> httpClientFactory = null) {
            httpClientFactory = httpClientFactory ?? DefaultHttpClientFactory;
            
            var httpClient = httpClientFactory();
            var baseUrl = contentsUrl.Replace("{+path}", "").TrimEnd(new char[] { '/' });
            var request = new HttpRequestMessage { RequestUri = new Uri(baseUrl + "/" + filename) };
            request.Headers.Authorization = new AuthenticationHeaderValue("token", config["GITHUB_TOKEN"]);

            var result = await httpClient.SendAsync(request);
            if(result.StatusCode != HttpStatusCode.OK) {
                return null;
            }

            var body = await result.Content.ReadAsStringAsync();
            return new CommittedFile { Contents = body };
        }

        public override string ToString() => Contents;
        public static implicit operator string(CommittedFile file) => file.Contents;        
    }
}