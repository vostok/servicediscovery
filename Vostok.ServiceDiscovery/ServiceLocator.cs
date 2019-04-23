using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Vostok.Commons.Helpers.Url;
using Vostok.Logging.Abstractions;
using Vostok.ServiceDiscovery.Abstractions;
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

            // Note(kungurtsev): if environment info or application replicas have not initially filled due to connection errors, do not go to parent environment.
            while (environment.Info != null && environment.Replicas != null)
            {
                if (!visitedEnvironments.Add(environment.Name))
                {
                    log.Warn("Cycled when resolving environment {Environment} parents.", environmentName);
                    return null;
                }

                var goToParent = environment.Info.ParentEnvironment != null 
                                 && environment.Replicas.Length == 0 
                                 && environment.Info.SkipIfEmpty();

                if (!goToParent)
                    return new ServiceTopology(environment.Replicas, environment.Info.Properties);

                environment = GetApplicationEnvironment(environment.Info.ParentEnvironment);
            }

            return null;
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
            var getDataResult = zooKeeperClient.GetData(serviceDiscoveryPath.BuildEnvironmentPath(environment.Name));
            environment.UpdateInfo(getDataResult, log);

            var applicationPath = serviceDiscoveryPath.BuildApplicationPath(environment.Name, application);

            var existsResult = zooKeeperClient.Exists(new ExistsRequest(applicationPath) {Watcher = nodeWatcher});
            if (environment.NeedUpdateReplicas(existsResult, log))
            {
                var getChildrenResult = zooKeeperClient.GetChildren(new GetChildrenRequest(applicationPath) {Watcher = nodeWatcher});
                environment.UpdateReplicas(getChildrenResult, log);
            }

            return environment;
        }
    }

    internal class ApplicationEnvironment
    {
        public readonly string Name;
        private readonly VersionedContainer<EnvironmentInfo> infoContainer;
        private readonly VersionedContainer<Uri[]> replicasContainer;
        
        public ApplicationEnvironment(string name)
        {
            Name = name;
            infoContainer = new VersionedContainer<EnvironmentInfo>();
            replicasContainer = new VersionedContainer<Uri[]>();
        }

        public EnvironmentInfo Info => infoContainer.Value;

        public Uri[] Replicas => replicasContainer.Value;

        public void UpdateInfo(GetDataResult dataResult, ILog log)
        {
            if (!dataResult.IsSuccessful)
                return;

            try
            {
                infoContainer.Update(dataResult.Stat.ModifiedZxId, () => 
                    EnvironmentNodeDataSerializer.Deserialize(dataResult.Data));
            }
            catch (Exception e)
            {
                log.Error(e, "Failed to update environment info for path '{Path}'.", dataResult.Path);
            }
        }

        public bool NeedUpdateReplicas(ExistsResult existsResult, ILog log)
        {
            if (!existsResult.IsSuccessful)
                return false;

            try
            {
                if (existsResult.Stat == null)
                {
                    replicasContainer.Update(-1, () => null);
                    return false;
                }

                var replicasCount = existsResult.Stat.NumberOfChildren;
                var replicasZxId = existsResult.Stat.ModifiedChildrenZxId;

                if (replicasCount == 0)
                {
                    replicasContainer.Update(replicasZxId, () => new Uri[0]);
                    return false;
                }

                return replicasContainer.NeedUpdate(replicasZxId);
            }
            catch (Exception e)
            {
                log.Error(e, "Failed to check need update replicas for path '{Path}'.", existsResult.Path);
            }

            return false;
        }

        public void UpdateReplicas(GetChildrenResult childrenResult, ILog log)
        {
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
    }

    internal class VersionedContainer<T> where T : class
    {
        public volatile T Value;
        private long version = -1;
        private readonly object sync = new object();

        public void Update(long newVersion, Func<T> valueProvider)
        {
            if (!NeedUpdate(newVersion))
                return;

            lock (sync)
            {
                if (!NeedUpdate(newVersion))
                    return;
                Value = valueProvider();
                version = newVersion;
            }
        }

        public bool NeedUpdate(long newVersion)
        {
            return newVersion == -1 || newVersion > version;
        }
    }
}