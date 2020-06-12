using System.Collections.Generic;

namespace Cythral.CloudFormation.UpdateTargets
{
    public class Metric
    {
        public string MetricName { get; set; }
        public string Namespace { get; set; }
        public IEnumerable<Dimension> Dimensions { get; set; }
    }
}