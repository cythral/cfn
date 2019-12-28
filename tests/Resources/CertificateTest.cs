using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

using Amazon.CertificateManager;
using Amazon.CertificateManager.Model;
using Amazon.Lambda;
using Amazon.Lambda.Core;
using Amazon.Lambda.Model;
using Amazon.Route53;
using Amazon.Route53.Model;

using Cythral.CloudFormation.CustomResource;
using Cythral.CloudFormation.Resources;

using FluentAssertions;

using NSubstitute;

using NUnit.Framework;

using RichardSzalay.MockHttp;

using Tag = Amazon.CertificateManager.Model.Tag;
using ResourceRecord = Amazon.CertificateManager.Model.ResourceRecord;


namespace Tests
{
    public class CertificateTest
    {

        private class Context : ILambdaContext
        {
            public string AwsRequestId { get; set; }
            public IClientContext ClientContext { get; set; }
            public string FunctionName { get; set; }
            public string FunctionVersion { get; set; }
            public ICognitoIdentity Identity { get; set; }
            public string InvokedFunctionArn { get; set; }
            public ILambdaLogger Logger { get; set; }
            public string LogGroupName { get; set; }
            public string LogStreamName { get; set; }
            public int MemoryLimitInMB { get; set; }
            public TimeSpan RemainingTime { get; set; }

        }

        public IAmazonCertificateManager CreateAcmClient()
        {
            var client = Substitute.For<IAmazonCertificateManager>();

            client
            .RequestCertificateAsync(Arg.Any<RequestCertificateRequest>())
            .Returns(new RequestCertificateResponse
            {
                CertificateArn = "arn:aws:acm::1:certificate/example.com"
            });

            return client;
        }

        public IAmazonRoute53 CreateRoute53Client()
        {
            var client = Substitute.For<IAmazonRoute53>();

            client
            .ChangeResourceRecordSetsAsync(Arg.Any<ChangeResourceRecordSetsRequest>())
            .Returns(new ChangeResourceRecordSetsResponse { });

            return client;
        }

        public IAmazonLambda CreateLambdaClient()
        {
            var client = Substitute.For<IAmazonLambda>();

            client
            .InvokeAsync(Arg.Any<InvokeRequest>())
            .Returns(new InvokeResponse { });

            return client;
        }

        [Test]
        public async Task CreateCallsRequestCertificate()
        {
            var client = CreateAcmClient();
            var request = new Request<Certificate.Properties>
            {
                RequestType = RequestType.Create,
                ResourceProperties = new Certificate.Properties
                {
                    DomainName = "example.com",
                    SubjectAlternativeNames = new List<string> { "www.example.com" },
                    ValidationMethod = ValidationMethod.DNS
                }
            };

            Certificate.AcmClientFactory = () => client;
            await Certificate.Handle(request.ToStream());

            await client.Received().RequestCertificateAsync(
                Arg.Is<RequestCertificateRequest>(req =>
                    req.DomainName == "example.com" &&
                    req.SubjectAlternativeNames.Any(name => name == "www.example.com") &&
                    req.ValidationMethod == ValidationMethod.DNS
                )
            );
        }

        [Test]
        public async Task CreateAddsTags()
        {
            var acmClient = CreateAcmClient();
            var route53Client = CreateRoute53Client();

            acmClient
            .DescribeCertificateAsync(Arg.Is<DescribeCertificateRequest>(req =>
                req.CertificateArn == "arn:aws:acm::1:certificate/example.com"
            ))
            .Returns(new DescribeCertificateResponse
            {
                Certificate = new CertificateDetail
                {
                    Status = CertificateStatus.ISSUED, // don't test wait
                    DomainValidationOptions = new List<DomainValidation> {
                        new DomainValidation {
                            DomainName = "example.com",
                            ResourceRecord = new ResourceRecord {
                                Name = "_x1.example.com",
                                Type = RecordType.CNAME,
                                Value = "example-com.acm-validations.aws"
                            }
                        }
                    }
                }
            });

            var request = new Request<Certificate.Properties>
            {
                RequestType = RequestType.Create,
                ResourceProperties = new Certificate.Properties
                {
                    DomainName = "example.com",
                    HostedZoneId = "ABC123",
                    Tags = new List<Tag> {
                        new Tag {
                            Key = "Contact",
                            Value = "Talen Fisher"
                        },
                        new Tag {
                            Key = "ContactEmail",
                            Value = "talen@example.com"
                        }
                    }
                }
            };

            Certificate.WaitInterval = 0;
            Certificate.AcmClientFactory = () => acmClient;
            Certificate.Route53ClientFactory = () => route53Client;
            await Certificate.Handle(request.ToStream());

            await acmClient.Received().AddTagsToCertificateAsync(Arg.Is<AddTagsToCertificateRequest>(req =>
                req.Tags.Any(tag => tag.Key == "Contact" && tag.Value == "Talen Fisher") &&
                req.Tags.Any(tag => tag.Key == "ContactEmail" && tag.Value == "talen@example.com") &&
                req.CertificateArn == "arn:aws:acm::1:certificate/example.com"
            ));
        }

