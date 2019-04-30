using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Commons.Helpers.Url;
using Vostok.Commons.Testing;
using Vostok.ServiceDiscovery.Models;
using Vostok.ServiceDiscovery.ServiceLocatorStorage;

namespace Vostok.ServiceDiscovery.Tests.ServiceLocatorStorage
{
    [TestFixture]
    internal class EnvironmentsStorage_Tests : TestsBase
    {
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

                    var expected = new EnvironmentInfo(parent, properties);
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
            var info = new EnvironmentInfo("parent", new Dictionary<string, string>
            {
                { "key", "value" }
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
            return new EnvironmentsStorage(ZooKeeperClient, pathHelper, Log);
        }
    }
}