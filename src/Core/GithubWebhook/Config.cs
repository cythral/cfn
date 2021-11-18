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
        public string GithubOwner { get; set; } = string.Empty;

        [Encrypted] public string GithubToken { get; set; } = string.Empty;

        [Encrypted] public string GithubSigningSecret { get; set; } = string.Empty;

        public string StatusNotificationTopicArn { get; set; } = string.Empty;

        public string TemplateFilename { get; set; } = string.Empty;

        public string PipelineDefinitionFilename { get; set; } = string.Empty;

        public string ArtifactStore { get; set; } = string.Empty;

        public string StackSuffix { get; set; } = string.Empty;

        public string RoleArn { get; set; } = string.Empty;
    }
}