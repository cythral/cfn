using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

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

            var stream = new MemoryStream();
            var writer = new StreamWriter(stream);
            var value = JsonSerializer.Serialize(this, options);

            writer.Write(value);
            writer.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            return stream;
        }
    }
    
}