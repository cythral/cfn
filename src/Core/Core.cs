using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.Json;

namespace Cythral.CloudFormation {
    public class Core {
        public static async Task Main(string[] args) {
            
            var handler = Environment.GetEnvironmentVariable("_HANDLER");

            switch(handler) {
                case "GithubWebhook": await Bootstrap(Webhook.Handle); break;
                default: break;
            }
        }

        private static async Task Bootstrap<T>(T handler) {
            var serializer = new JsonSerializer();

            using(var wrapper = HandlerWrapper.GetHandlerWrapper(handler, serializer))
            using(var bootstrap = new LambdaBootstrap(wrapper)) {
                await bootstrap.RunAsync();
            }   
        }
    }
}