using System;
using System.IO;
using System.IO.Compression;
using System.Reflection;

namespace Cythral.CloudFormation.S3Deployment
{
    public static class ZipArchiveEntryExtensions
    {
        private static Func<ZipArchiveEntry, bool, Stream> OpenInReadModeDelegate;

        static ZipArchiveEntryExtensions()
        {
            var type = typeof(Func<ZipArchiveEntry, bool, Stream>);
            var method = typeof(ZipArchiveEntry).GetMethod("OpenInReadMode", BindingFlags.NonPublic | BindingFlags.Instance);
            OpenInReadModeDelegate = (Func<ZipArchiveEntry, bool, Stream>)Delegate.CreateDelegate(type, method);
        }



        public static Stream OpenInReadMode(this ZipArchiveEntry entry)
        {
            return OpenInReadModeDelegate(entry, false);
        }
    }
}