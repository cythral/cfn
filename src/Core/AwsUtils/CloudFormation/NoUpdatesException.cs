using System;
namespace Cythral.CloudFormation.AwsUtils.CloudFormation
{
    public class NoUpdatesException : Exception
    {
        public NoUpdatesException(string message) : base(message)
        {

        }
    }
}