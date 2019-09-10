using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Vostok.ServiceDiscovery.Abstractions;

namespace Vostok.ServiceDiscovery.Models
{
    internal class EnvironmentInfo : IEnvironmentInfo
    {
        public EnvironmentInfo([NotNull] string environment, [CanBeNull] string parentEnvironment, [CanBeNull] IReadOnlyDictionary<string, string> properties)
        {
            if (string.IsNullOrWhiteSpace(environment))
                throw new ArgumentOutOfRangeException(nameof(environment), environment);
            Environment = environment;
            ParentEnvironment = parentEnvironment;
            Properties = properties ?? new Dictionary<string, string>();
        }

        [NotNull]
        public string Environment { get; }

        [CanBeNull]
        public string ParentEnvironment { get; }

        [NotNull]
        public IReadOnlyDictionary<string, string> Properties { get; }
    }
}