using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using NUnit.Framework;
using Vostok.ServiceDiscovery.Abstractions.Models;
using Vostok.ServiceDiscovery.Models;
using Vostok.ServiceDiscovery.Serializers;

namespace Vostok.ServiceDiscovery.Tests.Serializers
{
    [TestFixture]
    public class ReplicaNodeDataSerializer_Tests
    {
        [Test]
        public void SerializeProperties_should_concat_dict_key_values()
        {
            var replicaInfo = new ReplicaInfo(
                "default",
                "vostok",
                "doesntmatter",
                new Dictionary<string, string>
                {
                    {"a", "a-value"},
                    {"asdf", "complex = value"}
                });
            var serialized = ReplicaNodeDataSerializer.Serialize(
                replicaInfo
            );

            var str = Encoding.UTF8.GetString(serialized);
            var expected = new List<string> {"a = a-value", "asdf = complex = value"};
            str.Should().Be(string.Join("\n", expected));
        }

        [Test]
        public void SerializeProperties_should_ignore_null_and_empty_values()
        {
            var replicaInfo = new ReplicaInfo(
                "default",
                "vostok",
                "doesntmatter",
                new Dictionary<string, string>
                {
                    {"a", null},
                    {"b", "value"},
                    {"c", ""}
                });

            var serialized = ReplicaNodeDataSerializer.Serialize(replicaInfo);

            var str = Encoding.UTF8.GetString(serialized);
            var expected = new List<string> {"b = value"};
            str.Should().Be(string.Join("\n", expected));
        }

        [Test]
        public void DeserializeProperties_should_deserialize_serialized()
        {
            var dict = new Dictionary<string, string>
            {
                {"a", "a-value"},
                {"asdf", "complex = value"},
                {"a.b.c", "dsfds sdf sdf sdf sd   ,. ,ds . . , .,  ;; ; ;"},
                {"with some spaces  ", "   "}
            };

            var replicaInfo = new ReplicaInfo(
                "default",
                "vostok",
                "doesntmatter",
                dict);

            var serialized = ReplicaNodeDataSerializer.Serialize(replicaInfo);
            var deserialized = ReplicaNodeDataSerializer.Deserialize(replicaInfo.Environment, replicaInfo.Application, replicaInfo.Replica, serialized);

            deserialized.Should().BeEquivalentTo(replicaInfo);
        }

        [Test]
        public void DeserializeProperties_should_ignore_null_and_empty_values()
        {
            var dict = new Dictionary<string, string>
            {
                {"a", "a-value"},
                {"b", null},
                {"c", ""},
                {"d", " "}
            };

            var replicaInfo = new ReplicaInfo(
                "default",
                "vostok",
                "doesntmatter",
                dict);

            var serialized = ReplicaNodeDataSerializer.Serialize(replicaInfo);
            var deserialized = ReplicaNodeDataSerializer.Deserialize(replicaInfo.Environment, replicaInfo.Application, replicaInfo.Replica, serialized);

            deserialized.Properties.Should()
                .BeEquivalentTo(
                    new Dictionary<string, string>
                    {
                        {"a", "a-value"},
                        {"d", " "}
                    });
        }

        [Test]
        public void DeserializeProperties_should_replace_new_line_symbol()
        {
            var dict = new Dictionary<string, string>
            {
                {"a", "a-value\n2"},
                {"b", "b\n\nb"}
            };
            var replicaInfo = new ReplicaInfo(
                "default",
                "vostok",
                "doesntmatter",
                dict);

            var serialized = ReplicaNodeDataSerializer.Serialize(replicaInfo);
            var deserialized = ReplicaNodeDataSerializer.Deserialize(replicaInfo.Environment, replicaInfo.Application, replicaInfo.Replica, serialized);

            deserialized.Properties.Should()
                .BeEquivalentTo(
                    new Dictionary<string, string>
                    {
                        {"a", "a-value 2"},
                        {"b", "b  b"}
                    });
        }
    }
}