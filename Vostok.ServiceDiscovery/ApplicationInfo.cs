using System.Collections.Generic;
using JetBrains.Annotations;

namespace Vostok.ServiceDiscovery
{
    internal class ApplicationInfo
    {
        public ApplicationInfo([CanBeNull] Dictionary<string, string> properties)
        {
            Properties = properties ?? new Dictionary<string, string>();
        }

        [NotNull]
        public Dictionary<string, string> Properties { get; }
    }
}