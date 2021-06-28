using JetBrains.Annotations;
using Vostok.ServiceDiscovery.Abstractions.Models;

namespace Vostok.ServiceDiscovery.Models
{
    internal class ServiceBeaconInfo
    {
        public ServiceBeaconInfo(ReplicaInfo replicaInfo, TagCollection tags = null)
        {
            ReplicaInfo = replicaInfo;
            Tags = tags ?? new TagCollection();
        }
        
        [NotNull]
        public ReplicaInfo ReplicaInfo { get; }
        
        [NotNull]
        public TagCollection Tags { get; }
    }
}