using System;
using System.Threading.Tasks;
using Amazon;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.Json;
using Amazon.Lambda.ApplicationLoadBalancerEvents;

namespace Cythral.CloudFormation {
    public class Core {
        public static async Task Main(string[] args) {
            var serializer = new JsonSerializer();
            var handler = Environment.GetEnvironmentVariable("_HANDLER");

            switch(handler) {
                case "GithubWebhook":
                    Func<ApplicationLoadBalancerRequest, ILambdaContext, Task<ApplicationLoadBalancerResponse>> func = Webhook.Handle;
                    using(var wrapper = HandlerWrapper.GetHandlerWrapper(func, serializer))
                    using(var bootstrap = new LambdaBootstrap(wrapper)) {
                        await bootstrap.RunAsync();
                    }

                    break;
                
                default: break;
            }   
        }
    }
}