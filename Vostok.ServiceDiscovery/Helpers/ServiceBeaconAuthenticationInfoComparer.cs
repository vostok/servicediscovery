using System.Collections.Generic;
using Vostok.ServiceDiscovery.Models;

namespace Vostok.ServiceDiscovery.Helpers
{
    public class ServiceBeaconAuthenticationInfoComparer : IEqualityComparer<ServiceBeaconAuthenticationInfo>
    {
        public static readonly ServiceBeaconAuthenticationInfoComparer Instance = new ServiceBeaconAuthenticationInfoComparer();

        public bool Equals(ServiceBeaconAuthenticationInfo x, ServiceBeaconAuthenticationInfo y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x == null || y == null)
                return false;

            return string.Equals(x.Login, y.Login) && string.Equals(x.Password, y.Password);
        }

        public int GetHashCode(ServiceBeaconAuthenticationInfo obj)
        {
            return ((obj.Login != null ? obj.Login.GetHashCode() : 0) * 397) ^ obj.Password.GetHashCode();
        }
    }
}