        [Test]
        public async Task CreateCallsChangeResourceRecordSets()
        {
            var acmClient = CreateAcmClient();
            var route53Client = CreateRoute53Client();

            acmClient
            .DescribeCertificateAsync(Arg.Is<DescribeCertificateRequest>(req =>
                req.CertificateArn == "arn:aws:acm::1:certificate/example.com"
            ))
            .Returns(new DescribeCertificateResponse
            {
                Certificate = new CertificateDetail
                {
                    Status = CertificateStatus.ISSUED, // don't test wait
                    DomainValidationOptions = new List<DomainValidation> {
                        new DomainValidation {
                            DomainName = "example.com",
                            ResourceRecord = new ResourceRecord {
                                Name = "_x1.example.com",
                                Type = RecordType.CNAME,
                                Value = "example-com.acm-validations.aws"
                            }
                        }
                    }
                }
            });

            var request = new Request<Certificate.Properties>
            {
                RequestType = RequestType.Create,
                ResourceProperties = new Certificate.Properties
                {
                    DomainName = "example.com",
                    HostedZoneId = "ABC123"
                }
            };

            Certificate.WaitInterval = 0;
            Certificate.AcmClientFactory = () => acmClient;
            Certificate.Route53ClientFactory = () => route53Client;
            await Certificate.Handle(request.ToStream());

            await route53Client.Received().ChangeResourceRecordSetsAsync(
                Arg.Is<ChangeResourceRecordSetsRequest>(req =>
                    req.HostedZoneId == "ABC123" &&
                    req.ChangeBatch.Changes.Any(change =>
                        change.Action == ChangeAction.UPSERT &&
                        change.ResourceRecordSet.Type == RRType.CNAME &&
                        change.ResourceRecordSet.Name == "_x1.example.com" &&
                        change.ResourceRecordSet.ResourceRecords.Any(record =>
                            record.Value == "example-com.acm-validations.aws"
                        )
                    )
                )
            );
        }

        [Test]
        public async Task WaitCallsInvokeIfStatusIsPending()
        {
            var acmClient = CreateAcmClient();
            var lambdaClient = CreateLambdaClient();

            acmClient
            .DescribeCertificateAsync(Arg.Any<DescribeCertificateRequest>())
            .Returns(new DescribeCertificateResponse
            {
                Certificate = new CertificateDetail
                {
                    Status = CertificateStatus.PENDING_VALIDATION
                }
            });

            var request = new Request<Certificate.Properties>
            {
                RequestType = RequestType.Wait,
                ResourceProperties = new Certificate.Properties
                {
                    DomainName = "example.com"
                }
            };

            Certificate.WaitInterval = 0;
            Certificate.AcmClientFactory = () => acmClient;
            Certificate.LambdaClientFactory = () => lambdaClient;

            var context = new Context { FunctionName = "Certificate" } as ILambdaContext;
            await Certificate.Handle(request.ToStream(), context);

            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());
            options.Converters.Add(new AwsConstantClassConverterFactory());

            string expectedPayload = JsonSerializer.Serialize(request, options);
            await lambdaClient.Received().InvokeAsync(
                Arg.Is<InvokeRequest>(req =>
                    req.FunctionName == "Certificate" &&
                    req.Payload == expectedPayload &&
                    req.InvocationType == InvocationType.Event
                )
            );
        }

        [Test]
        public async Task WaitRespondsWhenCertIsIssued()
        {
            var acmClient = CreateAcmClient();
            var mockHttp = new MockHttpMessageHandler();
            var request = new Request<Certificate.Properties>
            {
                RequestType = RequestType.Wait,
                ResponseURL = "http://example.com",
                PhysicalResourceId = "arn:aws:acm::1:certificate/example.com",
                ResourceProperties = new Certificate.Properties
                {
                    DomainName = "example.com"
                }
            };

            acmClient
            .DescribeCertificateAsync(Arg.Any<DescribeCertificateRequest>())
            .Returns(new DescribeCertificateResponse
            {
                Certificate = new CertificateDetail
                {
                    Status = CertificateStatus.ISSUED
                }
            });

            Certificate.HttpClientProvider = new FakeHttpClientProvider(mockHttp);
            Certificate.AcmClientFactory = () => acmClient;

            mockHttp
            .Expect("http://example.com")
            .WithJsonPayload(new Response
            {
                Status = ResponseStatus.SUCCESS,
                PhysicalResourceId = "arn:aws:acm::1:certificate/example.com"
            });

            await Certificate.Handle(request.ToStream());
            mockHttp.VerifyNoOutstandingExpectation();
        }

