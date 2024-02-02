using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
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
        private readonly ActionsQueue eventsQueue;
        private readonly bool observeNonExistentApplications;
        private readonly ILog log;
        private readonly AtomicBoolean isDisposed = false;

        public ApplicationsStorage(
            IZooKeeperClient zooKeeperClient,
            ServiceDiscoveryPathHelper pathHelper,
            ActionsQueue eventsQueue,
            bool observeNonExistentApplications,
            ILog log)
        {
            this.zooKeeperClient = zooKeeperClient;
            this.pathHelper = pathHelper;
            this.eventsQueue = eventsQueue;
            this.observeNonExistentApplications = observeNonExistentApplications;
            this.log = log;
        }

        public ApplicationWithReplicas Get(string environment, string application)
        {
            if (applications.TryGetValue((environment, application), out var lazy))
                return lazy.Value;

            return CreateAndGet(environment, application, lazy);
        }

        public void UpdateAll()
        {
            foreach (var kvp in applications)
            {
                if (isDisposed)
                    return;

                kvp.Value.Value.Update(out var appExists);
                if (!appExists && !observeNonExistentApplications)
                    applications.TryRemove(kvp.Key, out _);
            }
        }

        public void Dispose()
        {
            if (isDisposed.TrySetTrue())
            {
                foreach (var kvp in applications)
                {
                    kvp.Value.Value.Dispose();
                }
            }
        }

        //(deniaa): Do not inline this method to avoid closures in the hot part of the Get method.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private ApplicationWithReplicas CreateAndGet(string environment, string application, Lazy<ApplicationWithReplicas> lazy)
        {
            lazy = new Lazy<ApplicationWithReplicas>(
                () =>
                {
                    var container = new ApplicationWithReplicas(environment, application, pathHelper.BuildApplicationPath(environment, application), zooKeeperClient, pathHelper, eventsQueue, observeNonExistentApplications, log);
                    if (!isDisposed)
                        container.Update(out _);
                    return container;
                },
                LazyThreadSafetyMode.ExecutionAndPublication);

            var value = applications.GetOrAdd((environment, application), _ => lazy).Value;

            if (isDisposed)
                value.Dispose();

            return value;
        }
    }
}