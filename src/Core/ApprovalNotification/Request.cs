namespace Cythral.CloudFormation.ApprovalNotification
{
    public class Request
    {
        public string Pipeline { get; set; }
        public string CustomMessage { get; set; }
        public string Token { get; set; }
    }
}