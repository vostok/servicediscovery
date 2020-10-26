using Vostok.ServiceDiscovery.Helpers;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model.Authentication;

namespace Vostok.ServiceDiscovery
{
    public static class IZooKeeperClientExtensions
    {
        public static void SetupServiceDiscoveryApiKey(this IZooKeeperClient zkClient, string environment, string application, string apiKey)
        {
            var login = AuthenticationHelper.GenerateLogin(application, environment);
            var zkAuthInfo = AuthenticationInfo.Digest(login, apiKey);
            zkClient.AddAuthenticationInfo(zkAuthInfo);
        }
    }
}