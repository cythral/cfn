namespace Cythral.CloudFormation.Entities
{
    public class MetricDataQuery
    {
        public string Id { get; set; }
        public string Expression { get; set; }
        public bool ReturnData { get; set; }
        public MetricStat MetricStat { get; set; }
    }
}