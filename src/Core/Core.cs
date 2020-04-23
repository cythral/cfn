using System;
using System.Threading.Tasks;

using Amazon.Lambda.ApplicationLoadBalancerEvents;
using Amazon.Lambda.Core;
using Amazon.Lambda.RuntimeSupport;
using Amazon.Lambda.Serialization.Json;
using Amazon.Lambda.SNSEvents;

using Cythral.CloudFormation.Handlers;

[assembly:LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace Cythral.CloudFormation
{
    public class Core
    {
        public static async Task Main(string[] args)
        {
            var serializer = new JsonSerializer();
            var handler = Environment.GetEnvironmentVariable("_HANDLER");

            switch (handler)
            {
                case "GithubWebhook":
                    Func<ApplicationLoadBalancerRequest, ILambdaContext, Task<ApplicationLoadBalancerResponse>> webhookHandler = GithubWebhookHandler.Handle;
                    using (var wrapper = HandlerWrapper.GetHandlerWrapper(webhookHandler, serializer))
                    using (var bootstrap = new LambdaBootstrap(wrapper))
                    {
                        await bootstrap.RunAsync();
                    }

                    break;

                case "UpdateTargets":
                    Func<SNSEvent, ILambdaContext, Task<UpdateTargets.Response>> targetsHandler = UpdateTargets.Handler.Handle;
                    using (var wrapper = HandlerWrapper.GetHandlerWrapper(targetsHandler, serializer))
                    using (var bootstrap = new LambdaBootstrap(wrapper))
                    {
                        await bootstrap.RunAsync();
                    }

                    break;

                default: break;
            }
        }
    }
}