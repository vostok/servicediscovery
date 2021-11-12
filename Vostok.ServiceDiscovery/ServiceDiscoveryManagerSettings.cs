using JetBrains.Annotations;
using Vostok.ServiceDiscovery.Helpers;
using Vostok.ServiceDiscovery.Telemetry.EventSender;

namespace Vostok.ServiceDiscovery
{
    /// <summary>
    /// Represents <see cref="ServiceDiscoveryManager"/> settings.
    /// </summary>
    [PublicAPI]
    public class ServiceDiscoveryManagerSettings
    {
        public string ZooKeeperNodesPrefix { get; set; } = "/service-discovery/v2";

        public IZooKeeperPathEscaper ZooKeeperNodesPathEscaper { get; set; } = ZooKeeperPathEscaper.Instance;

        public int ZooKeeperNodeUpdateAttempts { get; set; } = 5;
        
        [NotNull]
        public IServiceDiscoveryEventSender ServiceDiscoveryEventSender { get; set; } = new DevNullServiceDiscoveryEventSender();
    }
}