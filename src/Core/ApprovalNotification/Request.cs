namespace Cythral.CloudFormation.ApprovalNotification
{
    public class Request
    {
        public string Pipeline { get; set; } = string.Empty;
        public string CustomMessage { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
    }
}