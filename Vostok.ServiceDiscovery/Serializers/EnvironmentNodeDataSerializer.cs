using System.Collections.Generic;
using JetBrains.Annotations;
using Vostok.Commons.Binary;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ServiceDiscovery.Abstractions.Models;
using Vostok.ServiceDiscovery.Models;

namespace Vostok.ServiceDiscovery.Serializers
{
    internal static class EnvironmentNodeDataSerializer
    {
        private const int WithPropertiesVersion = 2;

        [NotNull]
        public static byte[] Serialize([CanBeNull] IEnvironmentInfo info)
        {
            var writer = new BinaryBufferWriter(0);
            writer.Write(WithPropertiesVersion);
            writer.WriteNullable(info?.ParentEnvironment, (w, i) => w.WriteWithLength(i));

            writer.WriteDictionary(
                (IReadOnlyDictionary<string, string>)info?.Properties ?? new Dictionary<string, string>(),
                (w, k) => w.WriteWithLength(k),
                (w, v) => w.WriteWithLength(v));

            return writer.Buffer;
        }

        [NotNull]
        public static EnvironmentInfo Deserialize([NotNull] string environment, [CanBeNull] byte[] data)
        {
            if (data == null || data.Length == 0)
                return new EnvironmentInfo(environment, null, null);

            var reader = new BinaryBufferReader(data, 0);

            var version = reader.ReadInt32();

            var parentEnvironment = reader.ReadNullable(r => r.ReadString());
            var properties = version >= WithPropertiesVersion
                ? DeserializeProperties(reader)
                : null;

            return new EnvironmentInfo(environment, parentEnvironment, properties);
        }

        private static Dictionary<string, string> DeserializeProperties(BinaryBufferReader reader)
        {
            return reader.ReadDictionary(r => r.ReadString(), r => r.ReadString());
        }
    }
}