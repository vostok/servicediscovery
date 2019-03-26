using System;
using JetBrains.Annotations;
using Vostok.Commons.Time;

namespace Vostok.ServiceDiscovery
{
    /// <summary>
    /// Represents <see cref="ServiceBeacon"/> settings.
    /// </summary>
    [PublicAPI]
    public class ServiceBeaconSettings
    {
        public string ZooKeeperNodePath { get; set; } = "/service-discovery/v2";

        public TimeSpan MinimumTimeBetweenIterations { get; set; } = 500.Milliseconds();

        public TimeSpan IterationPeriod { get; set; } = 1.Minutes();
    }
}