﻿using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Vostok.Commons.Testing;
using Vostok.ServiceDiscovery.Abstractions.Models;
using Vostok.ServiceDiscovery.ServiceLocatorStorage;
using Vostok.ZooKeeper.Client.Abstractions;

namespace Vostok.ServiceDiscovery.Tests.ServiceLocatorStorage
{
    [TestFixture]
    internal class ApplicationsStorage_Tests : TestsBase
    {
        [Test]
        public void Should_track_application_properties()
        {
            CreateEnvironmentNode("default");
            CreateApplicationNode("default", "application");
            CreateReplicaNode(new ReplicaInfo("default", "application", "https://github.com/vostok"));

            using (var storage = GetApplicationsStorage(out _))
            {
                for (var times = 0; times < 10; times++)
                {
                    var properties = new Dictionary<string, string>
                    {
                        {"key", $"value_{times}"}
                    };

                    CreateApplicationNode("default", "application", properties);

                    var expected = ServiceTopology.Build(new List<Uri> {new Uri("https://github.com/vostok")}, properties);
                    if (times == 0)
                        ShouldReturnImmediately(storage, "default", "application", expected);
                    else
                        ShouldReturn(storage, "default", "application", expected);
                }
            }
        }

        [Test]
        public void Should_track_application_replicas()
        {
            CreateEnvironmentNode("default");
            CreateApplicationNode("default", "application");

            using (var storage = GetApplicationsStorage(out _))
            {
                var expectedReplicas = new List<Uri>();

                for (var times = 0; times < 10; times++)
                {
                    CreateReplicaNode(new ReplicaInfo("default", "application", $"https://github.com/vostok/{times}"));
                    expectedReplicas.Add(new Uri($"https://github.com/vostok/{times}"));

                    var expected = ServiceTopology.Build(expectedReplicas, null);
                    if (times == 0)
                        ShouldReturnImmediately(storage, "default", "application", expected);
                    else
                        ShouldReturn(storage, "default", "application", expected);
                }
            }
        }

        [Test]
        public void Should_return_null_without_application()
        {
            CreateEnvironmentNode("default");

            using (var storage = GetApplicationsStorage(out _))
            {
                ShouldReturnImmediately(storage, "default", "application", null);

                CreateApplicationNode("default", "application");

                ShouldReturn(storage, "default", "application", ServiceTopology.Build(new Uri[0], null));
            }
        }

        [Test]
        public void Should_return_empty_list_without_replicas()
        {
            CreateEnvironmentNode("default");
            CreateApplicationNode("default", "application");

            using (var storage = GetApplicationsStorage(out _))
            {
                ShouldReturnImmediately(storage, "default", "application", ServiceTopology.Build(new Uri[0], null));

                CreateReplicaNode(new ReplicaInfo("default", "application", "https://github.com/vostok"));

                var expected = ServiceTopology.Build(new[] {new Uri("https://github.com/vostok")}, null);
                ShouldReturn(storage, "default", "application", expected);
            }
        }

        [Test]
        public void Should_store_multiple_environments_and_applications()
        {
            CreateEnvironmentNode("environment1");
            CreateApplicationNode("environment1", "application1", new Dictionary<string, string> {{"key", "1/1"}});

            CreateEnvironmentNode("environment2");
            CreateApplicationNode("environment2", "application1", new Dictionary<string, string> {{"key", "2/1"}});
            CreateApplicationNode("environment2", "application2", new Dictionary<string, string> {{"key", "2/2"}});

            using (var storage = GetApplicationsStorage(out _))
            {
                ShouldReturnImmediately(
                    storage,
                    "environment1",
                    "application1",
                    ServiceTopology.Build(new Uri[0], new Dictionary<string, string> {{"key", "1/1"}}));

                ShouldReturnImmediately(
                    storage,
                    "environment2",
                    "application1",
                    ServiceTopology.Build(new Uri[0], new Dictionary<string, string> {{"key", "2/1"}}));
                ShouldReturnImmediately(
                    storage,
                    "environment2",
                    "application2",
                    ServiceTopology.Build(new Uri[0], new Dictionary<string, string> {{"key", "2/2"}}));
            }
        }

