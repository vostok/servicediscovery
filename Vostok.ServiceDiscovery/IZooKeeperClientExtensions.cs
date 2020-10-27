using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model.Authentication;

namespace Vostok.ServiceDiscovery
{
    public static class IZooKeeperClientExtensions
    {
        public static void SetupServiceDiscoveryApiKey(this IZooKeeperClient zkClient, string login, string apiKey)
        {
            var zkAuthInfo = AuthenticationInfo.Digest(login, apiKey);
            zkClient.AddAuthenticationInfo(zkAuthInfo);
        }
    }
}