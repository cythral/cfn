using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Amazon.Route53;
using Amazon.Route53.Model;

using Cythral.CloudFormation.CustomResource.Core;
using Cythral.CloudFormation.CustomResource.Attributes;

using Cythral.CloudFormation.Resources;

using FluentAssertions;

using NSubstitute;

using NUnit.Framework;

using RichardSzalay.MockHttp;

using static Amazon.Route53.VPCRegion;
using static Amazon.Route53.ChangeStatus;

using HostedZone = Cythral.CloudFormation.Resources.HostedZone;

namespace Tests
{

    public class HostedZoneTest
    {

        private IAmazonRoute53 CreateClient()
        {
            var client = Substitute.For<IAmazonRoute53>();

            client
            .CreateHostedZoneAsync(Arg.Any<CreateHostedZoneRequest>())
            .Returns(new CreateHostedZoneResponse
            {
                ChangeInfo = new ChangeInfo { Id = "" },
                HostedZone = new Amazon.Route53.Model.HostedZone { Id = "ABC123" }
            });

            client
            .GetChangeAsync(Arg.Any<GetChangeRequest>())
            .Returns(new GetChangeResponse
            {
                ChangeInfo = new ChangeInfo
                {
                    Status = ChangeStatus.INSYNC
                }
            });

            client
            .ChangeTagsForResourceAsync(Arg.Any<ChangeTagsForResourceRequest>())
            .Returns(new ChangeTagsForResourceResponse { });

            client
            .CreateQueryLoggingConfigAsync(Arg.Any<CreateQueryLoggingConfigRequest>())
            .Returns(new CreateQueryLoggingConfigResponse { });


            return client;
        }

        /// <summary>
        /// Tests to see if the DeletedTags property returns tags that are in 
        /// OldResourceProperties but not ResourceProperties
        /// </summary>
        [Test]
        public void DeletedTagsTest()
        {
            var oldTags = new List<Tag>() {
                new Tag {
                    Key = "Contact",
                    Value = "Talen Fisher"
                },
                new Tag {
                    Key = "Email",
                    Value = "test@test.com"
                }
            };

            var newTags = new List<Tag>() {
                new Tag {
                    Key = "Email",
                    Value = "example@test.com"
                }
            };

            var resource = new HostedZone();
            resource.Request = new Request<HostedZone.Properties>
            {
                ResourceProperties = new HostedZone.Properties
                {
                    HostedZoneTags = newTags
                },
                OldResourceProperties = new HostedZone.Properties
                {
                    HostedZoneTags = oldTags
                }
            };
            resource.DeletedTags.Should().BeEquivalentTo("Contact");
        }

        /// <summary>
        /// Tests to see if the UpsertableTags property returns tags
        /// whose values were updated in ResourceProperties or not present
        /// at all in OldResourceProperties
        /// </summary>
        [Test]
        public void UpsertableTagsTest()
        {
            var oldTags = new List<Tag> {
                new Tag {
                    Key = "Contact",
                    Value = "Talen Fisher"
                }
            };

            var newTags = new List<Tag> {
                new Tag {
                    Key = "Contact",
                    Value = "Someone else"
                },
                new Tag {
                    Key = "Email",
                    Value = "johndoe@example.com"
                }
            };

            var resource = new HostedZone();
            resource.Request = new Request<HostedZone.Properties>
            {
                ResourceProperties = new HostedZone.Properties
                {
                    HostedZoneTags = newTags,
                },
                OldResourceProperties = new HostedZone.Properties
                {
                    HostedZoneTags = oldTags
                }
            };
            resource.UpsertedTags.Should().BeEquivalentTo(newTags);
        }

