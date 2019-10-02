using System;

namespace Cythral.CloudFormation.CustomResource {

    public class Response {

        public ResponseStatus Status { get; set; } = ResponseStatus.SUCCESS;

        public string PhysicalResourceId { get; set; }

        public string StackId { get; set; }

        public string LogicalResourceId { get; set; }

        public string RequestId { get; set; }

        public string Reason { get; set; } = "";
        
        public object Data { get; set; }
        
    }
    
}