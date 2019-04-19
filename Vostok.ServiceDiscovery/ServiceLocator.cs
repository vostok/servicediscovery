using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using JetBrains.Annotations;
using Vostok.Logging.Abstractions;
using Vostok.ServiceDiscovery.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
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
            var environments = applications.GetOrAdd(application, a => new ApplicationEnvironments(a, zooKeeperClient, serviceDiscoveryPath, log));
            return environments.Locate(environment);
        }

        public void Dispose()
        {
            
        }

        private void OnNodeEvent(NodeChangedEventType type, string path)
        {
        }
    }

    internal class ApplicationEnvironments
    {
        private readonly string application;
        private readonly IZooKeeperClient zooKeeperClient;
        private readonly ServiceDiscoveryPath serviceDiscoveryPath;
        private readonly ILog log;
        private readonly ConcurrentDictionary<string, ApplicationEnvironment> environments = new ConcurrentDictionary<string, ApplicationEnvironment>();

        public ApplicationEnvironments(string application, IZooKeeperClient zooKeeperClient, ServiceDiscoveryPath serviceDiscoveryPath, ILog log)
        {
            this.application = application;
            this.zooKeeperClient = zooKeeperClient;
            this.serviceDiscoveryPath = serviceDiscoveryPath;
            this.log = log;
        }

        public IServiceTopology Locate(string environmentName)
        {
            var environment = GetApplicationEnvironment(environmentName);

            var visitedEnvironments = new HashSet<string>();

            // Note(kungurtsev): if environment info or application replicas have not initially filled due to connection errors, do not go to parent environment.
            while (environment.Info?.ParentEnvironment != null && environment.Replicas != null)
            {
                if (!visitedEnvironments.Add(environment.Name))
                {
                    log.Warn("Cycled when resolving environment {Environment} parents.", environmentName);
                    return null;
                }

                var goToParent = environment.Replicas.Length == 0 && environment.Info.IgnoreEmptyTopologies();

                if (!goToParent)
                    return new ServiceTopology(environment.Replicas, environment.Info.Properties);

                environment = GetApplicationEnvironment(environment.Info.ParentEnvironment);
            }

            return null;
        }

        private ApplicationEnvironment GetApplicationEnvironment(string environment)
        {
            return environments.GetOrAdd(environment, e => UpdateApplicationEnvironment(new ApplicationEnvironment(e)));
        }

        private ApplicationEnvironment UpdateApplicationEnvironment(ApplicationEnvironment environment)
        {
            var envitonmentDataResult = zooKeeperClient.GetData(serviceDiscoveryPath.BuildEnvironmentPath(environment.Name));
            environment.UpdateInfo(envitonmentDataResult, log);
            
            var getChildrenDataResult = zooKeeperClient.GetChildren(serviceDiscoveryPath.BuildApplicationPath(environment.Name, application));
            environment.UpdateReplicas(getChildrenDataResult, log);

            return environment;
        }
    }

    internal class ApplicationEnvironment
    {
        public readonly string Name;
        public EnvironmentInfo Info;
        public Uri[] Replicas;

        public ApplicationEnvironment(string name)
        {
            Name = name;
        }

        public void UpdateInfo(GetDataResult dataResult, ILog log)
        {
            if (!dataResult.IsSuccessful)
                return;

            try
            {
                // TODO(kungurtsev): add zxid
                Info = EnvironmentNodeDataSerializer.Deserialize(dataResult.Data);
            }
            catch (Exception e)
            {
                log.Error(e, "Failed to deserialize environment info from path '{Path}'", dataResult.Path);
                Info = new EnvironmentInfo(null, null);
            }
        }

        public void UpdateReplicas(GetChildrenResult childrenResult, ILog log)
        {
            if (!childrenResult.IsSuccessful)
                return;

            try
            {
                // TODO(kungurtsev): add zxid
                Replicas = ParseReplicas(childrenResult.ChildrenNames);
            }
            catch (Exception e)
            {
                log.Error(e, "Failed to parse replica names from path '{Path}'", childrenResult.Path);
                Replicas = new Uri[0];
            }
        }

        private static Uri[] ParseReplicas(IEnumerable<string> replicas)
        {
            var result = new List<Uri>();
            foreach (var replica in replicas)
            {
                if (!Uri.TryCreate(replica, UriKind.Absolute, out var parsedUri))
                    continue;
                result.Add(parsedUri);
            }
            return result.ToArray();
        }
    }
}