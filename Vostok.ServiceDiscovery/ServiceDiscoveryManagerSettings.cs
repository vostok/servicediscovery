using JetBrains.Annotations;
using Vostok.ServiceDiscovery.Helpers;

namespace Vostok.ServiceDiscovery
{
    /// <summary>
    /// Represents <see cref="ServiceLocator"/> settings.
    /// </summary>
    [PublicAPI]
    public class ServiceDiscoveryManagerSettings
    {
        public string ZooKeeperNodesPrefix { get; set; } = "/service-discovery/v2";

        public IZooKeeperPathEscaper ZooKeeperNodesPathEscaper { get; set; } = ZooKeeperPathEscaper.Instance;
    }
}