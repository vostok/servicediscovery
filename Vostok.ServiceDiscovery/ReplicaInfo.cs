using System.Collections.Generic;
using JetBrains.Annotations;

namespace Vostok.ServiceDiscovery
{
    [PublicAPI]
    public class ReplicaInfo
    {
        private readonly Dictionary<string, string> properties = new Dictionary<string, string>();

        public ReplicaInfo(string environment, string service, string replica)
        {
            Environment = environment;
            Service = service;
            Replica = replica;
        }

        public string Environment { get; }
        public string Service { get; }
        public string Replica { get; }

        public ReplicaInfo AddProperty(string key, string value)
        {
            properties[key] = value;
            return this;
        }

        public IReadOnlyDictionary<string, string> ToDictionary() => properties;
    }
}