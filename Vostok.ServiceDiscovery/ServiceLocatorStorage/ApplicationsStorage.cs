using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Vostok.Commons.Threading;
using Vostok.Logging.Abstractions;
using Vostok.ServiceDiscovery.Helpers;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;

namespace Vostok.ServiceDiscovery.ServiceLocatorStorage
{
    internal class ApplicationsStorage : IDisposable
    {
        private readonly ConcurrentDictionary<(string environment, string application), Lazy<ApplicationWithReplicas>> applications
            = new ConcurrentDictionary<(string environment, string application), Lazy<ApplicationWithReplicas>>();
        private readonly IZooKeeperClient zooKeeperClient;
        private readonly ServiceDiscoveryPathHelper pathHelper;
        private readonly ILog log;
        private readonly AdHocNodeWatcher nodeWatcher;
        private readonly AtomicBoolean disposed = new AtomicBoolean(false);

        public ApplicationsStorage(IZooKeeperClient zooKeeperClient, ServiceDiscoveryPathHelper pathHelper, ILog log)
        {
            this.zooKeeperClient = zooKeeperClient;
            this.pathHelper = pathHelper;
            this.log = log;
            nodeWatcher = new AdHocNodeWatcher(OnNodeEvent);
        }

        public ApplicationWithReplicas Get(string environment, string application)
        {
            if (applications.TryGetValue((environment, application), out var lazy))
                return lazy.Value;

            lazy = new Lazy<ApplicationWithReplicas>(
                () =>
                {
                    var container = new ApplicationWithReplicas(environment, application, pathHelper.BuildApplicationPath(environment, application), zooKeeperClient, nodeWatcher, log);
                    if (!disposed)
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
                if (!disposed)
                    kvp.Value.Value.Update();
            }
        }

        public void Dispose()
        {
            disposed.TrySetTrue();
        }

        private void Update(string environment, string application)
        {
            if (!applications.TryGetValue((environment, application), out var container))
            {
                log.Warn("Failed to update unexisting '{Application} application in '{Environment}' environment.", application, environment);
                return;
            }

            if (!disposed)
                container.Value.Update();
        }

        private void OnNodeEvent(NodeChangedEventType type, string path)
        {
            var parsedPath = pathHelper.TryParse(path);

            if (parsedPath?.environment == null || parsedPath.Value.application == null || parsedPath.Value.replica != null)
            {
                log.Warn("Recieved node event of type '{NodeEventType}' on path '{NodePath}': not an application node.", type, path);
                return;
            }

            // Note(kungurtsev): run in new thread, because we shouldn't block ZooKeeperClient.
            Task.Run(() => Update(parsedPath.Value.environment, parsedPath.Value.application));
        }
    }
}