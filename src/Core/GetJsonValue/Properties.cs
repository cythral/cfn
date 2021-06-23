using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Cythral.CloudFormation.GetJsonValue
{
    public class Properties
    {
        [Required]
        public string? Json { get; set; } = null;

        public string? Key { get; set; }
    }
}