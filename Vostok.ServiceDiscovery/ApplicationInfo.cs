using System.Collections.Generic;
using JetBrains.Annotations;

namespace Vostok.ServiceDiscovery
{
    internal class ApplicationInfo
    {
        [NotNull]
        public Dictionary<string, string> Properties { get; }

        public ApplicationInfo([CanBeNull] Dictionary<string, string> properties)
        {
            Properties = properties ?? new Dictionary<string, string>();
        }
    }
}