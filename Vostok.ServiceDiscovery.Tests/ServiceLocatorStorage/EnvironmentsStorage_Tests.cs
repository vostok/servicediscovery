using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Ploeh.AutoFixture;
using Vostok.Commons.Testing;
using Vostok.ServiceDiscovery.Models;
using Vostok.ServiceDiscovery.ServiceLocatorStorage;
using Vostok.ZooKeeper.Client.Abstractions;

namespace Vostok.ServiceDiscovery.Tests.ServiceLocatorStorage
{
    [TestFixture]
    internal class EnvironmentsStorage_Tests : TestsBase
    {
        private Fixture fixture;
        private string envName;

        [SetUp]
        public void SetUp()
        {
            fixture = new Fixture();
            envName = fixture.Create<string>();
        }

        [Test]
        public void Should_track_environment_parent_with_properties()
        {
            using (var storage = GetEnvironmentsStorage())
            {
                for (var times = 0; times < 10; times++)
                {
                    var parent = $"parent_{times}";
                    var properties = new Dictionary<string, string>
                    {
                        {"key", $"value_{times}"}
                    };

                    CreateEnvironmentNode("default", parent, properties);

                    var expected = new EnvironmentInfo("default", parent, properties);
                    if (times == 0)
                        ShouldReturnImmediately(storage, "default", expected);
                    else
                        ShouldReturn(storage, "default", expected);
                }
            }
        }

        [Test]
        public void Should_track_environment_creation_and_deletion()
        {
            var info = new EnvironmentInfo(
                "name",
                "parent",
                new Dictionary<string, string>
                {
                    {"key", "value"}
                });

            using (var storage = GetEnvironmentsStorage())
            {
                for (var times = 0; times < 10; times++)
                {
                    if (times == 0)
                        ShouldReturnImmediately(storage, "default", null);
                    else
                        ShouldReturn(storage, "default", null);

                    CreateEnvironmentNode("default", info.ParentEnvironment, info.Properties);

                    ShouldReturn(storage, "default", info);

                    DeleteEnvironmentNode("default");
                }
            }
        }

        [Test]
        public void Should_store_multiple_environments()
        {
            using (var storage = GetEnvironmentsStorage())
            {
                for (var i = 0; i < 10; i++)
                    CreateEnvironmentNode($"environment_{i}", $"environment_{i + 1}");

                for (var i = 0; i < 10; i++)
                    ShouldReturnImmediately(storage, $"environment_{i}", new EnvironmentInfo($"environment_{i}", $"environment_{i + 1}", null));
            }
        }

        [Test]
        public void Should_works_disconnected()
        {
            using (var storage = GetEnvironmentsStorage())
            {
                CreateEnvironmentNode("default", "parent");

                ShouldReturnImmediately(storage, "default", new EnvironmentInfo("default", "parent", null));

                Ensemble.Stop();
                ShouldReturnImmediately(storage, "default", new EnvironmentInfo("default", "parent", null));
                ShouldReturnImmediately(storage, "new", null);

                Ensemble.Start();
                ShouldReturnImmediately(storage, "default", new EnvironmentInfo("default", "parent", null));
                ShouldReturnImmediately(storage, "new", null);
            }
        }

        [Test]
        public void Should_not_update_after_dispose()
        {
            using (var storage = GetEnvironmentsStorage())
            {
                CreateEnvironmentNode("default", "parent_1");

                ShouldReturnImmediately(storage, "default", new EnvironmentInfo("default", "parent_1", null));

                storage.Dispose();

                CreateEnvironmentNode("default", "parent_2");
                storage.UpdateAll();

                ShouldReturnImmediately(storage, "default", new EnvironmentInfo("default", "parent_1", null));
            }
        }

        [Test]
        public void Should_not_update_to_invalid_data()
        {
            using (var storage = GetEnvironmentsStorage())
            {
                CreateEnvironmentNode("default", "parent");

                ShouldReturnImmediately(storage, "default", new EnvironmentInfo("default", "parent", null));

                ZooKeeperClient.SetData(PathHelper.BuildEnvironmentPath("default"), new byte[] {1, 2, 3});

                storage.UpdateAll();
                ShouldReturnImmediately(storage, "default", new EnvironmentInfo("default", "parent", null));
            }
        }

        [Test]
        public void UpdateAll_should_force_update()
        {
            using (var storage = GetEnvironmentsStorage())
            {
                for (var times = 0; times < 10; times++)
                {
                    var parent = $"parent_{times}";
                    var properties = new Dictionary<string, string>
                    {
                        {"key", $"value_{times}"}
                    };

                    CreateEnvironmentNode("default", parent, properties);
                    storage.UpdateAll();

                    var expected = new EnvironmentInfo("default", parent, properties);
                    ShouldReturnImmediately(storage, "default", expected);
                }
            }
        }

        private static void ShouldReturn(EnvironmentsStorage storage, string name, EnvironmentInfo info)
        {
            Action assertion = () => { ShouldReturnImmediately(storage, name, info); };
            assertion.ShouldPassIn(DefaultTimeout);
        }

        private static void ShouldReturnImmediately(EnvironmentsStorage storage, string name, EnvironmentInfo info)
        {
            storage.Get(name).Should().BeEquivalentTo(info);
        }

        private EnvironmentsStorage GetEnvironmentsStorage()
        {
            return new EnvironmentsStorage(ZooKeeperClient, PathHelper, Log);
        }
    }
}