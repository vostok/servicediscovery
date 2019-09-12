using Vostok.ServiceDiscovery.Abstractions.Models;
using Vostok.ServiceDiscovery.Models;

namespace Vostok.ServiceDiscovery.Helpers
{
    internal static class EnvironmentInfoExtensions
    {
        public static bool SkipIfEmpty(this EnvironmentInfo info)
        {
            if (info?.Properties == null || !info.Properties.TryGetValue(EnvironmentInfoKeys.SkipIfEmpty, out var ignore))
                return false;

            bool.TryParse(ignore, out var result);
            return result;
        }
    }
}