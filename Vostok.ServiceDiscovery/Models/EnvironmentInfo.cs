using System.Collections.Generic;
using JetBrains.Annotations;

namespace Vostok.ServiceDiscovery.Models
{
    internal class EnvironmentInfo
    {
        public EnvironmentInfo([CanBeNull] string parentEnvironment, [CanBeNull] Dictionary<string, string> properties)
        {
            ParentEnvironment = parentEnvironment;
            Properties = properties ?? new Dictionary<string, string>();
        }

        [CanBeNull]
        public string ParentEnvironment { get; }

        [NotNull]
        public Dictionary<string, string> Properties { get; }
    }
}