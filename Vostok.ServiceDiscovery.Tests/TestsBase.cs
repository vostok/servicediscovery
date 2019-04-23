using System;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Commons.Testing;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
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
        protected readonly ServiceDiscoveryPath ServiceDiscoveryPath = new ServiceDiscoveryPath(new ServiceBeaconSettings().ZooKeeperNodePath);
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
        }

        [TearDown]
        public void TearDown()
        {
            ZooKeeperClient.Delete(ServiceDiscoveryPath.Prefix);
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
                    var path = ServiceDiscoveryPath.BuildReplicaPath(replica.Environment, replica.Application, replica.Replica);
                    var exists = ZooKeeperClient.Exists(path);
                    exists.IsSuccessful.Should().Be(true);
                    exists.Exists.Should().Be(expected);
                });

            wait.ShouldPassIn(DefaultTimeout);
        }

        protected bool ReplicaRegistered(ReplicaInfo replica)
        {
            var path = ServiceDiscoveryPath.BuildReplicaPath(replica.Environment, replica.Application, replica.Replica);
            var exists = ZooKeeperClient.Exists(path);
            return exists.Exists;
        }

        protected void CreateEnvironmentNode(string environment)
        {
            var path = ServiceDiscoveryPath.BuildEnvironmentPath(environment);
            var create = ZooKeeperClient.Create(path, CreateMode.Persistent);
            (create.Status == ZooKeeperStatus.Ok || create.Status == ZooKeeperStatus.NodeAlreadyExists).Should().BeTrue();
        }

        protected void DeleteEnvironmentNode(string environment)
        {
            var path = ServiceDiscoveryPath.BuildEnvironmentPath(environment);
            var delete = ZooKeeperClient.Delete(path);
            delete.IsSuccessful.Should().BeTrue();
        }

        protected void DeleteApplicationNode(ReplicaInfo replicaInfo)
        {
            var path = ServiceDiscoveryPath.BuildApplicationPath(replicaInfo.Environment, replicaInfo.Application);
            var delete = ZooKeeperClient.Delete(path);
            delete.IsSuccessful.Should().BeTrue();
        }

        protected void DeleteReplicaNode(ReplicaInfo replicaInfo)
        {
            var path = ServiceDiscoveryPath.BuildReplicaPath(replicaInfo.Environment, replicaInfo.Application, replicaInfo.Replica);
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
    }
}