        /// <summary>
        /// Tests to see if the AssociatableVPCs property returns vpcs present
        /// in ResourceProperties but not OldResourceProperties
        /// </summary>
        [Test]
        public void AssociatableVPCsTest()
        {
            var oldVpcs = new List<VPC> {
                new VPC { VPCId = "1", VPCRegion = UsEast1 }
            };

            var newVpcs = new List<VPC> {
                new VPC { VPCId = "1", VPCRegion = UsEast1 },
                new VPC { VPCId = "2", VPCRegion = UsEast1 }
            };

            var resource = new HostedZone();
            resource.Request = new Request<HostedZone.Properties>
            {
                ResourceProperties = new HostedZone.Properties
                {
                    VPCs = newVpcs,
                },
                OldResourceProperties = new HostedZone.Properties
                {
                    VPCs = oldVpcs,
                },
            };
            resource.AssociatableVPCs.Should().BeEquivalentTo(new List<VPC> {
                new VPC { VPCId = "2", VPCRegion = UsEast1 }
            });
        }

        /// <summary>
        /// Tests to see if Create calls Route53:CreateHostedZone with the correct values
        /// </summary>
        [Test]
        public async Task CreateHostedZoneIsCalled()
        {
            var mockHttp = new MockHttpMessageHandler();
            var client = CreateClient();
            var vpc = new VPC { VPCId = "ABC", VPCRegion = new VPCRegion("us-east-1") };
            var request = new Request<HostedZone.Properties>
            {
                RequestType = RequestType.Create,
                ResponseURL = "http://example.com",
                ResourceProperties = new HostedZone.Properties
                {
                    Name = "example.com.",
                    DelegationSetId = "12345",
                    VPCs = new List<VPC> { vpc }
                }
            };

            HostedZone.HttpClientProvider = new FakeHttpClientProvider(mockHttp);
            HostedZone.ClientFactory = () => client;

            await HostedZone.Handle(request.ToStream());

            await client.Received().CreateHostedZoneAsync(
                Arg.Is<CreateHostedZoneRequest>(req =>
                    req.Name == "example.com." &&
                    req.DelegationSetId == "12345" &&
                    req.VPC.VPCId == vpc.VPCId &&
                    req.VPC.VPCRegion == vpc.VPCRegion
                )
            );
        }

        /// <summary>
        /// Tests to see if Create calls route53:CreateQueryLoggingConfig with the correct values
        /// </summary>
        [Test]
        public async Task CreateHostedZoneQueryLoggingConfigTest()
        {
            var mockHttp = new MockHttpMessageHandler();
            var client = CreateClient();
            var logGroupArn = "arn:aws:logs::log-group:example.com";
            var request = new Request<HostedZone.Properties>
            {
                RequestType = RequestType.Create,
                ResourceProperties = new HostedZone.Properties
                {
                    Name = "example.com",
                    QueryLoggingConfig = new QueryLoggingConfig
                    {
                        CloudWatchLogsLogGroupArn = logGroupArn,
                    },
                }
            };

            HostedZone.HttpClientProvider = new FakeHttpClientProvider(mockHttp);
            HostedZone.ClientFactory = () => client;

            await HostedZone.Handle(request.ToStream());

            await client.Received().CreateQueryLoggingConfigAsync(
                Arg.Is<CreateQueryLoggingConfigRequest>(req =>
                    req.HostedZoneId == "ABC123" &&
                    req.CloudWatchLogsLogGroupArn == logGroupArn
                )
            );
        }

        /// <summary>
        /// Test that the PhysicalResourceId is in the response if the call to turn on Query Logging
        /// fails in HostedZone.Create.
        /// </summary>
        [Test]
        public async Task CreateShouldReturnPhysicalResourceIdIfTurningOnQueryLoggingFails()
        {
            var responseURL = "https://example.com";
            var mockHttp = new MockHttpMessageHandler();
            var client = CreateClient();

            client
            .When(x => x.CreateQueryLoggingConfigAsync(Arg.Any<CreateQueryLoggingConfigRequest>()))
            .Do(x => { throw new Exception(); });

            mockHttp
            .Expect(responseURL)
            .WithJsonPayload(new Response
            {
                Status = ResponseStatus.FAILED,
                PhysicalResourceId = "ABC123",
                Reason = "One or more errors occurred. (Exception of type \u0027System.Exception\u0027 was thrown.)"
            });

            HostedZone.ClientFactory = () => client;
            HostedZone.HttpClientProvider = new FakeHttpClientProvider(mockHttp);

            var request = new Request<HostedZone.Properties>
            {
                ResponseURL = responseURL,
                RequestType = RequestType.Create,
                ResourceProperties = new HostedZone.Properties
                {
                    Name = "example.com",
                    QueryLoggingConfig = new QueryLoggingConfig
                    {
                        CloudWatchLogsLogGroupArn = "test"
                    }
                }
            };

            await HostedZone.Handle(request.ToStream());
            mockHttp.VerifyNoOutstandingExpectation();
        }

