using System;
using System.Reflection;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cythral.CloudFormation.CustomResource.Attributes;

namespace Cythral.CloudFormation.CustomResource {

    public class Request<T> {

        public virtual RequestType RequestType { get; set; }

        public virtual string ResponseURL { get; set; }

        public virtual string StackId { get; set; }

        public virtual string RequestId { get; set; }

        public virtual string ResourceType { get; set; }

        public virtual string LogicalResourceId { get; set; }

        public virtual string PhysicalResourceId { get; set; }

        public virtual T ResourceProperties { get; set; }
        
        public virtual T OldResourceProperties { get; set; }

        public Stream ToStream() {
            var options = new JsonSerializerOptions();
            options.Converters.Add(new JsonStringEnumConverter());
            options.MaxDepth = 32;

            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            var value = JsonSerializer.Serialize(this, options);

            writer.Write(value);
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }

        [JsonIgnore]
        public IEnumerable<PropertyInfo> ChangedProperties {
            get {
                if(ResourceProperties == null || OldResourceProperties == null) {
                    yield break;
                }

                var currentProps = ResourceProperties.GetType().GetProperties();
                var oldProps = OldResourceProperties.GetType().GetProperties();

                foreach(var prop in currentProps) {
                    
                    object current, old;

                    try {
                        var getter = prop.GetMethod;
                        current = getter.Invoke(ResourceProperties, new object[] {});
                        old = getter.Invoke(OldResourceProperties, new object[] {});
                    } catch(Exception) {
                        continue;
                    }

                    if(current != old) {
                        yield return prop;
                    }
                }
            }
        }

        [JsonIgnore]
        public bool RequiresReplacement {
            get {
                bool PropRequiresReplacement(PropertyInfo prop) {
                    foreach(var attr in prop.CustomAttributes) {
                        if(attr.AttributeType == typeof(UpdateRequiresReplacementAttribute)) {
                            return true;
                        }
                    }

                    return false;
                }

                foreach(var prop in ChangedProperties) {
                    if(PropRequiresReplacement(prop)) {
                        return true;
                    }
                }

                return false;
            }
        }

    }
    
}