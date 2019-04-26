using System;
using System.Linq;
using Vostok.Commons.Helpers.Url;
using Vostok.Logging.Abstractions;
using Vostok.ServiceDiscovery.Helpers;
using Vostok.ServiceDiscovery.Models;
using Vostok.ServiceDiscovery.Serializers;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

namespace Vostok.ServiceDiscovery.ServiceLocatorStorage
{
    internal class ApplicationEnvironment
    {
        public readonly string Name;
        private readonly VersionedContainer<EnvironmentInfo> environmentContainer;
        private readonly VersionedContainer<ApplicationInfo> applicationContainer;
        private readonly VersionedContainer<Uri[]> replicasContainer;
        public ServiceTopology ServiceTopology;

        public ApplicationEnvironment(string name)
        {
            Name = name;
            environmentContainer = new VersionedContainer<EnvironmentInfo>();
            applicationContainer = new VersionedContainer<ApplicationInfo>();
            replicasContainer = new VersionedContainer<Uri[]>();
        }

        public EnvironmentInfo Environment => environmentContainer.Value;

        public void UpdateEnvironment(GetDataResult environmentData, ILog log)
        {
            if (environmentData.Status == ZooKeeperStatus.NodeNotFound)
                RemoveEnvironment();
            if (!environmentData.IsSuccessful)
                return;

            try
            {
                environmentContainer.Update(
                    environmentData.Stat.ModifiedZxId,
                    () =>
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
                if (applicationContainer.Update(
                    applicationData.Stat.ModifiedZxId,
                    () =>
                        ApplicationNodeDataSerializer.Deserialize(applicationData.Data))
                )
                {
                    UpdateServiceTopology();
                }
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
                if (replicasContainer.Update(
                    childrenResult.Stat.ModifiedChildrenZxId,
                    () =>
                        UrlParser.Parse(childrenResult.ChildrenNames.Select(ServiceDiscoveryPathHelper.Unescape)))
                )
                {
                    UpdateServiceTopology();
                }
            }
            catch (Exception e)
            {
                log.Error(e, "Failed to update replicas for path '{Path}'.", childrenResult.Path);
            }
        }

        private void RemoveEnvironment()
        {
            environmentContainer.Clear();
            RemoveApplication();
        }

        private void RemoveApplication()
        {
            applicationContainer.Clear();
            replicasContainer.Clear();

            UpdateServiceTopology();
        }

        private void UpdateServiceTopology()
        {
            ServiceTopology = ServiceTopology.Build(replicasContainer.Value, applicationContainer.Value?.Properties);
        }
    }
}