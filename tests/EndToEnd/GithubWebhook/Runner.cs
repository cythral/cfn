using System;
using System.Threading.Tasks;
using NUnitLite;
using Cythral.CloudFormation.CustomResource.Attributes;
using Cythral.CloudFormation.CustomResource.Core;

namespace Cythral.CloudFormation.Tests.EndToEnd.GithubWebhook
{
    [CustomResource]
    public partial class Runner
    {
        public class Properties
        {
            public string Serial { get; set; }
        }

        public Task<Response> Create()
        {
            if (new AutoRun().Execute(new string[] { }) != 0)
            {
                throw new Exception("End to end tests failed.");
            }

            return Task.FromResult(new Response { });
        }

        public Task<Response> Update()
        {
            return Create();
        }

        public Task<Response> Delete()
        {
            return Task.FromResult(new Response { });
        }
    }
}