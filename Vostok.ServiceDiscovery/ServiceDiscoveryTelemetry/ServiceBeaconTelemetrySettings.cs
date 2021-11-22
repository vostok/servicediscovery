using JetBrains.Annotations;
using Vostok.ServiceDiscovery.Telemetry.EventSender;

namespace Vostok.ServiceDiscovery.ServiceDiscoveryTelemetry
{
    [PublicAPI]
    public class ServiceBeaconTelemetrySettings
    {
        /// <summary>
        /// <see cref="IServiceDiscoveryEventSender">Sender</see> for sending events about start and stop of the beacon
        /// </summary>
        [NotNull]
        public IServiceDiscoveryEventSender ServiceDiscoveryEventSender { get; set; } = new DevNullServiceDiscoveryEventSender();
    }
}