        [Test]
        public void Should_works_disconnected()
        {
            CreateEnvironmentNode("default");
            CreateApplicationNode("default", "application");
            CreateReplicaNode(new ReplicaInfo("default", "application", "https://github.com/vostok"));

            using (var storage = GetApplicationsStorage(out _))
            {
                var properties = new Dictionary<string, string>
                {
                    {"key", "value"}
                };

                CreateApplicationNode("default", "application", properties);

                var expected = ServiceTopology.Build(new List<Uri> {new Uri("https://github.com/vostok")}, properties);

                ShouldReturnImmediately(storage, "default", "application", expected);

                Ensemble.Stop();
                ShouldReturnImmediately(storage, "default", "application", expected);

                Ensemble.Start();
                ShouldReturnImmediately(storage, "default", "application", expected);
            }
        }

        [Test]
        public void Should_not_update_after_dispose()
        {
            CreateEnvironmentNode("default");
            CreateApplicationNode("default", "application", new Dictionary<string, string> {{"key", "value1"}});
            CreateReplicaNode(new ReplicaInfo("default", "application", "https://github.com/vostok"));

            using (var storage = GetApplicationsStorage(out _))
            {
                var expected = ServiceTopology.Build(new List<Uri> {new Uri("https://github.com/vostok")}, new Dictionary<string, string> {{"key", "value1"}});
                ShouldReturnImmediately(storage, "default", "application", expected);

                storage.Dispose();
                CreateApplicationNode("default", "application", new Dictionary<string, string> {{"key", "value2"}});

                storage.UpdateAll();
                ShouldReturnImmediately(storage, "default", "application", expected);
            }
        }

        [Test]
        public void Should_not_update_to_invalid_application_properties()
        {
            CreateEnvironmentNode("default");
            CreateApplicationNode("default", "application", new Dictionary<string, string> {{"key", "value"}});
            CreateReplicaNode(new ReplicaInfo("default", "application", "https://github.com/vostok"));

            using (var storage = GetApplicationsStorage(out _))
            {
                var expected = ServiceTopology.Build(new List<Uri> {new Uri("https://github.com/vostok")}, new Dictionary<string, string> {{"key", "value"}});
                ShouldReturnImmediately(storage, "default", "application", expected);

                ZooKeeperClient.SetData(PathHelper.BuildApplicationPath("default", "application"), new byte[] {1, 2, 3});

                storage.UpdateAll();
                ShouldReturnImmediately(storage, "default", "application", expected);
            }
        }

        [Test]
        [Platform("Win", Reason = "Doesn't work on Unix systems because https://github.com/shayhatsor/zookeeper/issues/45 and notifications are delayed.")]
        public void UpdateAll_should_force_update()
        {
            CreateEnvironmentNode("default");
            CreateApplicationNode("default", "application");

            using (var storage = GetApplicationsStorage(out _))
            {
                var expectedReplicas = new List<Uri>();

                for (var times = 0; times < 10; times++)
                {
                    var properties = new Dictionary<string, string>
                    {
                        {"key", $"value_{times}"}
                    };

                    CreateApplicationNode("default", "application", properties);
                    CreateReplicaNode(new ReplicaInfo("default", "application", $"https://github.com/vostok/{times}"));
                    expectedReplicas.Add(new Uri($"https://github.com/vostok/{times}"));

                    storage.UpdateAll();

                    var expected = ServiceTopology.Build(expectedReplicas, properties);
                    ShouldReturnImmediately(storage, "default", "application", expected);
                }
            }
        }

