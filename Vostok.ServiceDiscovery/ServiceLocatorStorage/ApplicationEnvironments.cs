using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Vostok.Logging.Abstractions;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ServiceDiscovery.Helpers;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;

namespace Vostok.ServiceDiscovery.ServiceLocatorStorage
{
    internal class ApplicationEnvironments
    {
        private readonly string application;
        private readonly IZooKeeperClient zooKeeperClient;
        private readonly AdHocNodeWatcher nodeWatcher;
        private readonly ServiceDiscoveryPathHelper pathHelper;
        private readonly ConcurrentDictionary<string, Lazy<ApplicationEnvironment>> environments = new ConcurrentDictionary<string, Lazy<ApplicationEnvironment>>();
        private readonly ILog log;

        public ApplicationEnvironments(string application, IZooKeeperClient zooKeeperClient, AdHocNodeWatcher nodeWatcher, ServiceDiscoveryPathHelper pathHelper, ILog log)
        {
            this.application = application;
            this.zooKeeperClient = zooKeeperClient;
            this.nodeWatcher = nodeWatcher;
            this.pathHelper = pathHelper;
            this.log = log;
        }

        /// <inheritdoc cref="ServiceLocator.Locate"/>
        public IServiceTopology Locate(string environmentName)
        {
            var environment = GetApplicationEnvironment(environmentName);

            var visitedEnvironments = new HashSet<string>();

            while (true)
            {
                if (!visitedEnvironments.Add(environment.Name))
                {
                    log.Warn("Cycled when resolving '{Environment}' environment parents.", environmentName);
                    return null;
                }

                var topology = environment.ServiceTopology;

                var parentEnvironment = environment.Environment?.ParentEnvironment;
                if (parentEnvironment == null)
                    return topology;

                var goToParent = topology == null || topology.Replicas.Count == 0 && environment.Environment.SkipIfEmpty();
                if (!goToParent)
                    return topology;

                environment = GetApplicationEnvironment(parentEnvironment);
            }
        }

        public void UpdateCache()
        {
            foreach (var kvp in environments)
            {
                UpdateApplicationEnvironment(kvp.Value.Value);
            }
        }

        public void UpdateCache(string environmentName)
        {
            if (!environments.TryGetValue(environmentName, out var environment))
            {
                log.Warn("Failed to update unexisting application '{Application}' environment '{Environment}'", application, environmentName);
                return;
            }

            UpdateApplicationEnvironment(environment.Value);
        }

        private ApplicationEnvironment GetApplicationEnvironment(string environment)
        {
            var lazy = new Lazy<ApplicationEnvironment>(
                () =>
                {
                    var applicationEnvironment = new ApplicationEnvironment(environment);
                    UpdateApplicationEnvironment(applicationEnvironment);
                    return applicationEnvironment;
                },
                LazyThreadSafetyMode.ExecutionAndPublication);
            return environments.GetOrAdd(environment, _ => lazy).Value;
        }

        private void UpdateApplicationEnvironment(ApplicationEnvironment environment)
        {
            try
            {
                var environmentData = zooKeeperClient.GetData(pathHelper.BuildEnvironmentPath(environment.Name));
                environment.UpdateEnvironment(environmentData, log);
                if (!environmentData.IsSuccessful)
                    return;

                var applicationPath = pathHelper.BuildApplicationPath(environment.Name, application);
                var applicationData = zooKeeperClient.GetData(new GetDataRequest(applicationPath) {Watcher = nodeWatcher});
                environment.UpdateApplication(applicationData, log);

                if (applicationData.IsSuccessful)
                {
                    var getChildrenResult = zooKeeperClient.GetChildren(new GetChildrenRequest(applicationPath) {Watcher = nodeWatcher});
                    environment.UpdateReplicas(getChildrenResult, log);
                }
                else if (applicationData.Status == ZooKeeperStatus.NodeNotFound)
                {
                    // Note(kungurtsev): watch if node will be created.
                    zooKeeperClient.Exists(new ExistsRequest(applicationPath) {Watcher = nodeWatcher});
                }
            }
            catch (Exception e)
            {
                log.Error(e, "Failed to update '{Application}' application in '{Environment}' environment.", application, environment.Name);
            }
        }
    }
}