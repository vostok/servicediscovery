using JetBrains.Annotations;

namespace Vostok.ServiceDiscovery.Helpers
{
    [PublicAPI]
    public class AuthenticationHelper
    {
        private const string Delimiter = "/";

        public static string GenerateLogin(string application, string environment)
        {
            environment = environment ?? "default";
            return $"{application}{Delimiter}{environment}";
        }
    }
}