using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;

using Lambdajection.Attributes;
using Lambdajection.Encryption;

namespace Cythral.CloudFormation.GithubWebhook
{
    [LambdaOptions(typeof(Handler), "GithubWebhook")]
    public class Config
    {
        public string GithubOwner { get; set; }

        [Encrypted] public string GithubToken { get; set; }

        [Encrypted] public string GithubSigningSecret { get; set; }

        public string StatusNotificationTopicArn { get; set; }

        public string TemplateFilename { get; set; }

        public string PipelineDefinitionFilename { get; set; }

        public string ArtifactStore { get; set; }

        public string StackSuffix { get; set; }

        public string RoleArn { get; set; }
    }
}