using System;
using System.Threading.Tasks;
using Amazon.Route53;
using Amazon.Route53.Model;
using Cythral.CloudFormation.CustomResource;

namespace Cythral.CloudFormation.Resources {

    [CustomResourceAttribute(typeof(CreateHostedZoneRequest))]
    partial class HostedZone {
        
        public async Task<Response> Create() {
            var client = new AmazonRoute53Client();
            var payload = Request.ResourceProperties;
            payload.CallerReference = DateTime.Now.ToString();

            var result = await client.CreateHostedZoneAsync(payload);

            return new Response {
                PhysicalResourceId = result.HostedZone.Id,
                Data = result
            };
        }

        public async Task<Response> Update() {
            throw new NotImplementedException("Updates are not yet supported");
        }

        public async Task<Response> Delete() {
            var client = new AmazonRoute53Client();
            var request = new DeleteHostedZoneRequest() { Id = Request.PhysicalResourceId };
            var result = await client.DeleteHostedZoneAsync(request);
            
            return new Response {
                Data = result
            };
        }

        public static void Main(string[] args) {}
    }
}
