using System;
namespace Cythral.CloudFormation.StackDeployment
{
    public class NoUpdatesException : Exception
    {
        public NoUpdatesException(string message) : base(message)
        {

        }
    }
}