using System.Threading.Tasks;

using NUnit.Framework;

namespace Cythral.CloudFormation.Resources.Factories.Tests
{
    public class StsFactoryTest
    {
        [Test]
        public async Task Create_DoesNotReturnNull()
        {
            var factory = new StsFactory();
            var result = await factory.Create();

            Assert.That(result, Is.Not.EqualTo(null));
        }
    }
}