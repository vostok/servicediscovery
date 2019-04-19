using JetBrains.Annotations;

namespace Vostok.ServiceDiscovery
{
    /// <summary>
    /// Represents <see cref="ServiceLocatorSettings"/> settings.
    /// </summary>
    [PublicAPI]
    public class ServiceLocatorSettings
    {
        public string ZooKeeperNodePath { get; set; } = "/service-discovery/v2";
    }
}