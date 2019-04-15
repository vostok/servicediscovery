using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NSubstitute;
using NUnit.Framework;
using Vostok.Commons.Helpers.Observable;
using Vostok.Commons.Testing;
using Vostok.Logging.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

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
        public void Start_should_create_node()
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
        public void Start_should_create_node_with_replica_properties()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            replica.AddProperty("key", "value");

            using (var beacon = GetServiceBeacon(replica))
            {
                CreateEnvironmentNode(replica.Environment);

                beacon.Start();
                beacon.WaitForRegistration().ShouldCompleteIn(DefaultTimeout);

                var path = new PathBuilder(new ServiceBeaconSettings().ZooKeeperNodePath).BuildReplicaPath(replica.Environment, replica.Application, replica.Replica);
                var data = ZooKeeperClient.GetData(path).Data;
                var dict = NodeDataSerializer.Deserialize(data);

                dict["key"].Should().Be("value");
            }
        }

        [Test]
        public void Start_should_run_iterations_loop()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");

            var calls = 0;
            var client = Substitute.For<IZooKeeperClient>();
            client.OnConnectionStateChanged.Returns(new CachingObservable<ConnectionState>(ConnectionState.Connected));
            client.ExistsAsync(Arg.Any<ExistsRequest>()).Returns(c =>
            {
                Log.Info($"{c.Args()[0]}");
                Interlocked.Increment(ref calls);
                return Task.FromResult(ExistsResult.Successful("", new NodeStat(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0)));
            });
            client.DeleteAsync(Arg.Any<DeleteRequest>()).Returns(Task.FromResult(DeleteResult.Successful("")));

            var settings = new ServiceBeaconSettings
            {
                IterationPeriod = 200.Milliseconds(),
                MinimumTimeBetweenIterations = 0.Milliseconds()
            };

            using (var beacon = new ServiceBeacon(client, replica, settings, Log))
            {
                beacon.Start();

                Thread.Sleep((4*200).Milliseconds());

                beacon.Stop();
            }

            // One call for environment, one for node.
            calls.Should().BeInRange(2*3, 2*5);
        }

        [Test]
        public void Stop_should_delete_node()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");

            using (var beacon = GetServiceBeacon(replica))
            {
                CreateEnvironmentNode(replica.Environment);

                ReplicaRegistered(replica).Should().BeFalse();

                beacon.Start();
                beacon.WaitForRegistration().ShouldCompleteIn(DefaultTimeout);

                ReplicaRegistered(replica).Should().BeTrue();

                beacon.Stop();

                ReplicaRegistered(replica).Should().BeFalse();
            }
        }

        [Test]
        public void Dispose_should_delete_node()
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

            ReplicaRegistered(replica).Should().BeFalse();
        }

        [Test]
        public void Should_be_startable_and_stoppable_multiple_times()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");

            using (var beacon = GetServiceBeacon(replica))
            {
                CreateEnvironmentNode(replica.Environment);

                for (var times = 0; times < 5; times++)
                {
                    beacon.Start();
                    beacon.WaitForRegistration().ShouldCompleteIn(DefaultTimeout);
                    ReplicaRegistered(replica).Should().BeTrue();

                    beacon.Stop();
                    ReplicaRegistered(replica).Should().BeFalse();
                }
            }
        }

        [Test]
        public void Should_not_throw_if_someone_else_dispose_zookeeper_client()
        {
            var disposedClient = GetZooKeeperClient();

            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");

            using (var beacon = new ServiceBeacon(disposedClient, replica, null, Log))
            {
                CreateEnvironmentNode(replica.Environment);

                ReplicaRegistered(replica).Should().BeFalse();

                beacon.Start();
                beacon.WaitForRegistration().ShouldCompleteIn(DefaultTimeout);

                ReplicaRegistered(replica).Should().BeTrue();

                disposedClient.Dispose();

                beacon.Stop();
            }
        }

        private bool ReplicaRegistered(ReplicaInfo replica)
        {
            return ReplicaRegistered(new ServiceBeaconSettings().ZooKeeperNodePath, replica);
        }

        private bool ReplicaRegistered(string prefix, ReplicaInfo replica)
        {
            var path = new PathBuilder(prefix).BuildReplicaPath(replica.Environment, replica.Application, replica.Replica);
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
            var settings = new ServiceBeaconSettings
            {
                IterationPeriod = 5.Seconds(),
                MinimumTimeBetweenIterations = 1.Seconds()
            };
            return new ServiceBeacon(ZooKeeperClient, replica, settings, Log);
        }
    }
}