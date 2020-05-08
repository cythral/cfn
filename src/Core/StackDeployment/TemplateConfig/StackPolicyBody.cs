namespace Cythral.CloudFormation.StackDeployment
{
    public class StackPolicyBody
    {
        public string Value { get; set; }

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