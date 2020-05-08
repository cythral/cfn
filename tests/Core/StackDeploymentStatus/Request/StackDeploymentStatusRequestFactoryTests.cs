using System;
using System.Collections.Generic;
using static System.Text.Json.JsonSerializer;

using Amazon.ElasticLoadBalancingV2;
using Amazon.ElasticLoadBalancingV2.Model;
using Amazon.Lambda.SNSEvents;

using Cythral.CloudFormation.Entities;
using Cythral.CloudFormation.Events;
using Cythral.CloudFormation.StackDeploymentStatus.Request;
using Cythral.CloudFormation.UpdateTargets.DnsResolver;
using Cythral.CloudFormation.UpdateTargets.Request;

using NSubstitute;

using NUnit.Framework;

using SNSRecord = Amazon.Lambda.SNSEvents.SNSEvent.SNSRecord;
using SNSMessage = Amazon.Lambda.SNSEvents.SNSEvent.SNSMessage;

namespace Cythral.CloudFormation.Tests.StackDeploymentStatus.Request
{
    public class StackDeploymentStatusRequestFactoryTests
    {
        [Test]
        public void FromSnsEventReturnsRequest()
        {
            var stackId = "stackId";
            var stackName = "stackName";
            var eventId = "eventId";
            var token = "token";
            var timestamp = DateTime.Now;
            var logicalResourceId = "logicalResourceId";
            var physicalResourceId = "physicalResourceId";
            var namespac = "namespace";
            var principalId = "principalId";
            var resourceProperties = new { A = "B" };
            var resourceStatus = "status";
            var resourceType = "type";

            var message = @$"
                StackId='{stackId}'
                StackName={stackName}'
                ClientRequestToken='{token}'
                EventId='{eventId}'
                Timestamp='{timestamp.ToString("MM/dd/yyyy hh:mm:ss.fffffff tt")}'
                LogicalResourceId='{logicalResourceId}'
                PhysicalResourceId='{physicalResourceId}'
                Namespace='{namespac}'
                PrincipalId='{principalId}'
                ResourceProperties='{Serialize(resourceProperties)}'
                ResourceStatus='{resourceStatus}'
                ResourceType='{resourceType}'
            ";


            var evnt = new SNSEvent
            {
                Records = new List<SNSRecord> {
                    new SNSRecord {
                        Sns = new SNSMessage {
                            Message = message
                        }
                    }
                }
            };

            var factory = new StackDeploymentStatusRequestFactory();
            var request = factory.CreateFromSnsEvent(evnt);

            Assert.That(request.StackId, Is.EqualTo(stackId));
            Assert.That(request.StackName, Is.EqualTo(stackName));
            Assert.That(request.EventId, Is.EqualTo(eventId));
            Assert.That(request.ClientRequestToken, Is.EqualTo(token));
            Assert.That(request.Timestamp, Is.EqualTo(timestamp));
            Assert.That(request.LogicalResourceId, Is.EqualTo(logicalResourceId));
            Assert.That(request.PhysicalResourceId, Is.EqualTo(physicalResourceId));
            Assert.That(request.Namespace, Is.EqualTo(namespac));
            Assert.That(request.PrincipalId, Is.EqualTo(principalId));
            Assert.That(Serialize(request.ResourceProperties), Is.EqualTo(Serialize(resourceProperties)));
            Assert.That(request.ResourceStatus, Is.EqualTo(resourceStatus));
            Assert.That(request.ResourceType, Is.EqualTo(resourceType));
        }
    }
}