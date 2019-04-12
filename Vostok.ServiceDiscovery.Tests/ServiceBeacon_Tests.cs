using FluentAssertions;
using NUnit.Framework;
using Vostok.Commons.Testing;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;

namespace Vostok.ServiceDiscovery.Tests
{
    [TestFixture]
    internal class ServiceBeacon_Tests : TestsBase
    {
        [TearDown]
        public void TearDown()
        {
            ZooKeeperClient.Delete(new ServiceBeaconSettings().ZooKeeperNodePath);
        }

        [Test]
        public void EnsureNodeExists_should_create_node()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");

            using (var beacon = GetServiceBeacon(replica))
            {
                CreateEnvironmentNode(replica.Environment);

                ReplicaRegistered(replica).Should().BeFalse();

                beacon.Start();
                beacon.WaitForRegistration().ShouldCompleteIn(DefaultTimeout);

                ReplicaRegistered(replica).Should().BeTrue();
            }
        }

        [Test]
        public void EnsureNodeExists_should_create_node_with_replica_properties()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            replica.AddProperty("key", "value");

            using (var beacon = GetServiceBeacon(replica))
            {
                CreateEnvironmentNode(replica.Environment);

                beacon.Start();
                beacon.WaitForRegistration().ShouldCompleteIn(DefaultTimeout);

                var path = new PathBuilder(new ServiceBeaconSettings().ZooKeeperNodePath).BuildReplicaPath(replica.Environment, replica.Service, replica.Replica);
                var data = ZooKeeperClient.GetData(path).Data;
                var dict = NodeDataSerializer.Deserialize(data);

                dict["key"].Should().Be("value");
            }
        }

        private bool ReplicaRegistered(ReplicaInfo replica)
        {
            return ReplicaRegistered(new ServiceBeaconSettings().ZooKeeperNodePath, replica);
        }

        private bool ReplicaRegistered(string prefix, ReplicaInfo replica)
        {
            var path = new PathBuilder(prefix).BuildReplicaPath(replica.Environment, replica.Service, replica.Replica);
            var exists = ZooKeeperClient.Exists(path);
            return exists.Exists;
        }

        private void CreateEnvironmentNode(string environment)
        {
            CreateEnvironmentNode(new ServiceBeaconSettings().ZooKeeperNodePath, environment);
        }

        private void CreateEnvironmentNode(string prefix, string environment)
        {
            var path = new PathBuilder(prefix).BuildEnvironmentPath(environment);
            var create = ZooKeeperClient.Create(path, CreateMode.Persistent);
            (create.Status == ZooKeeperStatus.Ok || create.Status == ZooKeeperStatus.NodeAlreadyExists).Should().BeTrue();
        }

        private ServiceBeacon GetServiceBeacon(ReplicaInfo replica)
        {
            return new ServiceBeacon(ZooKeeperClient, replica, null, Log);
        }
    }
}