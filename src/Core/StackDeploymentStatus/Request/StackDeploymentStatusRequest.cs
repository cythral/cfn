using System;

namespace Cythral.CloudFormation.StackDeploymentStatus.Request
{
    public class StackDeploymentStatusRequest
    {
        public string SourceTopic { get; set; } = "";
        public string StackId { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public string EventId { get; set; } = "";
        public string LogicalResourceId { get; set; } = "";
        public string PhysicalResourceId { get; set; } = "";
        public string Namespace { get; set; } = "";
        public string PrincipalId { get; set; } = "";
        public object? ResourceProperties { get; set; } = new { };
        public string ResourceStatus { get; set; } = "";
        public string ResourceType { get; set; } = "";
        public string StackName { get; set; } = "";
        public string ClientRequestToken { get; set; } = "";

    }
}