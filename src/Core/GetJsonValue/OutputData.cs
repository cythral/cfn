using System;

using Lambdajection.CustomResource;

namespace Cythral.CloudFormation.GetJsonValue
{
    public class OutputData : ICustomResourceOutputData
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public object? Result { get; set; }
    }
}