        /// <summary>
        /// /// Test that the PhysicalResourceId is included in the response even if adding tags fails in HostedZone.Create.
        /// </summary>
        [Test]
        public async Task CreateShouldReturnPhysicalResourceIdIfAddingTagsFails()
        {
            var responseURL = "https://example.com";
            var mockHttp = new MockHttpMessageHandler();
            var client = CreateClient();

            client
            .When(x => x.ChangeTagsForResourceAsync(Arg.Any<ChangeTagsForResourceRequest>()))
            .Do(x => { throw new Exception(); });

            mockHttp
            .Expect(responseURL)
            .WithJsonPayload(new Response
            {
                Status = ResponseStatus.FAILED,
                PhysicalResourceId = "ABC123",
                Reason = "One or more errors occurred. (Exception of type \u0027System.Exception\u0027 was thrown.)"
            });

            HostedZone.ClientFactory = () => client;
            HostedZone.HttpClientProvider = new FakeHttpClientProvider(mockHttp);

            var request = new Request<HostedZone.Properties>
            {
                ResponseURL = responseURL,
                RequestType = RequestType.Create,
                ResourceProperties = new HostedZone.Properties
                {
                    Name = "example.com",
                    HostedZoneTags = new List<Tag> {
                        new Tag {
                            Key = "Contact",
                            Value = "Talen Fisher"
                        }
                    }
                }
            };

            await HostedZone.Handle(request.ToStream());
            mockHttp.VerifyNoOutstandingExpectation();
        }

        /// <summary>
        /// Test that the PhysicalResourceId is included in the response even if the call to associate vpcs fails in HostedZone.Create
        /// </summary>
        [Test]
        public async Task CreateShouldReturnPhysicalResourceIdIfAssociatingVPCsFails()
        {
            var responseURL = "https://example.com";
            var mockHttp = new MockHttpMessageHandler();
            var client = CreateClient();

            client
            .When(x => x.AssociateVPCWithHostedZoneAsync(Arg.Any<AssociateVPCWithHostedZoneRequest>()))
            .Do(x => { throw new Exception(); });

            mockHttp
            .Expect(responseURL)
            .WithJsonPayload(new Response
            {
                Status = ResponseStatus.FAILED,
                PhysicalResourceId = "ABC123",
                Reason = "One or more errors occurred. (One or more errors occurred. (Exception of type \u0027System.Exception\u0027 was thrown.))"
            });

            HostedZone.ClientFactory = () => client;
            HostedZone.HttpClientProvider = new FakeHttpClientProvider(mockHttp);

            var request = new Request<HostedZone.Properties>
            {
                ResponseURL = responseURL,
                RequestType = RequestType.Create,
                ResourceProperties = new HostedZone.Properties
                {
                    Name = "example.com",
                    VPCs = new List<VPC> {
                        new VPC {
                            VPCId = "BCD234",
                            VPCRegion = "us-east-1"
                        },
                        new VPC {
                            VPCId = "CDF345",
                            VPCRegion = "us-west-2"
                        }
                    }
                }
            };

            await HostedZone.Handle(request.ToStream());
            mockHttp.VerifyNoOutstandingExpectation();
        }


