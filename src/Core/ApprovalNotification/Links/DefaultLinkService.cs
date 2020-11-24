using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Cythral.CloudFormation.ApprovalNotification.Links
{
    public class DefaultLinkService : ILinkService
    {
        private readonly HttpClient httpClient;

        public DefaultLinkService(HttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        public async Task<string> Shorten(string url)
        {
            var uri = new Uri("", UriKind.Relative);
            var response = await httpClient.PostAsJsonAsync(uri, new {
                Destination = url,
            });

            return $"https://cythr.al{response.Headers.Location.ToString()}";
       }
    }
}