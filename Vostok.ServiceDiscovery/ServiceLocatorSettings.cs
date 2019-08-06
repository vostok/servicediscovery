using System;
using JetBrains.Annotations;
using Vostok.Commons.Time;
using Vostok.ServiceDiscovery.Helpers;

namespace Vostok.ServiceDiscovery
{
    /// <summary>
    /// Represents <see cref="ServiceLocator"/> settings.
    /// </summary>
    [PublicAPI]
    public class ServiceLocatorSettings
    {
        public int MaximumEnvironmentsDepth = 5;

        public string ZooKeeperNodesPrefix { get; set; } = "/service-discovery/v2";

        public IZooKeeperPathEscaper ZooKeeperNodesPathEscaper { get; set; } = ZooKeeperPathEscaper.Instance;

        public TimeSpan IterationPeriod { get; set; } = 5.Seconds();
    }
}