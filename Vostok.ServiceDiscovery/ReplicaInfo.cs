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

        /// <param name="environment">Application environment. Example: <c>default</c>.</param>
        /// <param name="application">Application name. Example: <c>hercules.api</c>.</param>
        /// <param name="replica">Replica url or description. Example: <c>http://localhost:888/</c>, <c>process-name(pid)</c>.</param>
        public ReplicaInfo([NotNull] string environment, [NotNull] string application, [NotNull] string replica)
        {
            Environment = environment ?? throw new ArgumentNullException(nameof(environment));
            Application = application ?? throw new ArgumentNullException(nameof(application));
            Replica = replica ?? throw new ArgumentNullException(nameof(replica));
        }

        [NotNull]
        public string Environment { get; }

        [NotNull]
        public string Application { get; }

        [NotNull]
        public string Replica { get; }

        [NotNull]
        public ReplicaInfo AddProperty([NotNull] string key, [CanBeNull] string value)
        {
            properties[key ?? throw new ArgumentNullException(nameof(key))] = value;
            return this;
        }

        [NotNull]
        public IReadOnlyDictionary<string, string> Properties => properties;
    }
}