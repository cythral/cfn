using Amazon.StepFunctions;

namespace Cythral.CloudFormation.StackDeploymentStatus
{
    public class StepFunctionsClientFactory
    {
        public virtual IAmazonStepFunctions Create()
        {
            return new AmazonStepFunctionsClient();
        }
    }
}