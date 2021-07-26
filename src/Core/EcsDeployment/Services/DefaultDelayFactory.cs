using System.Threading;
using System.Threading.Tasks;

namespace Cythral.CloudFormation.EcsDeployment
{
    /// <inheritdoc />
    public class DefaultDelayFactory : IDelayFactory
    {
        /// <inheritdoc />
        public Task CreateDelay(int milliseconds, CancellationToken cancellationToken = default)
        {
            return Task.Delay(milliseconds, cancellationToken);
        }
    }
}