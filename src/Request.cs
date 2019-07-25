using System;

namespace Cythral.CloudFormation.CustomResource {
    public class Request<T> {
        public RequestType RequestType { get; set; }
        public string ResponseURL { get; set; }
        public string StackId { get; set; }
        public string RequestId { get; set; }
        public string ResourceType { get; set; }
        public string LogicalResourceId { get; set; }
        public T ResourceProperties { get; set; }
    }
}