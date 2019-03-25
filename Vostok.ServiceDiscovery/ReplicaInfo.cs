using System.Collections.Generic;

namespace Vostok.ServiceDiscovery
{
    public class ReplicaInfo
    {
        private readonly Dictionary<string, string> properties = new Dictionary<string, string>();

        public string Envoronment { get; }
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