using Vostok.Commons.Threading;
using Vostok.ServiceDiscovery.Abstractions.Models;
using Vostok.ServiceDiscovery.Helpers;

namespace Vostok.ServiceDiscovery.ServiceLocatorStorage;

internal class EnvironmentContainerWrapper
{
    public VersionedContainer<EnvironmentInfo> Container { get; }
    
    public AtomicBoolean NodeIsDeleted { get; }

    public EnvironmentContainerWrapper(VersionedContainer<EnvironmentInfo> container)
    {
        Container = container;
        NodeIsDeleted = new AtomicBoolean(false);
    }

    public void MarkAsDeleted() => NodeIsDeleted.TrySetTrue();
}