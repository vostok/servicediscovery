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
            if (string.IsNullOrWhiteSpace(environment))
                throw new ArgumentOutOfRangeException(nameof(environment), environment);
            if (string.IsNullOrWhiteSpace(application))
                throw new ArgumentOutOfRangeException(nameof(application), application);
            if (string.IsNullOrWhiteSpace(replica))
                throw new ArgumentOutOfRangeException(nameof(replica), replica);

            Environment = environment;
            Application = application;
            Replica = replica;
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