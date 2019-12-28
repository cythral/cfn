using System;
using System.Reflection;

using Cythral.CloudFormation.CustomResource;
using Cythral.CloudFormation.CustomResource.Attributes;

using NUnit.Framework;

namespace Tests {
    public class RequestTest {

        public class RequestProperties {
            [UpdateRequiresReplacement]
            public string Message { get; set; }
        }

        [Test]
        public void ChangedPropertiesTest() {
            var request = new Request<RequestProperties> {
                ResourceProperties = new RequestProperties {
                    Message = "A"
                },
                OldResourceProperties = new RequestProperties { 
                    Message = "B"
                }
            };

            var messageProperty = typeof(RequestProperties).GetProperty("Message");
            Assert.That(request.ChangedProperties, Has.Member(messageProperty));
        }

        [Test]
        public void UnchangedPropertiesTest() {
            var request = new Request<RequestProperties> {
                ResourceProperties = new RequestProperties {
                    Message = "A"
                },
                OldResourceProperties = new RequestProperties {
                    Message = "A"
                }
            };

            var messageProperty = typeof(RequestProperties).GetProperty("Message");
            Assert.That(request.ChangedProperties, Has.No.Member(messageProperty));
        }

        [Test]
        public void NewPropertiesTest() {
            var request = new Request<RequestProperties> {
                ResourceProperties = new RequestProperties {
                    Message = "A"
                }, 
                OldResourceProperties = new RequestProperties {

                }
            };

            var messageProperty = typeof(RequestProperties).GetProperty("Message");
            Assert.That(request.ChangedProperties, Has.Member(messageProperty));
        }

        [Test]
        public void RequiresReplacementTest() {
            var request = new Request<RequestProperties> {
                ResourceProperties = new RequestProperties {
                    Message = "A"
                },
                OldResourceProperties = new RequestProperties {
                    Message = "B"
                }
            };

            Assert.True(request.RequiresReplacement);
        }

        [Test]
        public void NoReplacementTest() {
            var request = new Request<RequestProperties> {
                ResourceProperties = new RequestProperties {
                    Message = "A"
                },
                OldResourceProperties = new RequestProperties {
                    Message = "A"
                }
            };

            Assert.False(request.RequiresReplacement);
        }
    }
}