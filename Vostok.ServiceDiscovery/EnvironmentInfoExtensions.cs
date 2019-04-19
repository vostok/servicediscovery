namespace Vostok.ServiceDiscovery
{
    internal static class EnvironmentInfoExtensions
    {
        public static bool IgnoreEmptyTopologies(this EnvironmentInfo info)
        {
            if (info?.Properties == null || !info.Properties.TryGetValue(EnvironmentInfoKeys.IgnoreEmptyEnvironments, out var ignore))
                return false;

            bool.TryParse(ignore, out var result);
            return result;
        }
    }
}