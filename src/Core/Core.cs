using System;

namespace Cythral.CloudFormation {
    public class Core {
        public static async Task Main(string[] args) {
            var serializer = new Amazon.Lambda.Serialization.Json.JsonSerializer();
            var handler = Environment.GetEnvironmentVariable("_HANDLER");

            switch(handler) {
                case "GithubWebhook": await Bootstrap(Webhook.Handle); break;
                default: break;
            }
        }

        private static async Task Bootstrap<T>(T handler) {
            using(var wrapper = HandlerWrapper.GetHandlerWrapper(func, serializer))
            using(var bootstrap = new LambdaBootstrap(wrapper)) {
                await bootstrap.RunAsync();
            }   
        }
    }
}