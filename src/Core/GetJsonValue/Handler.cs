using System;
using System.Threading;
using System.Threading.Tasks;

using Lambdajection.Attributes;
using Lambdajection.CustomResource;

namespace Cythral.CloudFormation.GetJsonValue
{
    [CustomResourceProvider(typeof(Startup))]
    public partial class Handler
    {
        public Task<OutputData> Create(CustomResourceRequest<Properties> request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var props = request.ResourceProperties;

            object? result = null;
            props?.Json?.TryGetValue(props?.Key ?? string.Empty, out result);
            return Task.FromResult(new OutputData { Id = request.PhysicalResourceId ?? Guid.NewGuid().ToString(), Result = result });
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
