using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;

namespace Vostok.ServiceDiscovery.Tests
{
    [TestFixture]
    internal class EnvironmentNodeDataSerializer_Tests
    {
        [TestCase(null)]
        [TestCase("")]
        [TestCase("x")]
        [TestCase("parent")]
        [TestCase("very_long_parent_environment_with_some_description")]
        public void Should_serialize_and_deserialize(string parent)
        {
            foreach (var properties in TestProperties())
            {
                var info = new EnvironmentInfo(parent, properties);
                var serialized = EnvironmentNodeDataSerializer.Serialize(info);
                var deserialized = EnvironmentNodeDataSerializer.Deserialize(serialized);
                deserialized.Should().BeEquivalentTo(info);
            }
        }

        [Test]
        public void Should_serialize_null()
        {
            var serialized = EnvironmentNodeDataSerializer.Serialize(null);
            var deserialized = EnvironmentNodeDataSerializer.Deserialize(serialized);
            deserialized.Should().BeEquivalentTo(new EnvironmentInfo(null, null));
        }

        [Test]
        public void Should_deserialize_null()
        {
            EnvironmentNodeDataSerializer.Deserialize(null).Should().BeEquivalentTo(new EnvironmentInfo(null, null));
        }

        [Test]
        public void Should_deserialize_empty()
        {
            EnvironmentNodeDataSerializer.Deserialize(new byte[0]).Should().BeEquivalentTo(new EnvironmentInfo(null, null));
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