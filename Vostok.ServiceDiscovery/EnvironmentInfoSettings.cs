using System.Collections.Generic;
using JetBrains.Annotations;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ServiceDiscovery.Abstractions.Models;

namespace Vostok.ServiceDiscovery
{
    [PublicAPI]
    public class EnvironmentInfoSettings
    {
        [NotNull]
        public string ParentEnvironment { get; set; } = "default";

        [CanBeNull]
        public IReadOnlyDictionary<string, string> Properties { get; set; }

        public IEnvironmentInfo ToEnvironmentInfo(string envPath) => new EnvironmentInfo(envPath, ParentEnvironment, Properties);
    }
}