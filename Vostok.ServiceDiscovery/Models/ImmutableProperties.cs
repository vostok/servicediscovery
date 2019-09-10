using System.Collections;
using System.Collections.Generic;
using JetBrains.Annotations;
using Vostok.Commons.Collections;

namespace Vostok.ServiceDiscovery.Models
{
    /// <inheritdoc />
    internal class ImmutableProperties : IReadOnlyDictionary<string, string>
    {
        private readonly ImmutableArrayDictionary<string, string> properties;

        protected ImmutableProperties([CanBeNull] IReadOnlyDictionary<string, string> properties)
        {
            properties = properties ?? new Dictionary<string, string>();
            this.properties = new ImmutableArrayDictionary<string, string>(properties.Count);

            foreach (var kvp in properties)
                this.properties = this.properties.Set(kvp.Key, kvp.Value);
        }

        protected ImmutableProperties(ImmutableArrayDictionary<string, string> properties)
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
        public string this[string key] => properties[key];

        /// <inheritdoc />
        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();

        public ImmutableArrayDictionary<string, string> Set(string key, string value) =>
            properties.Set(key, value);


        public ImmutableArrayDictionary<string, string> Remove(string key) =>
            properties.Remove(key);
    }
}