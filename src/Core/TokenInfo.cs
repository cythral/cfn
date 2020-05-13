namespace Cythral.CloudFormation
{
    public class TokenInfo
    {
        public string ClientRequestToken { get; set; }
        public string QueueUrl { get; set; }
        public string ReceiptHandle { get; set; }
        public string RoleArn { get; set; }
    }
}