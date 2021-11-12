using System;
using JetBrains.Annotations;
using Vostok.ServiceDiscovery.Telemetry;
using Vostok.ServiceDiscovery.Telemetry.EventDescription;
using Vostok.ServiceDiscovery.Telemetry.EventSender;
using Vostok.ServiceDiscovery.Telemetry.Extensions;

namespace Vostok.ServiceDiscovery.Helpers
{
    internal class ServiceDiscoveryEventSenderHelper
    {
        private readonly IServiceDiscoveryEventSender serviceDiscoveryEventSender;

        public ServiceDiscoveryEventSenderHelper([NotNull] IServiceDiscoveryEventSender serviceDiscoveryEventSender)
        {
            this.serviceDiscoveryEventSender = serviceDiscoveryEventSender;
        }

        public void Send(Func<ServiceDiscoveryEventDescription, ServiceDiscoveryEventDescription> descriptionSetup)
        {
            var description = descriptionSetup(new ServiceDiscoveryEventDescription());

            var events = ServiceDiscoveryEventsBuilder.FromDescription(description);
            serviceDiscoveryEventSender.Send(events);
        }

        public void TrySendFromContext(Action<ServiceDiscoveryEventDescription> descriptionSetup)
        {
            if (ServiceDiscoveryEventDescriptionContext.CurrentDescription == null)
                return;

            descriptionSetup(ServiceDiscoveryEventDescriptionContext.CurrentDescription);

            var events = ServiceDiscoveryEventsBuilder.FromDescription(ServiceDiscoveryEventDescriptionContext.CurrentDescription);
            serviceDiscoveryEventSender.Send(events);
        }
    }
}