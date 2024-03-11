using System;
using FluentAssertions;
using Vostok.Commons.Testing;
using Vostok.ServiceDiscovery.Abstractions.Models;
using Vostok.ServiceDiscovery.ServiceLocatorStorage;

namespace Vostok.ServiceDiscovery.Tests.ServiceLocatorStorage;

internal class EnvironmentStorage_TestsBase : TestsBase
{
    protected static void ShouldReturn(EnvironmentsStorage storage, string name, EnvironmentInfo info)
    {
        Action assertion = () => { ShouldReturnImmediately(storage, name, info); };
        assertion.ShouldPassIn(DefaultTimeout);
    }

    protected static void ShouldReturnImmediately(EnvironmentsStorage storage, string name, EnvironmentInfo info)
    {
        storage.Get(name).Should().BeEquivalentTo(info);
    }

    protected EnvironmentsStorage GetEnvironmentsStorage(bool observeNonExistentEnvironment = true)
    {
        return new EnvironmentsStorage(ZooKeeperClient, PathHelper, EventsQueue, observeNonExistentEnvironment, Log);
    }
}