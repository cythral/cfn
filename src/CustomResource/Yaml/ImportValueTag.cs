namespace Cythral.CloudFormation.CustomResource.Yaml
{
    public class ImportValueTag
    {
        public ImportValueTag(string expression)
        {
            Expression = expression;
        }

        public string Expression { get; set; }
    }
}