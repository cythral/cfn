using System.IO;
namespace Cythral.CloudFormation.S3Deployment
{
    public class TestZipFile
    {
        // zip containing two files:
        // README.txt - hi
        // LICENSE.txt - test
        private static byte[] bytes = new byte[] {
            0x50,0x4B,0x03,0x04,0x0A,0x00,0x00,0x00,
            0x00,0x00,0x8F,0x3C,0xAE,0x50,0x7A,0x7A,
            0x6F,0xED,0x03,0x00,0x00,0x00,0x03,0x00,
            0x00,0x00,0x0A,0x00,0x1C,0x00,0x52,0x45,
            0x41,0x44,0x4D,0x45,0x2E,0x74,0x78,0x74,
            0x55,0x54,0x09,0x00,0x03,0x4E,0x3B,0xBD,
            0x5E,0x4E,0x3B,0xBD,0x5E,0x75,0x78,0x0B,
            0x00,0x01,0x04,0xF6,0x01,0x00,0x00,0x04,
            0x14,0x00,0x00,0x00,0x68,0x69,0x0A,0x50,
            0x4B,0x03,0x04,0x0A,0x00,0x00,0x00,0x00,
            0x00,0x97,0x3C,0xAE,0x50,0xC6,0x35,0xB9,
            0x3B,0x05,0x00,0x00,0x00,0x05,0x00,0x00,
            0x00,0x0B,0x00,0x1C,0x00,0x4C,0x49,0x43,
            0x45,0x4E,0x53,0x45,0x2E,0x74,0x78,0x74,
            0x55,0x54,0x09,0x00,0x03,0x5E,0x3B,0xBD,
            0x5E,0x5E,0x3B,0xBD,0x5E,0x75,0x78,0x0B,
            0x00,0x01,0x04,0xF6,0x01,0x00,0x00,0x04,
            0x14,0x00,0x00,0x00,0x74,0x65,0x73,0x74,
            0x0A,0x50,0x4B,0x01,0x02,0x1E,0x03,0x0A,
            0x00,0x00,0x00,0x00,0x00,0x8F,0x3C,0xAE,
            0x50,0x7A,0x7A,0x6F,0xED,0x03,0x00,0x00,
            0x00,0x03,0x00,0x00,0x00,0x0A,0x00,0x18,
            0x00,0x00,0x00,0x00,0x00,0x01,0x00,0x00,
            0x00,0xA4,0x81,0x00,0x00,0x00,0x00,0x52,
            0x45,0x41,0x44,0x4D,0x45,0x2E,0x74,0x78,
            0x74,0x55,0x54,0x05,0x00,0x03,0x4E,0x3B,
            0xBD,0x5E,0x75,0x78,0x0B,0x00,0x01,0x04,
            0xF6,0x01,0x00,0x00,0x04,0x14,0x00,0x00,
            0x00,0x50,0x4B,0x01,0x02,0x1E,0x03,0x0A,
            0x00,0x00,0x00,0x00,0x00,0x97,0x3C,0xAE,
            0x50,0xC6,0x35,0xB9,0x3B,0x05,0x00,0x00,
            0x00,0x05,0x00,0x00,0x00,0x0B,0x00,0x18,
            0x00,0x00,0x00,0x00,0x00,0x01,0x00,0x00,
            0x00,0xA4,0x81,0x47,0x00,0x00,0x00,0x4C,
            0x49,0x43,0x45,0x4E,0x53,0x45,0x2E,0x74,
            0x78,0x74,0x55,0x54,0x05,0x00,0x03,0x5E,
            0x3B,0xBD,0x5E,0x75,0x78,0x0B,0x00,0x01,
            0x04,0xF6,0x01,0x00,0x00,0x04,0x14,0x00,
            0x00,0x00,0x50,0x4B,0x05,0x06,0x00,0x00,
            0x00,0x00,0x02,0x00,0x02,0x00,0xA1,0x00,
            0x00,0x00,0x91,0x00,0x00,0x00,0x00,0x00
        };


        public static Stream Stream => new MemoryStream(bytes);
    }
}