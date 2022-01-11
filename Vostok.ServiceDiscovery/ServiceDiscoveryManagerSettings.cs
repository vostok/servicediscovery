using JetBrains.Annotations;
using Vostok.ServiceDiscovery.Helpers;
using Vostok.ServiceDiscovery.Telemetry;
using Vostok.ServiceDiscovery.Telemetry.Event;

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

        /// <summary>
        /// <see cref="IServiceDiscoveryEventsContext">Context</see> for sending events at:
        /// <list type="bullet">
        /// <item>Add replica to black list (<see cref="ServiceDiscoveryEventKind.ReplicaAddedToBlackList"/>)</item>
        /// <item>Remove replica from black list (<see cref="ServiceDiscoveryEventKind.ReplicaRemovedFromBlacklist"/>)</item>
        /// </list>
        /// </summary>
        [NotNull]
        public IServiceDiscoveryEventsContext ServiceDiscoveryEventContext { get; set; } = new DevNullServiceDiscoveryEventsContext();
    }
}