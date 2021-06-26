using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using Lambdajection.Attributes;
using Lambdajection.CustomResource;

using Microsoft.Extensions.Logging;

namespace Cythral.CloudFormation.GetJsonValue
{
    [CustomResourceProvider(typeof(Startup))]
    public partial class Handler
    {
        private readonly ILogger<Handler> logger;

        public Handler(
            ILogger<Handler> logger
        )
        {
            this.logger = logger;
        }

        public Task<OutputData> Create(CustomResourceRequest<Properties> request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var props = request.ResourceProperties;
            logger.LogInformation("Received properties: {@props}", props);

            var json = JsonSerializer.Deserialize<Dictionary<string, object>>(props!.Json!);
            logger.LogInformation("Deserialized JSON: {@json}", json);

            object? result = null;
            json?.TryGetValue(props?.Key ?? string.Empty, out result);
            logger.LogInformation("Found value: {@value}", result);

            var id = string.IsNullOrEmpty(request.PhysicalResourceId) ? Guid.NewGuid().ToString() : request.PhysicalResourceId;
            return Task.FromResult(new OutputData { Id = id, Result = result });
        }

        public Task<OutputData> Update(CustomResourceRequest<Properties> request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Create(request, cancellationToken);
        }

        public Task<OutputData> Delete(CustomResourceRequest<Properties> request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Create(request, cancellationToken);
        }
    }
}
