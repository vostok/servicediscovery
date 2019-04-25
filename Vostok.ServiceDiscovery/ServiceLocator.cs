using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Logging.Abstractions;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ServiceDiscovery.Helpers;
using Vostok.ServiceDiscovery.ServiceLocatorStorage;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;

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

    internal class ApplicationEnvironments
    {
        private readonly string application;
        private readonly IZooKeeperClient zooKeeperClient;
        private readonly AdHocNodeWatcher nodeWatcher;
        private readonly ServiceDiscoveryPathHelper pathHelper;
        private readonly ILog log;
        private readonly ConcurrentDictionary<string, ApplicationEnvironment> environments = new ConcurrentDictionary<string, ApplicationEnvironment>();

        public ApplicationEnvironments(string application, IZooKeeperClient zooKeeperClient, AdHocNodeWatcher nodeWatcher, ServiceDiscoveryPathHelper pathHelper, ILog log)
        {
            this.application = application;
            this.zooKeeperClient = zooKeeperClient;
            this.nodeWatcher = nodeWatcher;
            this.pathHelper = pathHelper;
            this.log = log;
        }

        public IServiceTopology Locate(string environmentName)
        {
            var environment = GetApplicationEnvironment(environmentName);

            var visitedEnvironments = new HashSet<string>();

            while (true)
            {
                if (!visitedEnvironments.Add(environment.Name))
                {
                    log.Warn("Cycled when resolving environment {Environment} parents.", environmentName);
                    return null;
                }

                var topology = environment.ServiceTopology;
                var parentEnvironment = environment.Environment?.ParentEnvironment;

                var goToParent = topology == null || topology.Replicas.Count == 0 && environment.Environment.SkipIfEmpty();

                if (parentEnvironment == null || !goToParent)
                    return topology;

                environment = GetApplicationEnvironment(parentEnvironment);
            }
        }

        public void UpdateApplicationEnvironment(string environmentName)
        {
            if (!environments.TryGetValue(environmentName, out var environment))
            {
                log.Warn("Failed to update unexisting application '{Application}' environment '{Environment}'", application, environmentName);
                return;
            }

            UpdateApplicationEnvironment(environment);
        }

        private ApplicationEnvironment GetApplicationEnvironment(string environment)
        {
            return environments.GetOrAdd(environment, e => UpdateApplicationEnvironment(new ApplicationEnvironment(e)));
        }

        private ApplicationEnvironment UpdateApplicationEnvironment(ApplicationEnvironment environment)
        {
            var environmentData = zooKeeperClient.GetData(pathHelper.BuildEnvironmentPath(environment.Name));
            environment.UpdateEnvironment(environmentData, log);
            if (!environmentData.IsSuccessful)
                return environment;

            var applicationPath = pathHelper.BuildApplicationPath(environment.Name, application);

            var applicationData = zooKeeperClient.GetData(new GetDataRequest(applicationPath) {Watcher = nodeWatcher});
            environment.UpdateApplication(applicationData, log);

            if (applicationData.IsSuccessful)
            {
                var getChildrenResult = zooKeeperClient.GetChildren(new GetChildrenRequest(applicationPath) { Watcher = nodeWatcher });
                environment.UpdateReplicas(getChildrenResult, log);
            }
            else if (applicationData.Status == ZooKeeperStatus.NodeNotFound)
            {
                // Note(kungurtsev): watch if node will be created.
                zooKeeperClient.Exists(new ExistsRequest(applicationPath) {Watcher = nodeWatcher});
            }

            return environment;
        }
    }
}