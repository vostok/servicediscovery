using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using Vostok.Commons.Collections;
using Vostok.ServiceDiscovery.Abstractions;

namespace Vostok.ServiceDiscovery
{
    /// <inheritdoc />
    internal class ServiceTopologyProperties : IServiceTopologyProperties
    {
        private readonly ImmutableArrayDictionary<string, string> properties;

        public ServiceTopologyProperties([CanBeNull] IReadOnlyDictionary<string, string> properties)
        {
            properties = properties ?? new Dictionary<string, string>();
            this.properties = new ImmutableArrayDictionary<string, string>(properties.Count);

            foreach (var kvp in properties)
                this.properties = this.properties.Set(kvp.Key, kvp.Value);
        }

        private ServiceTopologyProperties(ImmutableArrayDictionary<string, string> properties)
        {
            this.properties = properties ?? ImmutableArrayDictionary<string, string>.Empty;
        }

        /// <inheritdoc />
        public int Count => properties.Count;

        /// <inheritdoc />
        public IEnumerable<string> Keys => properties.Keys;

        /// <inheritdoc />
        public IEnumerable<string> Values => properties.Values;

        /// <inheritdoc />
        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() =>
            properties.GetEnumerator();

        /// <inheritdoc />
        public bool ContainsKey(string key) => properties.ContainsKey(key);

        /// <inheritdoc />
        public bool TryGetValue(string key, out string value) => properties.TryGetValue(key, out value);

        /// <inheritdoc />
        public IServiceTopologyProperties Set(string key, string value) =>
            new ServiceTopologyProperties(properties.Set(key, value));

        /// <inheritdoc />
        public IServiceTopologyProperties Remove(string key) =>
            new ServiceTopologyProperties(properties.Remove(key));

        /// <inheritdoc />
        public string this[string key] => properties[key];

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }
}