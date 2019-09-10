using System;
using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Vostok.ServiceDiscovery.Models;

namespace Vostok.ServiceDiscovery.Tests.Models
{
    [TestFixture]
    internal class ServiceTopology_Tests
    {
        [Test]
        public void Build_should_return_null_without_replicas()
        {
            ServiceTopology.Build(null, null).Should().BeNull();
        }

        [Test]
        public void Build_should_not_return_null_with_replicas_without_properties()
        {
            var topology = ServiceTopology.Build(new List<Uri>(), new Dictionary<string, string>());
            topology.Should().NotBeNull();
            topology.Replicas.Should().BeEmpty();
            topology.Properties.Should().BeEmpty();
        }

        [Test]
        public void Build_should_fill_replicas_and_properties()
        {
            var replicas = new List<Uri> {new Uri("http://x.ru"), new Uri("http://y.ru")};
            var properties = new Dictionary<string, string>
            {
                {"key1", "value1"},
                {"key2", "value2"}
            };
            var topology = ServiceTopology.Build(replicas, properties);
            topology.Should().NotBeNull();
            topology.Replicas.Should().BeEquivalentTo(replicas);
            topology.Properties.Should().BeEquivalentTo(properties);
        }
    }
}