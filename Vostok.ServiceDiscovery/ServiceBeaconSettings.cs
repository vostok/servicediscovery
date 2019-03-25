using System;
using JetBrains.Annotations;
using Vostok.Commons.Time;

namespace Vostok.ServiceDiscovery
{
    [PublicAPI]
    public class ServiceBeaconSettings
    {
        public string ZooKeeperNodePath { get; set; } = "/service-discovery/v2";

        public TimeSpan MinimumTimeBetweenIterations { get; set; } = 500.Milliseconds();

        public TimeSpan IterationTimeSpan { get; set; } = 1.Minutes();
    }
}