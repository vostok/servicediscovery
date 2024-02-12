using System;
using System.Linq;
using JetBrains.Annotations;
using Vostok.Commons.Helpers.Url;
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
    internal class ApplicationWithReplicas : IDisposable
    {
        [CanBeNull]
        public volatile ServiceTopology ServiceTopology;

        private readonly string environmentName;
        private readonly string applicationName;
        private readonly string applicationNodePath;
        private readonly VersionedContainer<ApplicationInfo> applicationContainer;
        private readonly VersionedContainer<Uri[]> replicasContainer;
        private readonly object updateServiceTopologySync = new object();

        private readonly IZooKeeperClient zooKeeperClient;
        private readonly ServiceDiscoveryPathHelper pathHelper;
        private readonly ActionsQueue eventsQueue;
        private readonly AdHocNodeWatcher nodeWatcher;
        private readonly AdHocNodeWatcher existsWatcher;
        private readonly ILog log;
        private readonly AtomicBoolean isDisposed = false;

        public ApplicationWithReplicas(
            string environmentName,
            string applicationName,
            string applicationNodePath,
            IZooKeeperClient zooKeeperClient,
            ServiceDiscoveryPathHelper pathHelper,
            ActionsQueue eventsQueue,
            bool observeNonExistentApplication,
            ILog log)
        {
            this.environmentName = environmentName;
            this.applicationName = applicationName;
            this.applicationNodePath = applicationNodePath;
            this.zooKeeperClient = zooKeeperClient;
            this.pathHelper = pathHelper;
            this.eventsQueue = eventsQueue;
            this.log = log;

            nodeWatcher = new AdHocNodeWatcher(OnNodeEvent);
            existsWatcher = observeNonExistentApplication ? nodeWatcher : null;
            applicationContainer = new VersionedContainer<ApplicationInfo>();
            replicasContainer = new VersionedContainer<Uri[]>();
        }

        public void Update(out bool appExists)
        {
            appExists = true;
            
            if (isDisposed)
                return;

            try
            {
                var applicationExists = zooKeeperClient.Exists(new ExistsRequest(applicationNodePath) {Watcher = existsWatcher});
                if (!applicationExists.IsSuccessful)
                {
                    return;
                }

                if (applicationExists.Stat == null)
                {
                    appExists = false;
                    Clear();
                    return;
                }

                if (applicationContainer.NeedUpdate(applicationExists.Stat.ModifiedZxId))
                {
                    var applicationData = zooKeeperClient.GetData(new GetDataRequest(applicationNodePath) {Watcher = nodeWatcher});
                    if (applicationData.Status == ZooKeeperStatus.NodeNotFound)
                        Clear();
                    if (!applicationData.IsSuccessful)
                        return;

                    var info = ApplicationNodeDataSerializer.Deserialize(environmentName, applicationName, applicationData.Data);
                    if (applicationContainer.Update(applicationData.Stat.ModifiedZxId, info))
                        UpdateServiceTopology();
                }

                if (replicasContainer.NeedUpdate(applicationExists.Stat.ModifiedChildrenZxId))
                {
                    var applicationChildren = zooKeeperClient.GetChildren(new GetChildrenRequest(applicationNodePath) {Watcher = nodeWatcher});
                    if (applicationChildren.Status == ZooKeeperStatus.NodeNotFound)
                        Clear();
                    if (!applicationChildren.IsSuccessful)
                        return;

                    var replicas = UrlParser.Parse(applicationChildren.ChildrenNames.Select(pathHelper.Unescape));
                    if (replicasContainer.Update(applicationChildren.Stat.ModifiedChildrenZxId, replicas))
                        UpdateServiceTopology();
                }
            }
            catch (Exception error)
            {
                log.Error(error, "Failed to update '{Application} application in '{Environment}' environment.", applicationName, environmentName);
            }
        }

        public void Dispose()
        {
            isDisposed.TrySetTrue();
        }

        private void OnNodeEvent(NodeChangedEventType type, string path)
        {
            if (isDisposed)
                return;

            eventsQueue.Enqueue(() => Update(out _));
        }

        private void Clear()
        {
            applicationContainer.Clear();
            replicasContainer.Clear();

            UpdateServiceTopology();
        }

        private void UpdateServiceTopology()
        {
            lock (updateServiceTopologySync)
            {
                ServiceTopology = ServiceTopology.Build(replicasContainer.Value, applicationContainer.Value?.Properties);
            }
        }
    }
}