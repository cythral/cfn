using System.Net.Http;
using System.Net.Http.Json;

namespace Cythral.CloudFormation.GithubWebhook.Tests
{
    public static class HttpRequestMessageExtensions
    {
        public static T Json<T>(this HttpRequestMessage message)
        {
            var jsonContent = (JsonContent)message.Content;
            return (T)jsonContent.Value;
        }
    }
}