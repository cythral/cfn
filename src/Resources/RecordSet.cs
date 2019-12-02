using System;
using System.Linq;
using System.Text.Json;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.Route53;
using Amazon.Route53.Model;
using Cythral.CloudFormation.CustomResource;
using Cythral.CloudFormation.CustomResource.Yaml;
using Cythral.CloudFormation.CustomResource.Attributes;

using static Cythral.CloudFormation.CustomResource.GranteeType;

namespace Cythral.CloudFormation.Resources {

    public class Record {
        public string HostedZoneId { get; set; }
        public string Comment { get; set; }
        public AliasTarget AliasTarget { get; set; }
        public ResourceRecordSetFailover Failover { get; set; }
        public GeoLocation GeoLocation { get; set; }
        public string HealthCheckId { get; set; }
        public bool? MultiValueAnswer { get; set; }
        public string Name { get; set; }
        public ResourceRecordSetRegion Region { get; set; }
        public List<string> ResourceRecords { get; set; }
        public string SetIdentifier { get; set; }
        public string TrafficPolicyInstanceId { get; set; }
        public string TTL { get; set; }
        public RRType Type { get; set; }
        public string Weight { get; set; }

        public ResourceRecordSet ToResourceRecordSet() {
            var resourceRecords = (from record in ResourceRecords select new ResourceRecord { Value = record }).ToList();

            var set = new ResourceRecordSet {
                AliasTarget = AliasTarget,
                Failover = Failover,
                GeoLocation = GeoLocation,
                HealthCheckId = HealthCheckId,
                Name = Name,
                Region = Region,
                Type = Type,
                SetIdentifier = SetIdentifier,
                TrafficPolicyInstanceId = TrafficPolicyInstanceId,
                ResourceRecords = resourceRecords,
            };

            if(TTL != null)                 set.TTL = Int64.Parse(TTL);
            if(Weight != null)              set.Weight = Int64.Parse(Weight);
            if(MultiValueAnswer != null)    set.MultiValueAnswer = (bool) MultiValueAnswer;

            return set;
        }
    }

    [CustomResource(
        ResourcePropertiesType = typeof(Record), 
        Grantees = new string[] { "cfn-metadata:DevAccountId", "cfn-metadata:ProdAccountId" },
        GranteeType = Import
    )]
    partial class RecordSet {
        
        private AmazonRoute53Client client = new AmazonRoute53Client();

        public async Task<Response> Create() {
            Console.WriteLine(JsonSerializer.Serialize(Request));

            var hostedZoneId = Request.ResourceProperties.HostedZoneId;
            var payload = new ChangeResourceRecordSetsRequest {
                HostedZoneId = hostedZoneId,
                ChangeBatch = new ChangeBatch {
                    Changes = new List<Change> {
                        new Change {
                            Action = "UPSERT",
                            ResourceRecordSet = Request.ResourceProperties.ToResourceRecordSet()
                        }
                    }
                }
            };

            await client.ChangeResourceRecordSetsAsync(payload);
            return new Response {
                PhysicalResourceId = Request.ResourceProperties.Name
            };
        }

        public async Task<Response> Update() {
            return await Create();
        }

        public Task<Response> Delete() {
            return Task.FromResult(new Response());
        }
    }
}