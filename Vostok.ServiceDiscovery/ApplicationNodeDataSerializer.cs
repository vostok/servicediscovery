using System.Collections.Generic;
using JetBrains.Annotations;
using Vostok.Commons.Binary;

namespace Vostok.ServiceDiscovery
{
    internal static class ApplicationNodeDataSerializer
    {
        [NotNull]
        public static byte[] Serialize([CanBeNull] ApplicationInfo info)
        {
            var writer = new BinaryBufferWriter(0);
            writer.WriteDictionary(
                info?.Properties ?? new Dictionary<string, string>(),
                (w, k) => w.WriteWithLength(k),
                (w, v) => w.WriteWithLength(v));

            return writer.Buffer;
        }

        [NotNull]
        public static ApplicationInfo Deserialize([CanBeNull] byte[] data)
        {
            if (data == null || data.Length == 0)
                return new ApplicationInfo(null);

            var reader = new BinaryBufferReader(data, 0);

            var properties = reader.ReadDictionary(r => r.ReadString(), r => r.ReadString());

            return new ApplicationInfo(properties);
        }
    }
}