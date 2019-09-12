using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Vostok.ServiceDiscovery.Abstractions.Models;
using Vostok.ServiceDiscovery.Models;
using Vostok.ServiceDiscovery.Serializers;

namespace Vostok.ServiceDiscovery.Tests.Serializers
{
    [TestFixture]
    internal class ApplicationNodeDataSerializer_Tests
    {
        private string envName;
        private string appName;

        [SetUp]
        public void SetUp()
        {
            envName = "envName";
            appName = "appName";
        }

        [Test]
        public void Should_serialize_and_deserialize_properties()
        {
            foreach (var properties in TestProperties())
            {
                var info = new ApplicationInfo(envName, appName, properties);
                var serialized = ApplicationNodeDataSerializer.Serialize(info);
                var deserialized = ApplicationNodeDataSerializer.Deserialize(envName, appName, serialized);
                deserialized.Should().BeEquivalentTo(info);
            }
        }

        [Test]
        public void Should_serialize_null()
        {
            var serialized = ApplicationNodeDataSerializer.Serialize(null);
            var deserialized = ApplicationNodeDataSerializer.Deserialize(envName, appName, serialized);
            deserialized.Should().BeEquivalentTo(new ApplicationInfo(envName, appName, null));
        }

        [Test]
        public void Should_deserialize_null()
        {
            ApplicationNodeDataSerializer.Deserialize(envName, appName, null).Should().BeEquivalentTo(new ApplicationInfo(envName, appName, null));
        }

        [Test]
        public void Should_deserialize_empty()
        {
            ApplicationNodeDataSerializer.Deserialize(envName, appName, new byte[0]).Should().BeEquivalentTo(new ApplicationInfo(envName, appName, null));
        }

        private static IEnumerable<Dictionary<string, string>> TestProperties()
        {
            return new[]
            {
                null,
                new Dictionary<string, string>(),
                new Dictionary<string, string> {{"key", "value"}},
                new Dictionary<string, string> {{"a", "aa"}, {"b b b", "bb __ bb // b"}}
            };
        }
    }
}