        [Test]
        public void Should_delete_application_from_cache_if_app_and_env_nodes_were_deleted_when_observation_of_deleted_apps_is_disabled()
        {
            const string environment = "environment1";
            const string app = "application1";

            var expectedTopology = ServiceTopology.Build(new Uri[0], new Dictionary<string, string> {{"key", "1/1"}});

            using (var storage = GetApplicationsStorage(out var envStorage, observeNonExistentApplications: false))
            {
                for (var i = 0; i < 10; i++)
                {
                    CreateEnvironmentNode(environment);
                    CreateApplicationNode(environment, app, new Dictionary<string, string> {{"key", "1/1"}});

                    envStorage.Get(environment).Should().BeEquivalentTo(new EnvironmentInfo(environment, null, null));

                    ShouldReturnImmediately(
                        storage,
                        environment,
                        app,
                        expectedTopology);

                    DeleteApplicationNode(environment, app);
                    DeleteEnvironmentNode(environment);

                    envStorage.UpdateAll();
                    storage.UpdateAll();

                    storage.Contains(environment, app).Should().BeFalse();
                }
            }
        }

        [Test]
        public void Should_not_delete_application_from_cache_when_env_exists_and_observation_of_deleted_apps_is_disabled()
        {
            const string environment = "environment1";
            const string app = "application1";

            CreateEnvironmentNode(environment);
            CreateApplicationNode(environment, app, new Dictionary<string, string> {{"key", "1/1"}});

            using (var storage = GetApplicationsStorage(out var envStorage, observeNonExistentApplications: false))
            {
                var expectedTopology = ServiceTopology.Build(new Uri[0], new Dictionary<string, string> {{"key", "1/1"}});
                ShouldReturnImmediately(
                    storage,
                    environment,
                    app,
                    expectedTopology);

                envStorage.Get(environment).Should().BeEquivalentTo(new EnvironmentInfo(environment, null, null));

                DeleteApplicationNode(environment, app);

                envStorage.UpdateAll();
                envStorage.Contains(environment).Should().BeTrue();
                storage.UpdateAll();

                storage.Contains(environment, app).Should().BeTrue();
            }
        }

        [Test]
        public void Should_not_delete_application_from_cache_when_observation_of_deleted_apps_is_disabled_and_client_disconnected()
        {
            const string environment = "environment1";
            const string app = "application1";
            CreateEnvironmentNode(environment);
            CreateApplicationNode(environment, app, new Dictionary<string, string> {{"key", "1/1"}});

            using (var storage = GetApplicationsStorage(out var envStorage, observeNonExistentApplications: false))
            {
                var expectedTopology = ServiceTopology.Build(new Uri[0], new Dictionary<string, string> {{"key", "1/1"}});
                ShouldReturnImmediately(
                    storage,
                    environment,
                    app,
                    expectedTopology);

                envStorage.Get(environment).Should().BeEquivalentTo(new EnvironmentInfo(environment, null, null));

                Ensemble.Stop();

                envStorage.UpdateAll();
                envStorage.Contains(environment).Should().BeTrue();
                storage.UpdateAll();

                storage.Contains(environment, app).Should().BeTrue();
                ShouldReturnImmediately(
                    storage,
                    environment,
                    app,
                    expectedTopology);
            }
        }

        private static void ShouldReturn(ApplicationsStorage storage, string environment, string application, ServiceTopology topology)
        {
            Action assertion = () => { ShouldReturnImmediately(storage, environment, application, topology); };
            assertion.ShouldPassIn(DefaultTimeout);
        }

        private static void ShouldReturnImmediately(ApplicationsStorage storage, string environment, string application, ServiceTopology topology)
        {
            storage.Get(environment, application).ServiceTopology.Should().BeEquivalentTo(topology);
        }

        private ApplicationsStorage GetApplicationsStorage(out EnvironmentsStorage envStorage, bool observeNonExistentApplications = true)
        {
            envStorage = new EnvironmentsStorage(ZooKeeperClient, PathHelper, EventsQueue, observeNonExistentApplications, Log);
            return new ApplicationsStorage(ZooKeeperClient, PathHelper, EventsQueue, observeNonExistentApplications, envStorage, Log);
        }
    }
}