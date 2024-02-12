using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Threading;
using Vostok.Commons.Threading;
using Vostok.Logging.Abstractions;
using Vostok.ServiceDiscovery.Abstractions.Models;
using Vostok.ServiceDiscovery.Helpers;
using Vostok.ServiceDiscovery.Serializers;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;

namespace Vostok.ServiceDiscovery.ServiceLocatorStorage
{
    internal class EnvironmentsStorage : IDisposable
    {
        private readonly ConcurrentDictionary<string, Lazy<VersionedContainer<EnvironmentInfo>>> environments
            = new ConcurrentDictionary<string, Lazy<VersionedContainer<EnvironmentInfo>>>();
        private readonly IZooKeeperClient zooKeeperClient;
        private readonly ServiceDiscoveryPathHelper pathHelper;
        private readonly ActionsQueue eventsHandler;
        private readonly bool observeNonExistentEnvironments;
        private readonly ILog log;
        private readonly AdHocNodeWatcher nodeWatcher;
        private readonly AdHocNodeWatcher existsWatcher;
        private readonly AtomicBoolean isDisposed = new AtomicBoolean(false);

        public int EnvironmentsInStorage => environments.Count;

        public EnvironmentsStorage(
            IZooKeeperClient zooKeeperClient,
            ServiceDiscoveryPathHelper pathHelper,
            ActionsQueue eventsHandler,
            bool observeNonExistentEnvironments,
            ILog log)
        {
            this.zooKeeperClient = zooKeeperClient;
            this.pathHelper = pathHelper;
            this.eventsHandler = eventsHandler;
            this.observeNonExistentEnvironments = observeNonExistentEnvironments;
            this.log = log;
            nodeWatcher = new AdHocNodeWatcher(OnNodeEvent);
            existsWatcher = this.observeNonExistentEnvironments ? nodeWatcher : null;
        }

        public EnvironmentInfo Get(string name)
        {
            if (environments.TryGetValue(name, out var lazy))
                return lazy.Value.Value;

            return CreateAndGet(name);
        }

        public void UpdateAll()
        {
            foreach (var kvp in environments)
            {
                if (isDisposed)
                    return;

                Update(kvp.Key, kvp.Value.Value);
            }
        }

        public void Dispose()
        {
            isDisposed.TrySetTrue();
        }

        //(deniaa): Do not inline this method to avoid closures in the hot part of the Get method.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private EnvironmentInfo CreateAndGet(string name)
        {
            var lazy = new Lazy<VersionedContainer<EnvironmentInfo>>(
                () =>
                {
                    var container = new VersionedContainer<EnvironmentInfo>();
                    Update(name, container);
                    return container;
                },
                LazyThreadSafetyMode.ExecutionAndPublication);

            return environments.GetOrAdd(name, _ => lazy).Value.Value;
        }

        private void Update(string name)
        {
            if (!environments.TryGetValue(name, out var container))
            {
                log.Warn("Failed to update '{Environment}' environment: it does not exist in local cache.", name);
                return;
            }

            Update(name, container.Value);
        }

        private void Update(string name, VersionedContainer<EnvironmentInfo> container)
        {
            if (isDisposed)
                return;

            try
            {
                var environmentPath = pathHelper.BuildEnvironmentPath(name);
                var environmentExists = zooKeeperClient.Exists(new ExistsRequest(environmentPath) {Watcher = existsWatcher});
                if (!environmentExists.IsSuccessful)
                    return;

                if (environmentExists.Stat == null)
                {
                    if (!observeNonExistentEnvironments)
                    {
                        environments.TryRemove(name, out _);
                        return;
                    }

                    container.Clear();
                }
                else
                {
                    if (!container.NeedUpdate(environmentExists.Stat.ModifiedZxId))
                        return;

                    var environmentData = zooKeeperClient.GetData(new GetDataRequest(environmentPath) {Watcher = nodeWatcher});
                    if (environmentData.Status == ZooKeeperStatus.NodeNotFound)
                        container.Clear();
                    if (!environmentData.IsSuccessful)
                        return;

                    var info = EnvironmentNodeDataSerializer.Deserialize(name, environmentData.Data);
                    container.Update(environmentData.Stat.ModifiedZxId, info);
                }
            }
            catch (Exception error)
            {
                log.Error(error, "Failed to update '{Environment}' environment.", name);
            }
        }

        private void OnNodeEvent(NodeChangedEventType type, string path)
        {
            if (isDisposed)
                return;

            var parsedPath = pathHelper.TryParse(path);
            if (parsedPath?.environment == null || parsedPath.Value.application != null)
            {
                log.Warn("Received node event of type '{NodeEventType}' on path '{NodePath}': not an environment node.", type, path);
                return;
            }

            // Note(kungurtsev): run in new thread, because we shouldn't block ZooKeeperClient.
            eventsHandler.Enqueue(() => Update(parsedPath.Value.environment));
        }
    }
}