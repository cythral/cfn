using System;
using System.Threading.Tasks;
using System.IO;

namespace Cythral.CloudFormation.Tests.EndToEnd
{
    public static class StreamExtensions
    {
        public static async Task<string> ReadAsString(this Stream stream)
        {
            stream.Seek(0, SeekOrigin.Begin);

            using (var reader = new StreamReader(stream))
            {
                return await reader.ReadToEndAsync();
            }
        }
    }
}