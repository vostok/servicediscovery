using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Commons.Testing;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.ServiceDiscovery.Helpers;
using Vostok.ServiceDiscovery.Models;
using Vostok.ServiceDiscovery.Serializers;
using Vostok.ZooKeeper.Client;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.LocalEnsemble;
using Vostok.ZooKeeper.Testing;

namespace Vostok.ServiceDiscovery.Tests
{
    internal abstract class TestsBase
    {
        protected static TimeSpan DefaultTimeout = 10.Seconds();
        protected readonly ILog Log = new SynchronousConsoleLog();
        protected readonly ServiceDiscoveryPathHelper pathHelper = new ServiceDiscoveryPathHelper(new ServiceBeaconSettings().ZooKeeperNodesPrefix);
        protected ZooKeeperEnsemble Ensemble;
        protected ZooKeeperClient ZooKeeperClient;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            Ensemble = ZooKeeperEnsemble.DeployNew(1, Log);
            ZooKeeperClient = GetZooKeeperClient();
        }

        [SetUp]
        public void SetUp()
        {
            if (!Ensemble.IsRunning)
                Ensemble.Start();
            ZooKeeperClient.Delete(new ServiceBeaconSettings().ZooKeeperNodesPrefix);
        }

        [TearDown]
        public void TearDown()
        {
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            Ensemble.Dispose();
        }

        protected Task KillSession(ZooKeeperClient client) =>
            ZooKeeperClientTestsHelper.KillSession(client.SessionId, client.SessionPassword, client.OnConnectionStateChanged, Ensemble.ConnectionString, DefaultTimeout);

        protected ZooKeeperClient GetZooKeeperClient()
        {
            var settings = new ZooKeeperClientSettings(Ensemble.ConnectionString) {Timeout = DefaultTimeout};
            return new ZooKeeperClient(settings, Log);
        }

        protected void WaitReplicaRegistered(ReplicaInfo replica, bool expected = true)
        {
            var wait = new Action(
                () =>
                {
                    var path = pathHelper.BuildReplicaPath(replica.Environment, replica.Application, replica.Replica);
                    var exists = ZooKeeperClient.Exists(path);
                    exists.IsSuccessful.Should().Be(true);
                    exists.Exists.Should().Be(expected);
                });

            wait.ShouldPassIn(DefaultTimeout);
        }

        protected bool ReplicaRegistered(ReplicaInfo replica)
        {
            var path = pathHelper.BuildReplicaPath(replica.Environment, replica.Application, replica.Replica);
            var exists = ZooKeeperClient.Exists(path);
            return exists.Exists;
        }

        protected void CreateEnvironmentNode(string environment, string parent = null, Dictionary<string, string> properties = null)
        {
            var info = new EnvironmentInfo(parent, properties);
            var data = EnvironmentNodeDataSerializer.Serialize(info);

            var path = pathHelper.BuildEnvironmentPath(environment);
            CreateOrUpdate(path, data);
        }

        protected void DeleteEnvironmentNode(string environment)
        {
            var path = pathHelper.BuildEnvironmentPath(environment);
            var delete = ZooKeeperClient.Delete(path);
            delete.IsSuccessful.Should().BeTrue();
        }

        protected void CreateApplicationNode(string environment, string application, Dictionary<string, string> properties = null)
        {
            var info = new ApplicationInfo(properties);
            var data = ApplicationNodeDataSerializer.Serialize(info);

            var path = pathHelper.BuildApplicationPath(environment, application);
            CreateOrUpdate(path, data);
        }

        protected void DeleteApplicationNode(string environment, string application)
        {
            var path = pathHelper.BuildApplicationPath(environment, application);
            var delete = ZooKeeperClient.Delete(path);
            delete.IsSuccessful.Should().BeTrue();
        }

        protected void CreateReplicaNode(ReplicaInfo replicaInfo)
        {
            var data = ReplicaNodeDataSerializer.Serialize(replicaInfo.Properties);
            var path = pathHelper.BuildReplicaPath(replicaInfo.Environment, replicaInfo.Application, replicaInfo.Replica);
            CreateOrUpdate(path, data);
        }

        protected void DeleteReplicaNode(ReplicaInfo replicaInfo)
        {
            var path = pathHelper.BuildReplicaPath(replicaInfo.Environment, replicaInfo.Application, replicaInfo.Replica);
            var delete = ZooKeeperClient.Delete(path);
            delete.IsSuccessful.Should().BeTrue();
        }

        protected ServiceBeacon GetServiceBeacon(ReplicaInfo replica, ZooKeeperClient client = null)
        {
            client = client ?? ZooKeeperClient;
            var settings = new ServiceBeaconSettings
            {
                IterationPeriod = 60.Seconds(),
                MinimumTimeBetweenIterations = 1.Seconds()
            };
            return new ServiceBeacon(client, replica, settings, Log);
        }

        private void CreateOrUpdate(string path, byte[] data)
        {
            var create = ZooKeeperClient.Create(path, CreateMode.Persistent, data);
            (create.Status == ZooKeeperStatus.Ok || create.Status == ZooKeeperStatus.NodeAlreadyExists).Should().BeTrue();
            if (create.Status == ZooKeeperStatus.NodeAlreadyExists)
                ZooKeeperClient.SetData(path, data).IsSuccessful.Should().BeTrue();
        }
    }
}