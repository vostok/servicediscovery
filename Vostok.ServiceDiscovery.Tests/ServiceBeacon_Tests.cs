using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using FluentAssertions.Extensions;
using NSubstitute;
using NUnit.Framework;
using Vostok.Commons.Helpers.Observable;
using Vostok.Commons.Testing;
using Vostok.Logging.Abstractions;
using Vostok.ServiceDiscovery.Helpers;
using Vostok.ServiceDiscovery.Models;
using Vostok.ServiceDiscovery.Serializers;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

namespace Vostok.ServiceDiscovery.Tests
{
    [TestFixture]
    internal class ServiceBeacon_Tests : TestsBase
    {
        [Test]
        public void Start_should_create_node()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            CreateEnvironmentNode(replica.Environment);

            using (var beacon = GetServiceBeacon(replica))
            {
                ReplicaRegistered(replica).Should().BeFalse();

                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);

                ReplicaRegistered(replica).Should().BeTrue();
            }
        }

        [Test]
        public void Start_should_create_node_with_replica_properties()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            replica.SetProperty("key", "value");
            CreateEnvironmentNode(replica.Environment);

            using (var beacon = GetServiceBeacon(replica))
            {
                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);

                var path = new ServiceDiscoveryPathHelper(new ServiceBeaconSettings().ZooKeeperNodesPrefix)
                    .BuildReplicaPath(replica.Environment, replica.Application, replica.Replica);
                var data = ZooKeeperClient.GetData(path).Data;
                var dict = ReplicaNodeDataSerializer.Deserialize(data);

                dict["key"].Should().Be("value");
            }
        }

        [Test]
        public void Should_use_replica_builder()
        {
            var url = "https://github.com/vostok";

            CreateEnvironmentNode("default");

            using (var beacon = new ServiceBeacon(
                ZooKeeperClient,
                setup => setup.SetUrl(new Uri(url)).SetApplication("test")))
            {
                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);

                var path = new ServiceDiscoveryPathHelper(new ServiceBeaconSettings().ZooKeeperNodesPrefix)
                    .BuildReplicaPath("default", "test", url);
                var data = ZooKeeperClient.GetData(path).Data;
                var dict = ReplicaNodeDataSerializer.Deserialize(data);

                dict[ReplicaInfoKeys.Replica].Should().Be(url);
                dict[ReplicaInfoKeys.Application].Should().Be("test");
            }
        }

        [Test]
        public void Should_use_default_replica_builder()
        {
            CreateEnvironmentNode("default");

            using (var beacon = new ServiceBeacon(ZooKeeperClient))
            {
                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);

                var builder = ReplicaInfoBuilder.Build(null);
                var path = new ServiceDiscoveryPathHelper(new ServiceBeaconSettings().ZooKeeperNodesPrefix)
                    .BuildReplicaPath(builder.Environment, builder.Application, builder.Replica);
                var data = ZooKeeperClient.GetData(path).Data;
                var dict = ReplicaNodeDataSerializer.Deserialize(data);

                dict[ReplicaInfoKeys.Application].Should().Be(builder.Application);
            }
        }

        [Test]
        public void Start_should_run_iterations_loop()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");

            var calls = 0;
            var client = Substitute.For<IZooKeeperClient>();
            client.OnConnectionStateChanged.Returns(new CachingObservable<ConnectionState>(ConnectionState.Connected));
            client.ExistsAsync(Arg.Any<ExistsRequest>())
                .Returns(
                    c =>
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

                Thread.Sleep((4 * 200).Milliseconds());

                beacon.Stop();
            }

            // One call for environment, one for node.
            calls.Should().BeInRange(2 * 3, 2 * 5);
        }

        [Test]
        public void Stop_should_delete_node()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            CreateEnvironmentNode(replica.Environment);

            using (var beacon = GetServiceBeacon(replica))
            {
                ReplicaRegistered(replica).Should().BeFalse();

                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);
                ReplicaRegistered(replica).Should().BeTrue();

                beacon.Stop();

                ReplicaRegistered(replica).Should().BeFalse();
            }
        }

        [Test]
        public void Stop_should_delete_node_after_reconnect()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            CreateEnvironmentNode(replica.Environment);

            using (var beacon = GetServiceBeacon(replica))
            {
                ReplicaRegistered(replica).Should().BeFalse();

                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);
                ReplicaRegistered(replica).Should().BeTrue();

                Ensemble.Stop();

                beacon.Stop();

                Ensemble.Start();

                Action action = () =>
                {
                    ReplicaRegistered(replica).Should().BeFalse();
                };
                action.ShouldPassIn(DefaultTimeout);
            }
        }

        [Test]
        public void Dispose_should_delete_node()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            CreateEnvironmentNode(replica.Environment);

            using (var beacon = GetServiceBeacon(replica))
            {
                ReplicaRegistered(replica).Should().BeFalse();

                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);
                ReplicaRegistered(replica).Should().BeTrue();
            }

            ReplicaRegistered(replica).Should().BeFalse();
        }

        [Test]
        public void Should_not_be_triggered_after_dispose()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            CreateEnvironmentNode(replica.Environment);

            using (var beacon = GetServiceBeacon(replica))
            {
                ReplicaRegistered(replica).Should().BeFalse();

                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);
                ReplicaRegistered(replica).Should().BeTrue();

                beacon.Dispose();
                ReplicaRegistered(replica).Should().BeFalse();

                CreateReplicaNode(replica);
                DeleteReplicaNode(replica);
                Thread.Sleep(1.Seconds());

                ReplicaRegistered(replica).Should().BeFalse();
            }
        }

        [Test]
        public void Should_be_startable_and_stoppable_multiple_times()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            CreateEnvironmentNode(replica.Environment);

            using (var beacon = GetServiceBeacon(replica))
            {
                for (var times = 0; times < 3; times++)
                {
                    beacon.Start();
                    beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);
                    ReplicaRegistered(replica).Should().BeTrue();

                    beacon.Stop();
                    ReplicaRegistered(replica).Should().BeFalse();
                }
            }
        }

        [Test]
        public void Should_not_throw_if_someone_else_dispose_zookeeper_client_before_start()
        {
            var disposedClient = GetZooKeeperClient();
            disposedClient.Dispose();

            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");

            var t = Task.Run(
                () =>
                {
                    using (var beacon = new ServiceBeacon(disposedClient, replica, null, Log))
                    {
                        beacon.Start();
                    }
                });

            t.ShouldCompleteIn(DefaultTimeout);
        }

        [Test]
        public void Should_not_throw_if_someone_else_dispose_zookeeper_client_after_start()
        {
            var disposedClient = GetZooKeeperClient();

            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            CreateEnvironmentNode(replica.Environment);

            using (var beacon = new ServiceBeacon(disposedClient, replica, null, Log))
            {
                ReplicaRegistered(replica).Should().BeFalse();

                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);
                ReplicaRegistered(replica).Should().BeTrue();

                disposedClient.Dispose();

                beacon.Stop();
            }
        }

        [Test]
        public void Should_create_node_immediately_after_ensemble_start()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            CreateEnvironmentNode(replica.Environment);
            Ensemble.Stop();

            using (var beacon = GetServiceBeacon(replica))
            {
                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldNotCompleteIn(1.Seconds());

                Ensemble.Start();

                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);
                ReplicaRegistered(replica).Should().BeTrue();
            }
        }

        [Test]
        public void Should_create_node_immediately_after_ensemble_restart()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            CreateEnvironmentNode(replica.Environment);

            using (var beacon = GetServiceBeacon(replica))
            {
                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);
                ReplicaRegistered(replica).Should().BeTrue();

                Ensemble.Stop();

                Ensemble.Start();

                WaitReplicaRegistered(replica);
            }
        }

        [Test]
        public void Should_create_node_immediately_after_session_expire()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            CreateEnvironmentNode(replica.Environment);

            using (var beacon = GetServiceBeacon(replica))
            {
                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);
                ReplicaRegistered(replica).Should().BeTrue();

                KillSession(ZooKeeperClient).Wait();

                WaitReplicaRegistered(replica);
            }
        }

        [Test]
        public void Should_create_node_immediately_after_environment_node_created()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");

            using (var beacon = GetServiceBeacon(replica))
            {
                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldNotCompleteIn(0.5.Seconds());
                ReplicaRegistered(replica).Should().BeFalse();

                for (var times = 0; times < 3; times++)
                {
                    CreateEnvironmentNode(replica.Environment);

                    WaitReplicaRegistered(replica);

                    DeleteEnvironmentNode(replica.Environment);

                    // Note(kungurtsev): can be true, if ServiceBeacon iteration in progress
                    ReplicaRegistered(replica).Should().BeFalse();
                }
            }
        }

        [Test]
        public void Should_create_node_immediately_after_replica_node_deleted_by_someone_else()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            CreateEnvironmentNode(replica.Environment);

            using (var beacon = GetServiceBeacon(replica))
            {
                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);
                ReplicaRegistered(replica).Should().BeTrue();

                for (var times = 0; times < 3; times++)
                {
                    DeleteReplicaNode(replica);

                    WaitReplicaRegistered(replica);
                }
            }
        }

        [Test]
        public void Should_create_node_immediately_after_application_node_deleted()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            CreateEnvironmentNode(replica.Environment);

            using (var beacon = GetServiceBeacon(replica))
            {
                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);
                ReplicaRegistered(replica).Should().BeTrue();

                for (var times = 0; times < 3; times++)
                {
                    DeleteApplicationNode(replica.Environment, replica.Application);

                    WaitReplicaRegistered(replica);
                }
            }
        }

        [Test]
        public void Should_register_multiple_replicas_in_one_application()
        {
            var replicas = Enumerable.Range(0, 3)
                .Select(i => new ReplicaInfo("default", "vostok", $"app_{i}"))
                .ToList();
            CreateEnvironmentNode(replicas.First().Environment);

            using (var beacon1 = GetServiceBeacon(replicas[0]))
            using (var beacon2 = GetServiceBeacon(replicas[1]))
            using (var beacon3 = GetServiceBeacon(replicas[2]))
            {
                var beacons = new List<ServiceBeacon> {beacon1, beacon2, beacon3};

                foreach (var beacon in beacons)
                    beacon.Start();

                foreach (var beacon in beacons)
                    beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);

                foreach (var replica in replicas)
                    ReplicaRegistered(replica).Should().BeTrue();
            }
        }

        [Test]
        public void Should_register_same_replicas_in_different_environments()
        {
            var replicas = Enumerable.Range(0, 3)
                .Select(i => new ReplicaInfo($"environment_{i}", "vostok", "https://github.com/vostok"))
                .ToList();

            foreach (var replica in replicas)
                CreateEnvironmentNode(replica.Environment);

            using (var beacon1 = GetServiceBeacon(replicas[0]))
            using (var beacon2 = GetServiceBeacon(replicas[1]))
            using (var beacon3 = GetServiceBeacon(replicas[2]))
            {
                var beacons = new List<ServiceBeacon> {beacon1, beacon2, beacon3};

                foreach (var beacon in beacons)
                    beacon.Start();

                foreach (var beacon in beacons)
                    beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);

                foreach (var replica in replicas)
                    ReplicaRegistered(replica).Should().BeTrue();
            }
        }

        [Test]
        public void Should_register_same_replicas_in_different_applications()
        {
            var replicas = Enumerable.Range(0, 3)
                .Select(i => new ReplicaInfo("environment", $"vostok_{i}", "https://github.com/vostok"))
                .ToList();

            CreateEnvironmentNode(replicas.First().Environment);

            using (var beacon1 = GetServiceBeacon(replicas[0]))
            using (var beacon2 = GetServiceBeacon(replicas[1]))
            using (var beacon3 = GetServiceBeacon(replicas[2]))
            {
                var beacons = new List<ServiceBeacon> {beacon1, beacon2, beacon3};

                foreach (var beacon in beacons)
                    beacon.Start();

                foreach (var beacon in beacons)
                    beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);

                foreach (var replica in replicas)
                    ReplicaRegistered(replica).Should().BeTrue();
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Should_register_one_replica_for_multiple_beacons(bool swapStops)
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            CreateEnvironmentNode(replica.Environment);

            using (var beacon1 = GetServiceBeacon(replica))
            using (var beacon2 = GetServiceBeacon(replica))
            {
                ReplicaRegistered(replica).Should().BeFalse();

                beacon1.Start();
                beacon1.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);

                beacon2.Start();
                beacon2.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);

                ReplicaRegistered(replica).Should().BeTrue();

                if (swapStops)
                {
                    beacon1.Stop();
                    WaitReplicaRegistered(replica);

                    beacon2.Stop();
                    ReplicaRegistered(replica).Should().BeFalse();
                }
                else
                {
                    beacon2.Stop();
                    WaitReplicaRegistered(replica);

                    beacon1.Stop();
                    ReplicaRegistered(replica).Should().BeFalse();
                }
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void Should_register_one_replica_for_multiple_zookeeper_clients(bool swapStops)
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            CreateEnvironmentNode(replica.Environment);

            using (var client1 = GetZooKeeperClient())
            using (var client2 = GetZooKeeperClient())
            using (var beacon1 = GetServiceBeacon(replica, client1))
            using (var beacon2 = GetServiceBeacon(replica, client2))
            {
                ReplicaRegistered(replica).Should().BeFalse();

                beacon1.Start();
                beacon1.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);

                beacon2.Start();
                beacon2.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);

                ReplicaRegistered(replica).Should().BeTrue();

                if (swapStops)
                {
                    beacon1.Stop();
                    WaitReplicaRegistered(replica);

                    beacon2.Stop();
                    ReplicaRegistered(replica).Should().BeFalse();
                }
                else
                {
                    beacon2.Stop();
                    WaitReplicaRegistered(replica);

                    beacon1.Stop();
                    ReplicaRegistered(replica).Should().BeFalse();
                }
            }
        }

        [Test]
        public void Should_not_throw_immediately_disposed()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");

            using (GetServiceBeacon(replica))
            {
            }
        }
    }
}