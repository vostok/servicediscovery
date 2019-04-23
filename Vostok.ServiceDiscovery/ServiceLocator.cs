using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Commons.Helpers.Url;
using Vostok.Logging.Abstractions;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ServiceDiscovery.Serializers;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

namespace Vostok.ServiceDiscovery
{
    /// <inheritdoc cref="ServiceLocator"/>
    [PublicAPI]
    public class ServiceLocator : IServiceLocator, IDisposable
    {
        private readonly IZooKeeperClient zooKeeperClient;
        private readonly ServiceLocatorSettings settings;
        private readonly ILog log;
        private readonly ServiceDiscoveryPath serviceDiscoveryPath;
        private readonly AdHocNodeWatcher nodeWatcher;
        private readonly ConcurrentDictionary<string, ApplicationEnvironments> applications = new ConcurrentDictionary<string, ApplicationEnvironments>();

        public ServiceLocator(
            [NotNull] IZooKeeperClient zooKeeperClient,
            [CanBeNull] ServiceLocatorSettings settings,
            [CanBeNull] ILog log)
        {
            this.zooKeeperClient = zooKeeperClient;
            this.settings = settings ?? new ServiceLocatorSettings();
            this.log = (log ?? LogProvider.Get()).ForContext<ServiceLocator>();

            serviceDiscoveryPath = new ServiceDiscoveryPath(this.settings.ZooKeeperNodePath);
            nodeWatcher = new AdHocNodeWatcher(OnNodeEvent);
        }

        /// <inheritdoc />
        [NotNull]
        public IServiceTopology Locate(string environment, string application)
        {
            var environments = applications.GetOrAdd(application, a => new ApplicationEnvironments(a, zooKeeperClient, nodeWatcher, serviceDiscoveryPath, log));
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
            var parsedPath = serviceDiscoveryPath.TryParse(path);

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
        private readonly ServiceDiscoveryPath serviceDiscoveryPath;
        private readonly ILog log;
        private readonly ConcurrentDictionary<string, ApplicationEnvironment> environments = new ConcurrentDictionary<string, ApplicationEnvironment>();

        public ApplicationEnvironments(string application, IZooKeeperClient zooKeeperClient, AdHocNodeWatcher nodeWatcher, ServiceDiscoveryPath serviceDiscoveryPath, ILog log)
        {
            this.application = application;
            this.zooKeeperClient = zooKeeperClient;
            this.nodeWatcher = nodeWatcher;
            this.serviceDiscoveryPath = serviceDiscoveryPath;
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
            var environmentData = zooKeeperClient.GetData(serviceDiscoveryPath.BuildEnvironmentPath(environment.Name));
            environment.UpdateEnvironment(environmentData, log);

            var applicationPath = serviceDiscoveryPath.BuildApplicationPath(environment.Name, application);

            var applicationData = zooKeeperClient.GetData(new GetDataRequest(applicationPath) {Watcher = nodeWatcher});
            environment.UpdateApplication(applicationData, log);

            if (applicationData.IsSuccessful)
            {
                var getChildrenResult = zooKeeperClient.GetChildren(new GetChildrenRequest(applicationPath) { Watcher = nodeWatcher });
                environment.UpdateReplicas(getChildrenResult, log);
            }
            else
            {
                // Note(kungurtsev): watch if node will be created.
                zooKeeperClient.Exists(new ExistsRequest(applicationPath) {Watcher = nodeWatcher});
            }

            return environment;
        }
    }

    internal class ApplicationEnvironment
    {
        public readonly string Name;
        private readonly VersionedContainer<EnvironmentInfo> environmentContainer;
        private readonly VersionedContainer<ApplicationInfo> applicationContainer;
        private readonly VersionedContainer<Uri[]> replicasContainer;
        
        public ApplicationEnvironment(string name)
        {
            Name = name;
            environmentContainer = new VersionedContainer<EnvironmentInfo>();
            applicationContainer = new VersionedContainer<ApplicationInfo>();
            replicasContainer = new VersionedContainer<Uri[]>();
        }

        public EnvironmentInfo Environment => environmentContainer.Value;

        public ServiceTopology ServiceTopology => BuildServiceTopology();

        public void UpdateEnvironment(GetDataResult environmentData, ILog log)
        {
            if (environmentData.Status == ZooKeeperStatus.NodeNotFound)
                RemoveApplication();
            if (!environmentData.IsSuccessful)
                return;

            try
            {
                environmentContainer.Update(environmentData.Stat.ModifiedZxId, () => 
                    EnvironmentNodeDataSerializer.Deserialize(environmentData.Data));
            }
            catch (Exception e)
            {
                log.Error(e, "Failed to update environment info for path '{Path}'.", environmentData.Path);
            }
        }

        public void UpdateApplication(GetDataResult applicationData, ILog log)
        {
            if (applicationData.Status == ZooKeeperStatus.NodeNotFound)
                RemoveApplication();
            if (!applicationData.IsSuccessful)
                return;

            try
            {
                applicationContainer.Update(applicationData.Stat.ModifiedZxId, () =>
                    ApplicationNodeDataSerializer.Deserialize(applicationData.Data));
            }
            catch (Exception e)
            {
                log.Error(e, "Failed to update application info for path '{Path}'.", applicationData.Path);
            }
        }

        public void UpdateReplicas(GetChildrenResult childrenResult, ILog log)
        {
            if (childrenResult.Status == ZooKeeperStatus.NodeNotFound)
                RemoveApplication();
            if (!childrenResult.IsSuccessful)
                return;

            try
            {
                replicasContainer.Update(childrenResult.Stat.ModifiedChildrenZxId, () => 
                    UrlParser.Parse(childrenResult.ChildrenNames.Select(ServiceDiscoveryPath.Unescape)));
            }
            catch (Exception e)
            {
                log.Error(e, "Failed to update replicas for path '{Path}'.", childrenResult.Path);
            }
        }

        private ServiceTopology BuildServiceTopology()
        {
            var application = applicationContainer.Value;
            var replicas = replicasContainer.Value;

            if (application == null || replicas == null)
                return null;

            return new ServiceTopology(replicas, application.Properties);
        }

        private void RemoveApplication()
        {
            replicasContainer.Clear();
            applicationContainer.Clear();
        }
    }

    internal class VersionedContainer<T> where T : class
    {
        public volatile T Value;
        private long version = long.MinValue;
        private readonly object sync = new object();

        public void Clear()
        {
            lock (sync)
            {
                Value = null;
                version = long.MinValue;
            }
        }

        public void Update(long newVersion, Func<T> valueProvider)
        {
            if (newVersion <= version)
                return;

            lock (sync)
            {
                if (newVersion <= version)
                    return;
                Value = valueProvider();
                version = newVersion;
            }
        }

        public override string ToString() =>
            $"{nameof(Value)}: {Value}, {nameof(version)}: {version}";
    }
}