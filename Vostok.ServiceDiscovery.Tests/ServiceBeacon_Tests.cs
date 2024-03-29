﻿using System;
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
using Vostok.ServiceDiscovery.Abstractions.Models;
using Vostok.ServiceDiscovery.Helpers;
using Vostok.ServiceDiscovery.Models;
using Vostok.ServiceDiscovery.Serializers;
using Vostok.ServiceDiscovery.Telemetry;
using Vostok.ServiceDiscovery.Telemetry.Event;
using Vostok.ServiceDiscovery.Telemetry.EventsSender;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;
using Vostok.ZooKeeper.Client.Abstractions.Model.Result;

// ReSharper disable AccessToModifiedClosure

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

            using (var beacon = GetServiceBeacon(new ServiceBeaconInfo(replica)))
            {
                ReplicaRegistered(replica).Should().BeFalse();

                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);

                ReplicaRegistered(replica).Should().BeTrue();
            }
        }

        [Test]
        public void Start_should_create_node_with_replica_properties_and_tags()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            var serviceBeaconInfo = new ServiceBeaconInfo(replica, new TagCollection {"tag1", "tag2"});
            replica.SetProperty("key", "value");
            CreateEnvironmentNode(replica.Environment);

            using (var beacon = GetServiceBeacon(serviceBeaconInfo))
            {
                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);

                var path = new ServiceDiscoveryPathHelper(new ServiceBeaconSettings().ZooKeeperNodesPrefix, ZooKeeperPathEscaper.Instance)
                    .BuildReplicaPath(replica.Environment, replica.Application, replica.Replica);
                var data = ZooKeeperClient.GetData(path).Data;
                var dict = ReplicaNodeDataSerializer.Deserialize(replica.Environment, replica.Application, replica.Replica, data).Properties;
                WaitForApplicationTagsExists(replica.Environment, replica.Application, replica.Replica, serviceBeaconInfo.Tags);

                dict["key"].Should().Be("value");
            }
        }

        [Test]
        public void Start_should_create_node_with_replica_properties_and_filtered_Dependencies_by_default()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            replica.SetProperty("key", "value");
            replica.SetProperty(ReplicaInfoKeys.Dependencies, "lalala");
            CreateEnvironmentNode(replica.Environment);

            using (var beacon = GetServiceBeacon(replica))
            {
                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);

                var path = new ServiceDiscoveryPathHelper(new ServiceBeaconSettings().ZooKeeperNodesPrefix, ZooKeeperPathEscaper.Instance)
                    .BuildReplicaPath(replica.Environment, replica.Application, replica.Replica);
                var data = ZooKeeperClient.GetData(path).Data;
                var dict = ReplicaNodeDataSerializer.Deserialize(replica.Environment, replica.Application, replica.Replica, data).Properties;

                dict.ContainsKey(ReplicaInfoKeys.Dependencies).Should().BeFalse();
            }
        }

        [Test]
        public void Start_should_create_node_with_replica_properties_with_Dependencies_is_ask_in_settings()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            replica.SetProperty("key", "value");
            replica.SetProperty(ReplicaInfoKeys.Dependencies, "lalala");
            CreateEnvironmentNode(replica.Environment);

            using (var beacon = GetServiceBeacon(replica, addDependenciesToNodeData: true))
            {
                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);

                var path = new ServiceDiscoveryPathHelper(new ServiceBeaconSettings().ZooKeeperNodesPrefix, ZooKeeperPathEscaper.Instance)
                    .BuildReplicaPath(replica.Environment, replica.Application, replica.Replica);
                var data = ZooKeeperClient.GetData(path).Data;
                var dict = ReplicaNodeDataSerializer.Deserialize(replica.Environment, replica.Application, replica.Replica, data).Properties;

                dict["key"].Should().Be("value");
                dict[ReplicaInfoKeys.Dependencies].Should().Be("lalala");
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

                var path = new ServiceDiscoveryPathHelper(new ServiceBeaconSettings().ZooKeeperNodesPrefix, ZooKeeperPathEscaper.Instance)
                    .BuildReplicaPath("default", "test", url);
                var data = ZooKeeperClient.GetData(path).Data;
                var dict = ReplicaNodeDataSerializer.Deserialize("default", "test", url, data).Properties;

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

                var builder = ReplicaInfoBuilder.Build(null, true).ReplicaInfo;
                var path = new ServiceDiscoveryPathHelper(new ServiceBeaconSettings().ZooKeeperNodesPrefix, ZooKeeperPathEscaper.Instance)
                    .BuildReplicaPath(builder.Environment, builder.Application, builder.Replica);
                var data = ZooKeeperClient.GetData(path).Data;
                var dict = ReplicaNodeDataSerializer.Deserialize(builder.Environment, builder.Application, builder.Replica, data).Properties;

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

            using (var beacon = new ServiceBeacon(client, new ServiceBeaconInfo(replica), settings, Log))
            {
                beacon.Start();

                Thread.Sleep((4 * 200).Milliseconds());

                beacon.Stop();
            }

            // One call for environment, one for node.
            calls.Should().BeInRange(2 * 2, 2 * 10);
        }

        [Test]
        public void Stop_should_delete_node_and_tags()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            var serviceBeaconInfo = new ServiceBeaconInfo(replica, new TagCollection {"tag1", "tag2"});
            CreateEnvironmentNode(replica.Environment);

            using (var beacon = GetServiceBeacon(serviceBeaconInfo))
            {
                ReplicaRegistered(replica).Should().BeFalse();
                CheckForApplicationTagsDoesNotExists(replica.Environment, replica.Application, replica.Replica);

                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);
                ReplicaRegistered(replica).Should().BeTrue();
                WaitForApplicationTagsExists(replica.Environment, replica.Application, replica.Replica, serviceBeaconInfo.Tags);

                beacon.Stop();

                ReplicaRegistered(replica).Should().BeFalse();
                CheckForApplicationTagsDoesNotExists(replica.Environment, replica.Application, replica.Replica);
            }
        }

        [Test]
        public void Stop_should_delete_node_and_not_delete_tags_after_reconnect()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            var serviceBeaconInfo = new ServiceBeaconInfo(replica, new TagCollection {"tag1", "tag2"});
            CreateEnvironmentNode(replica.Environment);

            using (var beacon = GetServiceBeacon(serviceBeaconInfo))
            {
                ReplicaRegistered(replica).Should().BeFalse();
                CheckForApplicationTagsDoesNotExists(replica.Environment, replica.Application, replica.Replica);

                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);
                ReplicaRegistered(replica).Should().BeTrue();
                WaitForApplicationTagsExists(replica.Environment, replica.Application, replica.Replica, serviceBeaconInfo.Tags);

                Ensemble.Stop();

                beacon.Stop();

                Ensemble.Start();

                WaitReplicaRegistered(replica, false);
                WaitForApplicationTagsExists(replica.Environment, replica.Application, replica.Replica);
            }
        }

        [Test]
        public void Dispose_should_delete_node()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            var serviceBeaconInfo = new ServiceBeaconInfo(replica, new TagCollection {"tag1", "tag2"});
            CreateEnvironmentNode(replica.Environment);

            using (var beacon = GetServiceBeacon(serviceBeaconInfo))
            {
                ReplicaRegistered(replica).Should().BeFalse();

                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);
                ReplicaRegistered(replica).Should().BeTrue();
            }

            ReplicaRegistered(replica).Should().BeFalse();
            CheckForApplicationTagsDoesNotExists(replica.Environment, replica.Application, replica.Replica);
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
                    using (var beacon = new ServiceBeacon(disposedClient, new ServiceBeaconInfo(replica), null, Log))
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

            using (var beacon = new ServiceBeacon(disposedClient, new ServiceBeaconInfo(replica), null, Log))
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
            var serviceBeaconInfo = new ServiceBeaconInfo(replica, new TagCollection {"tag1", "tag2"});
            CreateEnvironmentNode(replica.Environment);
            Ensemble.Stop();

            using (var beacon = GetServiceBeacon(serviceBeaconInfo))
            {
                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldNotCompleteIn(1.Seconds());

                Ensemble.Start();

                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);
                ReplicaRegistered(replica).Should().BeTrue();
                WaitForApplicationTagsExists(replica.Environment, replica.Application, replica.Replica, serviceBeaconInfo.Tags);
            }
        }

        [Test]
        public void Should_create_node_immediately_and_not_rewrite_tags_after_ensemble_restart()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            var serviceBeaconInfo = new ServiceBeaconInfo(replica, new TagCollection {"tag1", "tag2"});
            var newTags = new TagCollection {"tag2", "tag3"};
            CreateEnvironmentNode(replica.Environment);

            using (var beacon = GetServiceBeacon(serviceBeaconInfo))
            {
                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);
                ReplicaRegistered(replica).Should().BeTrue();
                WaitForApplicationTagsExists(replica.Environment, replica.Application, replica.Replica, serviceBeaconInfo.Tags);

                ServiceDiscoveryManager.SetReplicaTags(replica.Environment, replica.Application, replica.Replica, newTags).GetAwaiter().GetResult();
                ApplicationHasReplicaTags(replica.Environment, replica.Application, replica.Replica, newTags).Should().BeTrue();

                Ensemble.Stop();

                Ensemble.Start();

                WaitReplicaRegistered(replica);
                WaitForApplicationTagsExists(replica.Environment, replica.Application, replica.Replica, newTags);
            }
        }

        [Test]
        [Platform("Win", Reason = "Doesn't work on Unix systems because https://github.com/shayhatsor/zookeeper/issues/45 and notifications are delayed.")]
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

        [TestCase(true)]
        [TestCase(false)]
        public void Should_use_RegistrationAllowed(bool initialValue)
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            CreateEnvironmentNode(replica.Environment);

            var registrationAllowed = initialValue;
            Func<bool> registrationAllowedProvider = () => registrationAllowed;

            using (var beacon = GetServiceBeacon(replica, registrationAllowedProvider: registrationAllowedProvider))
            {
                ReplicaRegistered(replica).Should().BeFalse();

                beacon.Start();

                for (var checks = 0; checks < 5; checks++)
                {
                    WaitReplicaRegistered(replica, registrationAllowed);

                    registrationAllowed = !registrationAllowed;
                }
            }
        }

        [Test]
        public async Task Should_create_environment_if_absent()
        {
            var replica = new ReplicaInfo("absent", "vostok", "https://github.com/vostok");

            using (var beacon = GetServiceBeacon(replica, envSettings: new EnvironmentInfo("absent", "zapad", null)))
            {
                beacon.Start();
                await beacon.WaitForInitialRegistrationAsync().ConfigureAwait(false);
                var env = await ServiceDiscoveryManager.GetEnvironmentAsync("absent").ConfigureAwait(false);

                env.Should().NotBe(null);
                env?.ParentEnvironment.Should().Be("zapad");
            }
        }

        [Test]
        public void Should_throw_when_CreateIfAbsent_environment_is_different()
        {
            var replica = new ReplicaInfo("absent", "vostok", "https://github.com/vostok");

            Action throws = () => GetServiceBeacon(replica, envSettings: new EnvironmentInfo("different", "zapad", null));
            throws.Should().Throw<ArgumentException>();
        }

        [Test]
        public void Should_send_beacon_events()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            var baseProps = new Dictionary<string, string> {{ServiceDiscoveryEventWellKnownProperties.Description, "description"}};
            var dependenciesProps = new Dictionary<string, string>(baseProps) {{ServiceDiscoveryEventWellKnownProperties.Dependencies, "lalala"}};
            replica.SetProperty(ReplicaInfoKeys.Dependencies, dependenciesProps[ServiceDiscoveryEventWellKnownProperties.Dependencies]);

            var receivedEvents = new List<ServiceDiscoveryEvent>();
            var expectedEvents = new List<ServiceDiscoveryEvent>
            {
                new ServiceDiscoveryEvent(ServiceDiscoveryEventKind.ReplicaStarted, replica.Environment, replica.Application, replica.Replica, DateTimeOffset.Now, dependenciesProps),
                new ServiceDiscoveryEvent(ServiceDiscoveryEventKind.ReplicaStopped, replica.Environment, replica.Application, replica.Replica, DateTimeOffset.Now, baseProps)
            };
            var sender = Substitute.For<IServiceDiscoveryEventsSender>();
            sender.Send(Arg.Do<ServiceDiscoveryEvent>(serviceDiscoveryEvent => receivedEvents.Add(serviceDiscoveryEvent)));
            var settings = new ServiceBeaconSettings
            {
                ServiceDiscoveryEventContext = new ServiceDiscoveryEventsContext(new ServiceDiscoveryEventsContextConfig(sender)),
                AddDependenciesToNodeData = true
            };

            CreateEnvironmentNode(replica.Environment);
            using (var client = GetZooKeeperClient())
            using (var beacon = new ServiceBeacon(client, new ServiceBeaconInfo(replica), settings, Log))
            {
                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);

                ReplicaRegistered(replica).Should().BeTrue();
                beacon.Stop();
                ReplicaRegistered(replica).Should().BeFalse();
            }

            receivedEvents.Should()
                .BeEquivalentTo(expectedEvents,
                    options => options.Excluding(@event => @event.Timestamp)
                        .Using<Dictionary<string, string>>(context => context.Subject.Should().ContainKeys(context.Expectation.Keys))
                        .WhenTypeIs<Dictionary<string, string>>());
        }

        [Test]
        public async Task Should_send_stop_beacon_event_with_description_if_registration_not_allowed()
        {
            var replica =
                ReplicaInfoBuilder.Build(builder => builder.SetEnvironment("default").SetApplication("vostok").SetUrl(new Uri("https://github.com/vostok")), true);
            var registrationAllowed = true;
            Func<bool> registrationAllowedProvider = () => registrationAllowed;

            var receivedEvents = new List<ServiceDiscoveryEvent>();
            var sender = Substitute.For<IServiceDiscoveryEventsSender>();
            sender.Send(Arg.Do<ServiceDiscoveryEvent>(serviceDiscoveryEvent => receivedEvents.Add(serviceDiscoveryEvent)));
            var settings = new ServiceBeaconSettings
            {
                ServiceDiscoveryEventContext = new ServiceDiscoveryEventsContext(new ServiceDiscoveryEventsContextConfig(sender)),
                RegistrationAllowedProvider = registrationAllowedProvider
            };

            CreateEnvironmentNode(replica.ReplicaInfo.Environment);
            using (var client = GetZooKeeperClient())
            using (var beacon = new ServiceBeacon(client, replica, settings, Log))
            {
                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);

                ReplicaRegistered(replica.ReplicaInfo).Should().BeTrue();
                registrationAllowed = false;
                await Task.Delay(600);
                
                receivedEvents.Should().HaveCount(2);
                receivedEvents.First(serviceDiscoveryEvent => serviceDiscoveryEvent.Kind == ServiceDiscoveryEventKind.ReplicaStopped)
                    .Properties.Keys.Should()
                    .Contain(ServiceDiscoveryEventWellKnownProperties.Description);
            }
        }
    }
}