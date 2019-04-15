using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using NUnit.Framework;

namespace Vostok.ServiceDiscovery.Tests
{
    [TestFixture]
    public class ReplicaNodeDataSerializer_Tests
    {
        [Test]
        public void Serialize_should_concat_dict_key_values()
        {
            var serialized = ReplicaNodeDataSerializer.Serialize(
                new Dictionary<string, string>
                {
                    {"a", "a-value"},
                    {"asdf", "complex = value"}
                });

            var str = Encoding.UTF8.GetString(serialized);
            var expected = new List<string> {"a = a-value", "asdf = complex = value"};
            str.Should().Be(string.Join("\n", expected));
        }

        [Test]
        public void Deserialize_should_deserialize_serialized()
        {
            var dict = new Dictionary<string, string>
            {
                {"a", "a-value"},
                {"asdf", "complex = value"},
                {"a.b.c", "dsfds sdf sdf sdf sd   ,. ,ds . . , .,  ;; ; ;"},
                {"with some spaces  ", "   "}
            };

            var serialized = ReplicaNodeDataSerializer.Serialize(dict);
            var deserialized = ReplicaNodeDataSerializer.Deserialize(serialized);

            deserialized.Should().BeEquivalentTo(dict);
        }

        [Test]
        public void Deserialize_should_ignore_null_and_empty_values()
        {
            var dict = new Dictionary<string, string>
            {
                {"a", "a-value"},
                {"b", null},
                {"c", ""},
                {"d", " "}
            };

            var serialized = ReplicaNodeDataSerializer.Serialize(dict);
            var deserialized = ReplicaNodeDataSerializer.Deserialize(serialized);

            deserialized.Should()
                .BeEquivalentTo(
                    new Dictionary<string, string>
                    {
                        {"a", "a-value"},
                        {"d", " "}
                    });
        }

        [Test]
        public void Deserialize_should_replace_new_line_symbol()
        {
            var dict = new Dictionary<string, string>
            {
                {"a", "a-value\n2"},
                {"b", "b\n\nb"}
            };

            var serialized = ReplicaNodeDataSerializer.Serialize(dict);
            var deserialized = ReplicaNodeDataSerializer.Deserialize(serialized);

            deserialized.Should()
                .BeEquivalentTo(
                    new Dictionary<string, string>
                    {
                        {"a", "a-value 2"},
                        {"b", "b  b"}
                    });
        }
    }
}