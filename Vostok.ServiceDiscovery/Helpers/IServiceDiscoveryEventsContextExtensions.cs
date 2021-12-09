using System;
using JetBrains.Annotations;
using Vostok.ServiceDiscovery.Telemetry;
using Vostok.ServiceDiscovery.Telemetry.EventsBuilder;

namespace Vostok.ServiceDiscovery.Helpers
{
    internal static class IServiceDiscoveryEventsContextExtensions
    {
        public static void SendFromContext(this IServiceDiscoveryEventsContext eventsContext, [NotNull] Action<IServiceDiscoveryEventsBuilder> setup)
        {
            using (new ServiceDiscoveryEventsContextToken(setup))
                eventsContext.SendFromContext();
        }
    }
}