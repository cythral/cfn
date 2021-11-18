using System;

namespace Cythral.CloudFormation.GithubWebhook.StackDeployment
{
    public class NoUpdatesException : Exception
    {
        public NoUpdatesException(string message) : base(message)
        {

        }
    }
}