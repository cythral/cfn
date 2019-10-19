using System;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.ComponentModel.DataAnnotations;
using Amazon.Route53;
using Amazon.Route53.Model;
using Amazon.CertificateManager;
using Amazon.CertificateManager.Model;
using Amazon.Lambda;
using Amazon.Lambda.Model;
using Cythral.CloudFormation.CustomResource;
using Cythral.CloudFormation.CustomResource.Attributes;

using static Amazon.Route53.ChangeStatus;

using Tag = Amazon.CertificateManager.Model.Tag;
using ResourceRecord = Amazon.Route53.Model.ResourceRecord;

namespace Cythral.CloudFormation.Resources {

    /// <summary>
    /// Certificate Custom Resource supporting automatic dns validation
    /// </summary>
    [CustomResource(typeof(Certificate.Properties))]
    public partial class Certificate {

        public class Properties {
            [UpdateRequiresReplacement]
            [Required]
            public string DomainName { get; set; }

            [UpdateRequiresReplacement]
            public List<DomainValidationOption> DomainValidationOptions { get; set; }

            [UpdateRequiresReplacement]
            public List<string> SubjectAlternativeNames { get; set; }

            public List<Tag> Tags { get; set; }

            public ValidationMethod ValidationMethod { get; set; } = ValidationMethod.DNS;

            [UpdateRequiresReplacement]
            public string HostedZoneId { get; set; }

            public CertificateOptions Options { get; set; }

            [UpdateRequiresReplacement]
            public string CertificateAuthorityArn { get; set; }
        }

        public static int WaitInterval { get; set; } = 30;

        public static Func<IAmazonCertificateManager> AcmClientFactory { get; set; } = delegate { return (IAmazonCertificateManager) new AmazonCertificateManagerClient(); };

        public static Func<IAmazonRoute53> Route53ClientFactory { get; set; } = delegate { return (IAmazonRoute53) new AmazonRoute53Client(); };
        
        public static Func<IAmazonLambda> LambdaClientFactory { get; set; } = delegate { return (IAmazonLambda) new AmazonLambdaClient(); };

        public async Task<Response> Create() {
            var acmClient = AcmClientFactory();
            var route53Client = Route53ClientFactory();
            var props = Request.ResourceProperties;
            var request = new RequestCertificateRequest { 
                DomainName = props.DomainName,
                ValidationMethod = props.ValidationMethod
            };

            if(props.CertificateAuthorityArn    != null)   request.CertificateAuthorityArn  = props.CertificateAuthorityArn;
            if(props.DomainValidationOptions    != null)   request.DomainValidationOptions  = props.DomainValidationOptions;
            if(props.Options                    != null)   request.Options                  = props.Options;
            if(props.SubjectAlternativeNames    != null)   request.SubjectAlternativeNames  = props.SubjectAlternativeNames;

            var requestCertificateResponse = await acmClient.RequestCertificateAsync(request);
            Console.WriteLine($"Got Request Certificate Response: {JsonSerializer.Serialize(requestCertificateResponse)}");

            Request.PhysicalResourceId = requestCertificateResponse.CertificateArn;

            var describeCertificateResponse = await acmClient.DescribeCertificateAsync(new DescribeCertificateRequest {
                CertificateArn = Request.PhysicalResourceId,
            });
            Console.WriteLine($"Got Describe Certificate Response: {JsonSerializer.Serialize(describeCertificateResponse)}");

            var changes = new List<Change>();
            foreach(var option in describeCertificateResponse.Certificate.DomainValidationOptions) {
                changes.Add(new Change {
                    Action = ChangeAction.UPSERT,
                    ResourceRecordSet = new ResourceRecordSet {
                        Name = option.ResourceRecord.Name,
                        Type = new RRType(option.ResourceRecord.Type.Value),
                        TTL = 60,
                        ResourceRecords = new List<ResourceRecord> {
                            new ResourceRecord { Value = option.ResourceRecord.Value }
                        }
                    }
                });
            }

            var changeRecordsResponse = await route53Client.ChangeResourceRecordSetsAsync(new ChangeResourceRecordSetsRequest {
                HostedZoneId = props.HostedZoneId,
                ChangeBatch = new ChangeBatch {
                    Changes = changes
                }
            });
            Console.WriteLine($"Got Change Record Sets Response: {JsonSerializer.Serialize(changeRecordsResponse)}");
            
            Request.RequestType = RequestType.Wait;
            return await Wait();
        }

        public async Task<Response> Wait() {
            var request = new DescribeCertificateRequest { CertificateArn = Request.PhysicalResourceId };
            var response = await AcmClientFactory().DescribeCertificateAsync(request);
            var status = response.Certificate.Status.Value;
            
            switch(status) {
                case "PENDING_VALIDATION":
                    Thread.Sleep(WaitInterval * 1000);

                    var invokeResponse = await LambdaClientFactory().InvokeAsync(new InvokeRequest {
                        FunctionName = Context.FunctionName,
                        Payload = JsonSerializer.Serialize(Request),
                        InvocationType = InvocationType.Event,
                    });

                    Console.WriteLine($"Got Lambda Invoke Response: {JsonSerializer.Serialize(invokeResponse)}");
                    break;

                case "ISSUED":
                    return new Response { PhysicalResourceId = Request.PhysicalResourceId };

                default:
                    throw new Exception($"Certificate could not be issued. (Got status: {status})");
            }

            return null;
        }


        public async Task<Response> Update() {
            return new Response {

            };
        }

        public async Task<Response> Delete() {
            return new Response {

            };
        }
    }
}