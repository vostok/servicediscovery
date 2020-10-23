using JetBrains.Annotations;

namespace Vostok.ServiceDiscovery.Models
{
    [PublicAPI]
    public class ServiceBeaconAuthenticationInfo
    {
        [CanBeNull]
        public string Login { get; }

        [NotNull]
        public string Password { get; }

        public ServiceBeaconAuthenticationInfo(string login, [NotNull] string password)
        {
            Login = login;
            Password = password;
        }

        public ServiceBeaconAuthenticationInfo([NotNull]string password)
        {
            Password = password;
        }
    }
}