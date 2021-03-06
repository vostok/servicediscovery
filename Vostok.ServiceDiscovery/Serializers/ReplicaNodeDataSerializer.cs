﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ServiceDiscovery.Abstractions.Models;

namespace Vostok.ServiceDiscovery.Serializers
{
    internal static class ReplicaNodeDataSerializer
    {
        private const string KeyValueDelimiter = " = ";
        private const string LinesDelimiter = "\n";
        private const string AdditionalLinesDelimiter = "\r\n";

        [NotNull]
        public static byte[] Serialize(IReplicaInfo replica, AllowToSerializeProperty propertiesFilter = null) =>
            SerializeProperties(replica.Properties.Where(x => propertiesFilter?.Invoke(x.Key, x.Value) ?? true));

        [NotNull]
        public static IReplicaInfo Deserialize(string environment, string application, string replica, [CanBeNull] byte[] data) =>
            new ReplicaInfo(environment, application, replica, DeserializeProperties(data));

        [NotNull]
        private static byte[] SerializeProperties([CanBeNull] IEnumerable<KeyValuePair<string, string>> properties)
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
        private static Dictionary<string, string> DeserializeProperties([CanBeNull] byte[] data)
        {
            var content = Encoding.UTF8.GetString(data ?? new byte[0]);
            var lines = content.Split(new[] {AdditionalLinesDelimiter, LinesDelimiter}, StringSplitOptions.RemoveEmptyEntries);
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

        public delegate bool AllowToSerializeProperty(string key, string value);
    }
}