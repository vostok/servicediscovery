using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Vostok.ServiceDiscovery
{
    /// <summary>
    /// Represents replica information.
    /// </summary>
    [PublicAPI]
    public class ReplicaInfo
    {
        private readonly Dictionary<string, string> properties = new Dictionary<string, string>();

        /// <param name="environment">Service environment. Example: <c>default</c>.</param>
        /// <param name="service">Service name. Example: <c>hercules.api</c>.</param>
        /// <param name="replica">Service replica (uri or description). Example: <c>http://localhost:888/</c>, <c>process-name(pid)</c>.</param>
        public ReplicaInfo([NotNull] string environment, [NotNull] string service, [NotNull] string replica)
        {
            Environment = environment ?? throw new ArgumentNullException(nameof(environment));
            Service = service ?? throw new ArgumentNullException(nameof(service));
            Replica = replica ?? throw new ArgumentNullException(nameof(replica));
        }

        [NotNull]
        public string Environment { get; }

        [NotNull]
        public string Service { get; }

        [NotNull]
        public string Replica { get; }

        [NotNull]
        public ReplicaInfo AddProperty([NotNull] string key, [CanBeNull] string value)
        {
            properties[key ?? throw new ArgumentNullException(nameof(key))] = value;
            return this;
        }

        [NotNull]
        public IReadOnlyDictionary<string, string> ToDictionary() => properties;
    }
}