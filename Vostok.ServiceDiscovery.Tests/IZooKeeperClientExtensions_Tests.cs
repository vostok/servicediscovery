using System.Collections.Generic;
using FluentAssertions;
using NUnit.Framework;
using Vostok.Commons.Testing;
using Vostok.ServiceDiscovery.Abstractions.Models;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model.Authentication;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;

namespace Vostok.ServiceDiscovery.Tests
{
    internal class IZooKeeperClientExtensions_Tests : TestsBase
    {
        [Test]
        public void Should_use_AuthInfo()
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            CreateEnvironmentNode(replica.Environment);
            CreateApplicationNode(replica.Environment, replica.Application);
            var path = PathHelper.BuildApplicationPath(replica.Environment, replica.Application);
            var login = $"{replica.Environment}/{replica.Application}";
            var password = "password";
            var digest = Acl.Digest(AclPermissions.All, login, password);

            var setAclRequest = new SetAclRequest(path, new List<Acl> {digest});
            var setAclResult = ZooKeeperClient.SetAcl(setAclRequest);
            setAclResult.EnsureSuccess();

            ZooKeeperClient.SetupServiceDiscoveryApiKey(login, password);

            using (var beacon = GetServiceBeacon(replica))
            {
                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldCompleteIn(DefaultTimeout);
                ReplicaRegistered(replica).Should().BeTrue();
            }

            DeleteApplicationNode(replica.Environment, replica.Application);
        }

        [TestCase("beacon","beacon", "password0", "password1")]
        [TestCase("beacon", "beacon1", "password", "password")]
        public void Should_not_start_when_auth_data_is_incorrect(string beaconLogin ,string aclLogin, string beaconPassword, string aclPassword)
        {
            var replica = new ReplicaInfo("default", "vostok", "https://github.com/vostok");
            CreateEnvironmentNode(replica.Environment);
            CreateApplicationNode(replica.Environment, replica.Application);
            var path = PathHelper.BuildApplicationPath(replica.Environment, replica.Application);

            var digest = Acl.Digest(AclPermissions.Create, aclLogin, aclPassword);

            var setAclRequest = new SetAclRequest(path, new List<Acl> {digest});
            var setAclResult = ZooKeeperClient.SetAcl(setAclRequest);
            setAclResult.EnsureSuccess();

            ZooKeeperClient.SetupServiceDiscoveryApiKey(beaconLogin, beaconPassword);

            using (var beacon = GetServiceBeacon(replica))
            {
                beacon.Start();
                beacon.WaitForInitialRegistrationAsync().ShouldNotCompleteIn(DefaultTimeout);
                ReplicaRegistered(replica).Should().BeFalse();
            }

            DeleteApplicationNode(replica.Environment, replica.Application);
        }
    }
}