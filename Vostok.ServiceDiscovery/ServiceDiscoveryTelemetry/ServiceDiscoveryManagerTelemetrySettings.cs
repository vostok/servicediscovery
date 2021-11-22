using JetBrains.Annotations;
using Vostok.ServiceDiscovery.Telemetry.Event;
using Vostok.ServiceDiscovery.Telemetry.EventSender;

namespace Vostok.ServiceDiscovery.ServiceDiscoveryTelemetry
{
    [PublicAPI]
    public class ServiceDiscoveryManagerTelemetrySettings
    {
        /// <summary>
        /// <see cref="IServiceDiscoveryEventSender">Sender</see> for sending events at:
        /// <list type="bullet">
        /// <item>Add replica to black list (<see cref="ServiceDiscoveryEventKind.AddToBlackList"/>)</item>
        /// <item>Remove replica from black list (<see cref="ServiceDiscoveryEventKind.RemoveFromBlackList"/>)</item>
        /// </list>
        /// </summary>
        [NotNull]
        public IServiceDiscoveryEventSender ServiceDiscoveryEventSender { get; set; } = new DevNullServiceDiscoveryEventSender();
    }
}