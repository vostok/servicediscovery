﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Commons.Testing;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.ServiceDiscovery.Abstractions;
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
        protected ActionsQueue EventsQueue;
        protected ZooKeeperEnsemble Ensemble;
        protected ZooKeeperClient ZooKeeperClient;
        protected IServiceDiscoveryManager ServiceDiscoveryManager;

        [OneTimeSetUp]
        public void OneTimeSetUp()
        {
            EventsQueue = new ActionsQueue(Log);
            Ensemble = ZooKeeperEnsemble.DeployNew(1, Log);
            ZooKeeperClient = GetZooKeeperClient();
            ServiceDiscoveryManager = new ServiceDiscoveryManager(ZooKeeperClient, new ServiceDiscoveryManagerSettings(), Log);
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
            EventsQueue.Dispose();
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

        protected void WaitForApplicationTagsExists(string environment, string application, string replica, TagCollection tags = null)
        {
            var action = new Action(() => ApplicationHasReplicaTags(environment, application, replica, tags).Should().BeTrue());
            action.ShouldPassIn(DefaultTimeout);
        }

        protected void CheckForApplicationTagsDoesNotExists(string environment, string application, string replica, TagCollection tags = null)
        {
            var action = new Action(() => ApplicationHasReplicaTags(environment, application, replica, tags).Should().BeFalse());
            action.ShouldNotFailIn(1.Seconds());
        }

        protected bool ApplicationHasReplicaTags(string environment, string application, string replica, TagCollection tags = null)
        {
            var applicationNode = ServiceDiscoveryManager.GetApplicationAsync(environment, application).GetAwaiter().GetResult();
            if (applicationNode == null)
                return false;
            var applicationProperties = applicationNode.Properties;
            var replicaTagsPropertyKey = new TagsPropertyKey(replica, ReplicaTagKind.Ephemeral.ToString()).ToString();
            var containsTagsKey = applicationProperties.ContainsKey(replicaTagsPropertyKey);
            Log.Info(containsTagsKey ? $"Tags: {applicationProperties[replicaTagsPropertyKey]}" : "No Tags");
            return containsTagsKey && (tags == null || applicationProperties[replicaTagsPropertyKey] == tags.ToString());
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
        
        protected ServiceBeacon GetServiceBeacon(ReplicaInfo replica, ZooKeeperClient client = null, Func<bool> registrationAllowedProvider = null, bool? addDependenciesToNodeData = null, IEnvironmentInfo envSettings = null)
            => GetServiceBeacon(new ServiceBeaconInfo(replica), client, registrationAllowedProvider, addDependenciesToNodeData, envSettings);

        protected ServiceBeacon GetServiceBeacon(ServiceBeaconInfo serviceBeaconInfo, ZooKeeperClient client = null, Func<bool> registrationAllowedProvider = null, bool? addDependenciesToNodeData = null, IEnvironmentInfo envSettings = null)
        {
            client = client ?? ZooKeeperClient;
            var settings = new ServiceBeaconSettings
            {
                IterationPeriod = 60.Seconds(),
                MinimumTimeBetweenIterations = 100.Milliseconds(),
                RegistrationAllowedProvider = registrationAllowedProvider,
                CreateEnvironmentIfAbsent = envSettings
            };
            if (addDependenciesToNodeData.HasValue)
                settings.AddDependenciesToNodeData = addDependenciesToNodeData.Value;
            return new ServiceBeacon(client, serviceBeaconInfo, settings, Log);
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