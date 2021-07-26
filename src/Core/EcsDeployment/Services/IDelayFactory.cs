using System.Threading;
using System.Threading.Tasks;

namespace Cythral.CloudFormation.EcsDeployment
{
    public interface IDelayFactory
    {
        /// <summary>
        /// Creates a task that completes after a delay.
        /// </summary>
        /// <param name="milliseconds">The number of milliseconds to delay for.</param>
        /// <param name="cancellationToken">Token used to cancel the delay.</param>
        /// <returns>The resulting task.</returns>
        Task CreateDelay(int milliseconds, CancellationToken cancellationToken = default);
    }
}