        [Test]
        public async Task WaitRespondsWithErrorOnFailure()
        {
            var acmClient = CreateAcmClient();
            var mockHttp = new MockHttpMessageHandler();
            var request = new Request<Certificate.Properties>
            {
                RequestType = RequestType.Wait,
                ResponseURL = "http://example.com",
                PhysicalResourceId = "arn:aws:acm::1:certificate/example.com",
                ResourceProperties = new Certificate.Properties
                {
                    DomainName = "example.com"
                }
            };

            acmClient
            .DescribeCertificateAsync(Arg.Any<DescribeCertificateRequest>())
            .Returns(new DescribeCertificateResponse
            {
                Certificate = new CertificateDetail
                {
                    Status = CertificateStatus.VALIDATION_TIMED_OUT
                }
            });

            Certificate.HttpClientProvider = new FakeHttpClientProvider(mockHttp);
            Certificate.AcmClientFactory = () => acmClient;

            mockHttp
            .Expect("http://example.com")
            .WithJsonPayload(new Response
            {
                Status = ResponseStatus.FAILED,
                PhysicalResourceId = "arn:aws:acm::1:certificate/example.com",
                Reason = "Certificate could not be issued. (Got status: VALIDATION_TIMED_OUT)"
            });

            await Certificate.Handle(request.ToStream());
            mockHttp.VerifyNoOutstandingExpectation();
        }


        [Test]
        public async Task UpdateCallsAddAndDeleteTags()
        {
            var acmClient = CreateAcmClient();
            var route53Client = CreateRoute53Client();

            acmClient
            .DescribeCertificateAsync(Arg.Is<DescribeCertificateRequest>(req =>
                req.CertificateArn == "arn:aws:acm::1:certificate/example.com"
            ))
            .Returns(new DescribeCertificateResponse
            {
                Certificate = new CertificateDetail
                {
                    Status = CertificateStatus.ISSUED, // don't test wait
                    DomainValidationOptions = new List<DomainValidation> {
                        new DomainValidation {
                            DomainName = "example.com",
                            ResourceRecord = new ResourceRecord {
                                Name = "_x1.example.com",
                                Type = RecordType.CNAME,
                                Value = "example-com.acm-validations.aws"
                            }
                        }
                    }
                }
            });

            var request = new Request<Certificate.Properties>
            {
                RequestType = RequestType.Update,
                PhysicalResourceId = "arn:aws:acm::1:certificate/example.com",
                OldResourceProperties = new Certificate.Properties
                {
                    DomainName = "example.com",
                    HostedZoneId = "ABC123",
                    Tags = new List<Tag> {
                        new Tag {
                            Key = "Contact",
                            Value = "Talen Fisher"
                        },
                        new Tag {
                            Key = "ContactEmail",
                            Value = "talen@example.com"
                        }
                    }
                },
                ResourceProperties = new Certificate.Properties
                {
                    DomainName = "example.com",
                    HostedZoneId = "ABC123",
                    Tags = new List<Tag> {
                        new Tag {
                            Key = "Contact",
                            Value = "Someone else"
                        },

                    }
                }
            };

            Certificate.WaitInterval = 0;
            Certificate.AcmClientFactory = () => acmClient;
            Certificate.Route53ClientFactory = () => route53Client;
            await Certificate.Handle(request.ToStream());

            await acmClient.Received().AddTagsToCertificateAsync(Arg.Is<AddTagsToCertificateRequest>(req =>
                req.Tags.Any(tag => tag.Key == "Contact" && tag.Value == "Someone else") &&
                req.CertificateArn == "arn:aws:acm::1:certificate/example.com"
            ));

            await acmClient.Received().RemoveTagsFromCertificateAsync(Arg.Is<RemoveTagsFromCertificateRequest>(req =>
                req.Tags.Any(tag => tag.Key == "ContactEmail" && tag.Value == "talen@example.com") &&
                req.CertificateArn == "arn:aws:acm::1:certificate/example.com"
            ));
        }

