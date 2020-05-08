using Amazon.StepFunctions;

namespace Cythral.CloudFormation.Aws
{
    public class StepFunctionsClientFactory
    {
        public virtual IAmazonStepFunctions Create()
        {
            return new AmazonStepFunctionsClient();
        }
    }
}