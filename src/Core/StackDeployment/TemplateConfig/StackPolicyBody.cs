namespace Cythral.CloudFormation.StackDeployment
{
    public class StackPolicyBody
    {
        public string Value { get; set; } = string.Empty;

        public override string ToString()
        {
            return Value;
        }

        public static StackPolicyBody operator +(StackPolicyBody body, string value)
        {
            body.Value += value;
            return body;
        }
    }
}