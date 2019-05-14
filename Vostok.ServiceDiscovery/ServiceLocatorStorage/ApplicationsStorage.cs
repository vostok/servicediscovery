using System;
using System.Collections.Concurrent;
using System.Threading;
using Vostok.Commons.Threading;
using Vostok.Logging.Abstractions;
using Vostok.ServiceDiscovery.Helpers;
using Vostok.ZooKeeper.Client.Abstractions;

namespace Vostok.ServiceDiscovery.ServiceLocatorStorage
{
    internal class ApplicationsStorage : IDisposable
    {
        private readonly ConcurrentDictionary<(string environment, string application), Lazy<ApplicationWithReplicas>> applications
            = new ConcurrentDictionary<(string environment, string application), Lazy<ApplicationWithReplicas>>();
        private readonly IZooKeeperClient zooKeeperClient;
        private readonly ServiceDiscoveryPathHelper pathHelper;
        private readonly ILog log;
        private readonly AtomicBoolean isDisposed = new AtomicBoolean(false);

        public ApplicationsStorage(IZooKeeperClient zooKeeperClient, ServiceDiscoveryPathHelper pathHelper, ILog log)
        {
            this.zooKeeperClient = zooKeeperClient;
            this.pathHelper = pathHelper;
            this.log = log;
        }

        public ApplicationWithReplicas Get(string environment, string application)
        {
            if (applications.TryGetValue((environment, application), out var lazy))
                return lazy.Value;

            lazy = new Lazy<ApplicationWithReplicas>(
                () =>
                {
                    var container = new ApplicationWithReplicas(environment, application, pathHelper.BuildApplicationPath(environment, application), zooKeeperClient, log, isDisposed);
                    if (!isDisposed)
                        container.Update();
                    return container;
                },
                LazyThreadSafetyMode.ExecutionAndPublication);

            return applications.GetOrAdd((environment, application), _ => lazy).Value;
        }

        public void UpdateAll()
        {
            foreach (var kvp in applications)
            {
                if (isDisposed)
                    return;

                kvp.Value.Value.Update();
            }
        }

        public void Dispose()
        {
            isDisposed.TrySetTrue();
        }
    }
}