using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Vostok.ServiceDiscovery
{
    internal static class NodeDataSerializer
    {
        private const string KeyValueDelimiter = " = ";
        private const string LinesDelimiter = "\n";

        public static byte[] Serialize(IReadOnlyDictionary<string, string> properties)
        {
            var content = string.Join(LinesDelimiter, properties.Select(item => $"{item.Key}{KeyValueDelimiter}{item.Value}"));
            return Encoding.UTF8.GetBytes(content);
        }

        public static Dictionary<string, string> Deserialize(byte[] data)
        {
            var content = Encoding.UTF8.GetString(data);
            var lines = content.Split(new [] {LinesDelimiter}, StringSplitOptions.RemoveEmptyEntries);
            return lines
                .Where(line => !string.IsNullOrEmpty(line))
                .Select(line => line.Split(new[] { LinesDelimiter }, 2, StringSplitOptions.RemoveEmptyEntries))
                .Where(lineParts => lineParts.Length == 2)
                .ToDictionary(
                    lineParts => lineParts[0].Trim(),
                    lineParts => lineParts[1].Trim(),
                    StringComparer.OrdinalIgnoreCase
                );
        }
    }
}