        [Test]
        public async Task UpdateCallsUpdateOptions()
        {
            var acmClient = CreateAcmClient();
            var route53Client = CreateRoute53Client();

            acmClient
            .DescribeCertificateAsync(Arg.Is<DescribeCertificateRequest>(req =>
                req.CertificateArn == "arn:aws:acm::1:certificate/example.com"
            ))
            .Returns(new DescribeCertificateResponse
            {
                Certificate = new CertificateDetail
                {
                    Status = CertificateStatus.ISSUED, // don't test wait
                    DomainValidationOptions = new List<DomainValidation>()
                }
            });

            var request = new Request<Certificate.Properties>
            {
                RequestType = RequestType.Update,
                PhysicalResourceId = "arn:aws:acm::1:certificate/example.com",
                OldResourceProperties = new Certificate.Properties
                {
                    DomainName = "example.com",
                    HostedZoneId = "ABC123"
                },
                ResourceProperties = new Certificate.Properties
                {
                    DomainName = "example.com",
                    HostedZoneId = "ABC123",
                    Options = new CertificateOptions
                    {
                        CertificateTransparencyLoggingPreference = CertificateTransparencyLoggingPreference.ENABLED
                    }
                }
            };

            Certificate.WaitInterval = 0;
            Certificate.AcmClientFactory = () => acmClient;
            Certificate.Route53ClientFactory = () => route53Client;
            await Certificate.Handle(request.ToStream());

            await acmClient.Received().UpdateCertificateOptionsAsync(Arg.Is<UpdateCertificateOptionsRequest>(req =>
                req.CertificateArn == "arn:aws:acm::1:certificate/example.com" &&
                req.Options.CertificateTransparencyLoggingPreference == CertificateTransparencyLoggingPreference.ENABLED
            ));
        }

        [Test]
        public async Task DeleteCallsDeleteCertificate()
        {
            var acmClient = CreateAcmClient();
            var route53Client = CreateRoute53Client();

            acmClient
            .DescribeCertificateAsync(Arg.Any<DescribeCertificateRequest>())
            .Returns(new DescribeCertificateResponse
            {
                Certificate = new CertificateDetail
                {
                    DomainValidationOptions = new List<DomainValidation> { }
                }
            });

            var request = new Request<Certificate.Properties>
            {
                RequestType = RequestType.Delete,
                PhysicalResourceId = "arn:aws:acm::1:certificate/example.com",
                ResourceProperties = new Certificate.Properties
                {
                    DomainName = "example.com",
                    HostedZoneId = "ABC123",
                }
            };

            Certificate.WaitInterval = 0;
            Certificate.AcmClientFactory = () => acmClient;
            Certificate.Route53ClientFactory = () => route53Client;
            await Certificate.Handle(request.ToStream());

            await acmClient.Received().DeleteCertificateAsync(Arg.Is<DeleteCertificateRequest>(req =>
                req.CertificateArn == "arn:aws:acm::1:certificate/example.com"
            ));
        }

        [Test]
        public async Task DeleteCallsChangeRecordSets()
        {
            var acmClient = CreateAcmClient();
            var route53Client = CreateRoute53Client();

            acmClient
            .DescribeCertificateAsync(Arg.Any<DescribeCertificateRequest>())
            .Returns(new DescribeCertificateResponse
            {
                Certificate = new CertificateDetail
                {
                    DomainValidationOptions = new List<DomainValidation> {
                        new DomainValidation {
                            DomainName = "example.com",
                            ResourceRecord = new ResourceRecord {
                                Name = "_x1.example.com",
                                Type = RecordType.CNAME,
                                Value = "example-com.acm-validations.aws"
                            }
                        }
                    }
                }
            });

            var request = new Request<Certificate.Properties>
            {
                RequestType = RequestType.Delete,
                PhysicalResourceId = "arn:aws:acm::1:certificate/example.com",
                ResourceProperties = new Certificate.Properties
                {
                    DomainName = "example.com",
                    HostedZoneId = "ABC123",
                }
            };

            Certificate.WaitInterval = 0;
            Certificate.AcmClientFactory = () => acmClient;
            Certificate.Route53ClientFactory = () => route53Client;
            await Certificate.Handle(request.ToStream());

            await route53Client.Received().ChangeResourceRecordSetsAsync(Arg.Is<ChangeResourceRecordSetsRequest>(req =>
                req.HostedZoneId == "ABC123" &&
                req.ChangeBatch.Changes.Any(change =>
                    change.Action == ChangeAction.DELETE &&
                    change.ResourceRecordSet.Type == RRType.CNAME &&
                    change.ResourceRecordSet.Name == "_x1.example.com" &&
                    change.ResourceRecordSet.ResourceRecords.Any(record =>
                        record.Value == "example-com.acm-validations.aws"
                    )
                )
            ));
        }
    }
}