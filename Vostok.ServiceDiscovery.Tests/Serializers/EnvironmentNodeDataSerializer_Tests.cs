using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Ploeh.AutoFixture;
using Vostok.ServiceDiscovery.Models;
using Vostok.ServiceDiscovery.Serializers;

namespace Vostok.ServiceDiscovery.Tests.Serializers
{
    [TestFixture]
    internal class EnvironmentNodeDataSerializer_Tests
    {
        private Fixture fixture;
        private string envName;

        [SetUp]
        public void SetUp()
        {
            fixture = new Fixture();
            envName = fixture.Create<string>();
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("x")]
        [TestCase("parent")]
        [TestCase("very_long_parent_environment_with_some_description")]
        public void Should_serialize_and_deserialize(string parent)
        {
            foreach (var properties in TestProperties())
            {
                var info = new EnvironmentInfo(envName, parent, properties);
                var serialized = EnvironmentNodeDataSerializer.Serialize(info);
                var deserialized = EnvironmentNodeDataSerializer.Deserialize(envName, serialized);
                deserialized.Should().BeEquivalentTo(info);
            }
        }

        [Test]
        public void Should_serialize_null()
        {
            var serialized = EnvironmentNodeDataSerializer.Serialize(null);
            var deserialized = EnvironmentNodeDataSerializer.Deserialize(envName, serialized);
            deserialized.Should().BeEquivalentTo(new EnvironmentInfo(envName, null, null));
        }

        [Test]
        public void Should_deserialize_null()
        {
            EnvironmentNodeDataSerializer.Deserialize(envName, null).Should().BeEquivalentTo(new EnvironmentInfo(envName, null, null));
        }

        [Test]
        public void Should_deserialize_empty()
        {
            EnvironmentNodeDataSerializer.Deserialize(envName, new byte[0]).Should().BeEquivalentTo(new EnvironmentInfo(envName, null, null));
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