using System;
using System.Collections.Generic;
using System.Diagnostics;
using FluentAssertions;
using NUnit.Framework;
using Vostok.ServiceDiscovery.Models;
using EnvironmentInfo = Vostok.Commons.Environment.EnvironmentInfo;

namespace Vostok.ServiceDiscovery.Tests
{
    [TestFixture]
    internal class ReplicaInfoBuilder_Tests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void Should_fill_default_settings(bool nullSetup)
        {
            var info = nullSetup
                ? ReplicaInfoBuilder.Build(null)
                : ReplicaInfoBuilder.Build(_ => {});

            info.Environment.Should().Be("default");
            info.Application.Should().NotBeNullOrEmpty();
            info.Replica.Should().NotBeNullOrEmpty();

            var properties = info.Properties;

            properties[ReplicaInfoKeys.Environment].Should().Be("default");
            properties[ReplicaInfoKeys.Application].Should().NotBeNullOrEmpty();
            properties[ReplicaInfoKeys.Replica].Should().NotBeNullOrEmpty();

            properties[ReplicaInfoKeys.Url].Should().BeNull();
            properties[ReplicaInfoKeys.Host].Should().NotBeNullOrEmpty();
            properties[ReplicaInfoKeys.Port].Should().BeNull();

            properties[ReplicaInfoKeys.ProcessName].Should().NotBeNullOrEmpty();
            properties[ReplicaInfoKeys.ProcessId].Should().NotBeNullOrEmpty();
            properties[ReplicaInfoKeys.BaseDirectory].Should().NotBeNullOrEmpty();

            properties[ReplicaInfoKeys.CommitHash].Should().BeNull();
            properties[ReplicaInfoKeys.ReleaseDate].Should().BeNull();
            properties[ReplicaInfoKeys.Dependencies].Should().BeNullOrEmpty();
        }

        [Test]
        public void Should_be_configurable()
        {
            var url = new UriBuilder
            {
                Scheme = "https",
                Host = "github.com",
                Port = 123,
                Path = "vostok"
            }.Uri;

            var info = ReplicaInfoBuilder.Build(
                setup => setup
                    .SetEnvironment("custom-environment")
                    .SetApplication("Vostok.App.1")
                    .SetUrl(url)
                    .SetCommitHash("ASDF")
                    .SetReleaseDate("released now")
                    .SetDependencies(new List<string> {"dep-a", "dep-b"}));

            info.Environment.Should().Be("custom-environment");
            info.Application.Should().Be("Vostok.App.1");
            info.Replica.Should().Be("https://github.com:123/vostok");

            var properties = info.Properties;

            properties[ReplicaInfoKeys.Environment].Should().Be("custom-environment");
            properties[ReplicaInfoKeys.Application].Should().Be("Vostok.App.1");
            properties[ReplicaInfoKeys.Replica].Should().Be("https://github.com:123/vostok");

            properties[ReplicaInfoKeys.Url].Should().BeEquivalentTo("https://github.com:123/vostok");
            properties[ReplicaInfoKeys.Port].Should().Be("123");

            properties[ReplicaInfoKeys.CommitHash].Should().Be("ASDF");
            properties[ReplicaInfoKeys.ReleaseDate].Should().Be("released now");
            properties[ReplicaInfoKeys.Dependencies].Should().Be("dep-a;dep-b");
        }

        [Test]
        public void Should_build_url_from_parts()
        {
            var info = ReplicaInfoBuilder.Build(
                setup => setup
                    .SetScheme("https")
                    .SetPort(123)
                    .SetUrlPath("vostok"));

            var host = EnvironmentInfo.Host.ToLowerInvariant();

            info.Replica.Should().Be($"https://{host}:123/vostok");

            var properties = info.Properties;

            properties[ReplicaInfoKeys.Replica].Should().Be($"https://{host}:123/vostok");

            properties[ReplicaInfoKeys.Url].Should().BeEquivalentTo($"https://{host}:123/vostok");
            properties[ReplicaInfoKeys.Port].Should().Be("123");
        }

        [Test]
        public void Should_build_url_from_port()
        {
            var info = ReplicaInfoBuilder.Build(
                setup => setup.SetPort(123));

            var host = EnvironmentInfo.Host.ToLowerInvariant();

            info.Replica.Should().Be($"http://{host}:123/");
            info.Properties[ReplicaInfoKeys.Replica].Should().Be($"http://{host}:123/");
        }

        [Test]
        public void Should_build_url_from_default_port()
        {
            var info = ReplicaInfoBuilder.Build(
                setup => setup.SetPort(80));

            var host = EnvironmentInfo.Host.ToLowerInvariant();

            info.Replica.Should().Be($"http://{host}/");
            info.Properties[ReplicaInfoKeys.Replica].Should().Be($"http://{host}/");
            info.Properties[ReplicaInfoKeys.Port].Should().Be("80");
        }

        [Test]
        public void Should_build_replica_from_process_info_without_port()
        {
            var info = ReplicaInfoBuilder.Build(
                builder => {});

            var host = EnvironmentInfo.Host;

            info.Replica.Should().Be($"{host}({Process.GetCurrentProcess().Id})");
            info.Properties[ReplicaInfoKeys.Replica].Should().Be($"{host}({Process.GetCurrentProcess().Id})");
        }

        [Test]
        public void Should_add_properties()
        {
            var info = ReplicaInfoBuilder.Build(
                builder =>
                {
                    builder.SetProperty("key1", "value1");
                    builder.SetProperty("key2", "value2");
                });

            var properties = info.Properties;
            properties["key1"].Should().Be("value1");
            properties["key2"].Should().Be("value2");
        }

        [Test]
        public void Should_rewrite_properties()
        {
            var info = ReplicaInfoBuilder.Build(
                builder =>
                {
                    builder.SetProperty("key", "value1");
                    builder.SetProperty("key", "value2");
                });

            var properties = info.Properties;
            properties["key"].Should().Be("value2");
        }

        [Test]
        public void Should_rewrite_default_properties()
        {
            var info = ReplicaInfoBuilder.Build(
                builder => { builder.SetProperty(ReplicaInfoKeys.Replica, "value"); });

            var host = EnvironmentInfo.Host;

            info.Replica.Should().Be($"{host}({Process.GetCurrentProcess().Id})");
            info.Properties[ReplicaInfoKeys.Replica].Should().Be("value");
        }
    }
}