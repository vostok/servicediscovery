using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Logging.Abstractions;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ServiceDiscovery.Helpers;
using Vostok.ServiceDiscovery.ServiceLocatorStorage;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;

namespace Vostok.ServiceDiscovery
{
    /// <inheritdoc cref="ServiceLocator"/>
    [PublicAPI]
    public class ServiceLocator : IServiceLocator, IDisposable
    {
        private readonly IZooKeeperClient zooKeeperClient;
        private readonly ServiceLocatorSettings settings;
        private readonly ILog log;
        private readonly ServiceDiscoveryPathHelper pathHelper;
        private readonly AdHocNodeWatcher nodeWatcher;
        private readonly ConcurrentDictionary<string, ApplicationEnvironments> applications = new ConcurrentDictionary<string, ApplicationEnvironments>();

        public ServiceLocator(
            [NotNull] IZooKeeperClient zooKeeperClient,
            [CanBeNull] ServiceLocatorSettings settings = null,
            [CanBeNull] ILog log = null)
        {
            this.zooKeeperClient = zooKeeperClient;
            this.settings = settings ?? new ServiceLocatorSettings();
            this.log = (log ?? LogProvider.Get()).ForContext<ServiceLocator>();

            pathHelper = new ServiceDiscoveryPathHelper(this.settings.ZooKeeperNodePath);
            nodeWatcher = new AdHocNodeWatcher(OnNodeEvent);
        }

        /// <inheritdoc />
        [NotNull]
        public IServiceTopology Locate(string environment, string application)
        {
            var environments = applications.GetOrAdd(application, a => new ApplicationEnvironments(a, zooKeeperClient, nodeWatcher, pathHelper, log));
            return environments.Locate(environment);
        }

        public void Dispose()
        {
        }

        // TODO(kungurtsev): periodical update process

        private void Update(string environment, string application)
        {
            if (!applications.TryGetValue(application, out var environments))
            {
                log.Warn("Failed to update unexisting '{Application}'", application);
                return;
            }

            environments.UpdateApplicationEnvironment(environment);
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