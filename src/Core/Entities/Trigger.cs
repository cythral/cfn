using System;
using System.Collections;
using System.Collections.Generic;

namespace Cythral.CloudFormation.Entities {
    public class Trigger {
        public int Period { get; set; }
        public int EvaluationPeriods { get; set; }
        public string ComparisonOperator { get; set; }
        public double Threshold { get; set; }
        public string TreatMissingData { get; set; }
        public string EvaluateLowSampleCountPercentile { get; set; }
        public IEnumerable<MetricDataQuery> Metrics { get; set; }
    }
}