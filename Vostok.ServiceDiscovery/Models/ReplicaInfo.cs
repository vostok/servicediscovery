using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Vostok.ServiceDiscovery.Models
{
    internal class ReplicaInfo
    {
        private readonly Dictionary<string, string> properties = new Dictionary<string, string>();

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
        public IReadOnlyDictionary<string, string> Properties => properties;

        public void SetProperty([NotNull] string key, [CanBeNull] string value)
        {
            properties[key ?? throw new ArgumentNullException(nameof(key))] = value;
        }
    }
}