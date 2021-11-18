namespace Cythral.CloudFormation.ExtractFileFromZip
{
    public class Request
    {
        public string ZipLocation { get; set; } = string.Empty;
        public string Filename { get; set; } = string.Empty;
    }
}