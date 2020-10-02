using JetBrains.Annotations;

namespace Vostok.ServiceDiscovery.Models
{
    [PublicAPI]
    public class ServiceBeaconAuthenticationInfo
    {
        public string Login { get; }

        public string Password { get; }

        public ServiceBeaconAuthenticationInfo(string login, string password)
        {
            Login = login;
            Password = password;
        }

        public ServiceBeaconAuthenticationInfo(string password)
        {
            Password = password;
        }
    }
}