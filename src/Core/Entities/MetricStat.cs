namespace Cythral.CloudFormation.Entities
{
    public class MetricStat
    {
        public int Period { get; set; }
        public string Stat { get; set; }
        public Metric Metric { get; set; }
    }
}