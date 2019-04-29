using JetBrains.Annotations;

namespace Vostok.ServiceDiscovery
{
    /// <summary>
    /// A delegate that configures <see cref="IReplicaInfoBuilder"/> during <see cref="ServiceBeacon"/> construction.
    /// </summary>
    [PublicAPI]
    public delegate void ReplicaInfoSetup([NotNull] IReplicaInfoBuilder builder);
}