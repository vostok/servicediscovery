using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ServiceDiscovery.Models;

namespace Vostok.ServiceDiscovery.Serializers
{
    internal static class ReplicaNodeDataSerializer
    {
        private const string KeyValueDelimiter = " = ";
        private const string LinesDelimiter = "\n";

        // CR(kungurtsev): make Serialize/Deserialize private.

        [NotNull]
        public static byte[] SerializeProperties([CanBeNull] IReadOnlyDictionary<string, string> properties)
        {
            properties = properties ?? new Dictionary<string, string>();
            var content = string.Join(
                LinesDelimiter,
                properties
                    .Where(item => !string.IsNullOrEmpty(item.Value))
                    .Select(item => $"{item.Key}{KeyValueDelimiter}{item.Value}".Replace(LinesDelimiter, " ")));
            return Encoding.UTF8.GetBytes(content);
        }

        [NotNull]
        public static Dictionary<string, string> DeserializeProperties([CanBeNull] byte[] data)
        {
            var content = Encoding.UTF8.GetString(data ?? new byte[0]);
            var lines = content.Split(new[] {LinesDelimiter}, StringSplitOptions.RemoveEmptyEntries);
            return lines
                .Where(line => !string.IsNullOrEmpty(line))
                .Select(line => line.Split(new[] {KeyValueDelimiter}, 2, StringSplitOptions.RemoveEmptyEntries))
                .Where(lineParts => lineParts.Length == 2)
                .ToDictionary(
                    lineParts => lineParts[0],
                    lineParts => lineParts[1],
                    StringComparer.OrdinalIgnoreCase
                );
        }

        [NotNull]
        public static byte[] Serialize(IReplicaInfo replica) => SerializeProperties(replica.Properties);

        [NotNull]
        public static IReplicaInfo Deserialize(string environment, string application, string replica, [CanBeNull] byte[] data) =>
            new ReplicaInfo(environment, application, replica, DeserializeProperties(data));
    }
}