        /// <summary>
        /// Tests to see if all VPCs were associated with Route53:AssociateVPCWithHostedZone
        /// </summary>
        [Test]
        public async Task CreateVPCAssociationsTest()
        {
            var mockHttp = new MockHttpMessageHandler();
            var client = CreateClient();
            var vpcs = new List<VPC> {
                new VPC { VPCId = "1", VPCRegion = UsEast1 },
                new VPC { VPCId = "2", VPCRegion = UsEast2 },
                new VPC { VPCId = "3", VPCRegion = UsWest1 }
            };

            var request = new Request<HostedZone.Properties>
            {
                RequestType = RequestType.Create,
                ResourceProperties = new HostedZone.Properties
                {
                    Name = "example.com",
                    VPCs = vpcs,
                }
            };

            HostedZone.HttpClientProvider = new FakeHttpClientProvider(mockHttp);
            HostedZone.ClientFactory = delegate { return (IAmazonRoute53)client; };

            await HostedZone.Handle(request.ToStream());

            await client.Received().AssociateVPCWithHostedZoneAsync(
                Arg.Is<AssociateVPCWithHostedZoneRequest>(req =>
                    req.VPC.VPCId == "2" &&
                    req.VPC.VPCRegion == UsEast2
                )
            );
        }

        /// <summary>
        /// Tests to see if Create calls Route53:ChangeTagsForResource with the correct values
        /// </summary>
        [Test]
        public async Task CreateTagsTest()
        {
            var mockHttp = new MockHttpMessageHandler();
            var client = CreateClient();
            var tag = new Tag { Key = "Contact", Value = "Talen Fisher" };
            var tags = new List<Tag> { tag };
            var request = new Request<HostedZone.Properties>
            {
                RequestType = RequestType.Create,
                ResourceProperties = new HostedZone.Properties
                {
                    Name = "example.com",
                    HostedZoneTags = tags,
                }
            };

            HostedZone.HttpClientProvider = new FakeHttpClientProvider(mockHttp);
            HostedZone.ClientFactory = () => client;

            await HostedZone.Handle(request.ToStream());

            await client.Received().ChangeTagsForResourceAsync(
                Arg.Is<ChangeTagsForResourceRequest>(req =>
                    req.ResourceId == "ABC123" &&
                    req.ResourceType == "hostedzone" &&
                    req.AddTags.Any(t =>
                        t.Key == "Contact" &&
                        t.Value == "Talen Fisher"
                    )
                )
            );
        }

        [Test]
        public async Task UpdateTagsTest()
        {
            var mockHttp = new MockHttpMessageHandler();
            var client = CreateClient();

            var oldTags = new List<Tag> {
                new Tag { Key = "Contact", Value = "Talen Fisher" },
                new Tag { Key = "Phone", Value = "1112223333" }
            };

            var newTags = new List<Tag> {
                new Tag { Key = "Contact", Value = "Someone Else" },
                new Tag { Key = "Email", Value = "someone@example.com" }
            };

            var request = new Request<HostedZone.Properties>
            {
                RequestType = RequestType.Update,
                PhysicalResourceId = "ABC123",
                ResourceProperties = new HostedZone.Properties
                {
                    Name = "example.com",
                    HostedZoneTags = newTags,
                },
                OldResourceProperties = new HostedZone.Properties
                {
                    Name = "example.com",
                    HostedZoneTags = oldTags
                }
            };

            HostedZone.HttpClientProvider = new FakeHttpClientProvider(mockHttp);
            HostedZone.ClientFactory = () => client;

            await HostedZone.Handle(request.ToStream());

            await client.Received().ChangeTagsForResourceAsync(
                Arg.Is<ChangeTagsForResourceRequest>(req =>
                    req.ResourceId == "ABC123" &&
                    req.ResourceType == "hostedzone" &&
                    req.AddTags.Any(t =>
                        t.Key == "Contact" &&
                        t.Value == "Someone Else"
                    ) &&
                    req.AddTags.Any(t =>
                        t.Key == "Email" &&
                        t.Value == "someone@example.com"
                    ) &&
                    req.RemoveTagKeys.Contains("Phone")
                )
            );
        }
    }
}