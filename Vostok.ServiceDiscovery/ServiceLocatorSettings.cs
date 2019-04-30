using System;
using JetBrains.Annotations;
using Vostok.Commons.Time;

namespace Vostok.ServiceDiscovery
{
    /// <summary>
    /// Represents <see cref="ServiceLocator"/> settings.
    /// </summary>
    [PublicAPI]
    public class ServiceLocatorSettings
    {
        public int MaximumEnvironmentDeep = 10;
        public string ZooKeeperNodesPrefix { get; set; } = "/service-discovery/v2";

        public TimeSpan IterationPeriod { get; set; } = 5.Seconds();
    }
}