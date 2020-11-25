using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;

using Lambdajection.Attributes;
using Lambdajection.Encryption;

namespace Cythral.CloudFormation.S3Deployment
{
    [LambdaOptions(typeof(Handler), "Lambda")]
    public class Config
    {
        [Encrypted] public string GithubToken { get; set; }

        public string GithubOwner { get; set; }
    }
}