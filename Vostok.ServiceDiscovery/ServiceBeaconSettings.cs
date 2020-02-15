using System;
using JetBrains.Annotations;
using Vostok.Commons.Time;
using Vostok.ServiceDiscovery.Helpers;

namespace Vostok.ServiceDiscovery
{
    /// <summary>
    /// Represents <see cref="ServiceBeacon"/> settings.
    /// </summary>
    [PublicAPI]
    public class ServiceBeaconSettings
    {
        public string ZooKeeperNodesPrefix { get; set; } = "/service-discovery/v2";

        public IZooKeeperPathEscaper ZooKeeperNodesPathEscaper { get; set; } = ZooKeeperPathEscaper.Instance;

        public TimeSpan MinimumTimeBetweenIterations { get; set; } = 500.Milliseconds();

        public TimeSpan IterationPeriod { get; set; } = 1.Minutes();

        public TimeSpan InitialRegistrationIterationPeriod { get; set; } = 1.Seconds();

        public TimeSpan DeleteNodeIterationPeriod { get; set; } = 1.Seconds();

        /// <summary>
        /// If <see cref="RegistrationAllowedProvider"/> has been configured and returns <c>false</c>, service beacon will be stopped, until <c>true</c> received.
        /// </summary>
        [CanBeNull]
        public Func<bool> RegistrationAllowedProvider { get; set; }

        public bool UseFQDN { get; set; }
    }
}