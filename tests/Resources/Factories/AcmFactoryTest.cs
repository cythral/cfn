using System.Threading.Tasks;

using NUnit.Framework;

namespace Cythral.CloudFormation.Resources.Factories.Tests
{
    public class AcmFactoryTest
    {
        [Test]
        public async Task Create_DoesNotReturnNull()
        {
            var factory = new AcmFactory();
            var result = await factory.Create();

            Assert.That(result, Is.Not.EqualTo(null));
        }
    }
}