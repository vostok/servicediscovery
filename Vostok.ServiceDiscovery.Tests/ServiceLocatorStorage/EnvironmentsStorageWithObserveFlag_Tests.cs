using NUnit.Framework;
using FluentAssertions;
using Vostok.ServiceDiscovery.Abstractions.Models;

namespace Vostok.ServiceDiscovery.Tests.ServiceLocatorStorage;

[TestFixture]
internal class EnvironmentsStorageWithObserveFlag_Tests : EnvironmentStorage_TestsBase
{
    [Test]
    public void Should_not_delete_environment_from_cache_if_node_was_deleted_when_observation_of_deleted_apps_is_enabled()
    {
        using (var storage = GetEnvironmentsStorage(observeNonExistentEnvironment: true))
        {
            CreateEnvironmentNode("default", "parent");

            var expectedInfo = new EnvironmentInfo("default", "parent", null);
            storage.Get("default").Should().BeEquivalentTo(expectedInfo);

            DeleteEnvironmentNode("default");
            storage.UpdateAll();
            storage.Contains("default").Should().BeTrue();
            storage.Get("default").Should().BeNull();

            CreateEnvironmentNode("default", "parent");
            ShouldReturn(storage, "default", expectedInfo);
        }
    }

    [Test]
    public void Should_not_delete_environment_from_cache_when_observation_of_deleted_apps_is_disabled_and_client_disconnected()
    {
        using (var storage = GetEnvironmentsStorage(observeNonExistentEnvironment: false))
        {
            CreateEnvironmentNode("default", "parent");

            var expectedInfo = new EnvironmentInfo("default", "parent", null);
            ShouldReturnImmediately(storage, "default", expectedInfo);

            Ensemble.Stop();

            storage.UpdateAll();
            storage.Contains("default").Should().BeTrue();
            ShouldReturnImmediately(storage, "default", expectedInfo);
        }
    }

    [Test]
    public void Should_delete_environment_from_cache_if_node_was_deleted_when_observation_of_deleted_apps_is_disabled()
    {
        var expectedInfo = new EnvironmentInfo("default", "parent", null);

        using (var storage = GetEnvironmentsStorage(observeNonExistentEnvironment: false))
        {
            for (var i = 0; i < 10; i++)
            {
                CreateEnvironmentNode("default", "parent");

                ShouldReturnImmediately(storage, "default", expectedInfo);

                DeleteEnvironmentNode("default");
                storage.UpdateAll();
                storage.Contains("default").Should().BeFalse();
            }
        }
    }
}