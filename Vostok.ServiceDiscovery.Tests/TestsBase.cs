using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Commons.Testing;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.ServiceDiscovery.Abstractions.Models;
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
        protected readonly ServiceDiscoveryPathHelper PathHelper = new ServiceDiscoveryPathHelper(new ServiceBeaconSettings().ZooKeeperNodesPrefix, ZooKeeperPathEscaper.Instance);
        protected NodeEventsHandler EventsHandler;
        protected ZooKeeperEnsemble Ensemble;
        protected ZooKeeperClient ZooKeeperClient;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            EventsHandler = new NodeEventsHandler();
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
            EventsHandler.Dispose();
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
            WaitNodeExists(PathHelper.BuildReplicaPath(replica.Environment, replica.Application, replica.Replica), expected);
        }

        protected void WaitNodeExists(string path, bool expected = true)
        {
            var wait = new Action(
                () =>
                {
                    var exists = ZooKeeperClient.Exists(path);
                    exists.IsSuccessful.Should().Be(true);
                    exists.Exists.Should().Be(expected);
                });

            wait.ShouldPassIn(DefaultTimeout);
        }

        protected bool ReplicaRegistered(ReplicaInfo replica)
        {
            var path = PathHelper.BuildReplicaPath(replica.Environment, replica.Application, replica.Replica);
            var exists = ZooKeeperClient.Exists(path);
            return exists.Exists;
        }

        protected void CreateEnvironmentNode(string environment, string parent = null, IReadOnlyDictionary<string, string> properties = null)
        {
            var info = new EnvironmentInfo(environment, parent, properties);
            var data = EnvironmentNodeDataSerializer.Serialize(info);

            var path = PathHelper.BuildEnvironmentPath(environment);
            CreateOrUpdate(path, data);
        }

        protected void DeleteEnvironmentNode(string environment)
        {
            var path = PathHelper.BuildEnvironmentPath(environment);
            var delete = ZooKeeperClient.Delete(path);
            delete.IsSuccessful.Should().BeTrue();
        }

        protected void CreateApplicationNode(string environment, string application, IReadOnlyDictionary<string, string> properties = null)
        {
            var info = new ApplicationInfo(environment, application, properties);
            var data = ApplicationNodeDataSerializer.Serialize(info);

            var path = PathHelper.BuildApplicationPath(environment, application);
            CreateOrUpdate(path, data);
        }

        protected void DeleteApplicationNode(string environment, string application)
        {
            var path = PathHelper.BuildApplicationPath(environment, application);
            var delete = ZooKeeperClient.Delete(path);
            delete.IsSuccessful.Should().BeTrue();
        }

        protected void CreateReplicaNode(ReplicaInfo replicaInfo, bool persistent = true)
        {
            var data = ReplicaNodeDataSerializer.Serialize(replicaInfo);
            var path = PathHelper.BuildReplicaPath(replicaInfo.Environment, replicaInfo.Application, replicaInfo.Replica);
            CreateOrUpdate(path, data, persistent);
        }

        protected void DeleteReplicaNode(ReplicaInfo replicaInfo)
        {
            var path = PathHelper.BuildReplicaPath(replicaInfo.Environment, replicaInfo.Application, replicaInfo.Replica);
            var delete = ZooKeeperClient.Delete(path);
            delete.IsSuccessful.Should().BeTrue();
        }

        protected ServiceBeacon GetServiceBeacon(ReplicaInfo replica, ZooKeeperClient client = null, Func<bool> registrationAllowedProvider = null)
        {
            client = client ?? ZooKeeperClient;
            var settings = new ServiceBeaconSettings
            {
                IterationPeriod = 60.Seconds(),
                MinimumTimeBetweenIterations = 100.Milliseconds(),
                RegistrationAllowedProvider = registrationAllowedProvider
            };
            return new ServiceBeacon(client, replica, settings, Log);
        }

        private void CreateOrUpdate(string path, byte[] data, bool persistent = true)
        {
            var create = ZooKeeperClient.Create(path, persistent ? CreateMode.Persistent : CreateMode.Ephemeral, data);
            (create.Status == ZooKeeperStatus.Ok || create.Status == ZooKeeperStatus.NodeAlreadyExists).Should().BeTrue();
            if (create.Status == ZooKeeperStatus.NodeAlreadyExists)
                ZooKeeperClient.SetData(path, data).IsSuccessful.Should().BeTrue();
        }
    }
}