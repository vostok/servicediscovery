using System.Collections.Generic;
using JetBrains.Annotations;
using Vostok.Commons.Binary;

namespace Vostok.ServiceDiscovery
{
    internal class EnvironmentNodeDataSerializer
    {
        private const int WithPropertiesVersion = 2;

        [NotNull]
        public static byte[] Serialize([CanBeNull] EnvironmentInfo info)
        {
            var writer = new BinaryBufferWriter(0);
            writer.Write(WithPropertiesVersion);
            writer.WriteNullable(info?.ParentEnvironment, (w, i) => w.WriteWithLength(i));
            writer.WriteDictionary(info?.Properties ?? new Dictionary<string, string>(), 
                (w, k) => w.WriteWithLength(k), (w, v) => w.WriteWithLength(v));

            return writer.Buffer;
        }

        [NotNull]
        public static EnvironmentInfo Deserialize([CanBeNull] byte[] data)
        { 
            if (data == null)
                return new EnvironmentInfo(null, null);

            var reader = new BinaryBufferReader(data, 0);

            var version = reader.ReadInt32();

            var parentEnvironment = reader.ReadNullable(r => r.ReadString());
            var properties = version >= WithPropertiesVersion
                ? DeserializeProperties(reader)
                : null;

            return new EnvironmentInfo(parentEnvironment, properties);
        }

        private static Dictionary<string, string> DeserializeProperties(BinaryBufferReader reader)
        {
            return reader.ReadDictionary(r => r.ReadString(), r => r.ReadString());
        }
    }
}