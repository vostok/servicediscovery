using System;

namespace Vostok.ServiceDiscovery.Helpers
{
    internal class ZooKeeperPathEscaper : IZooKeeperPathEscaper
    {
        public static ZooKeeperPathEscaper Instance = new ZooKeeperPathEscaper();

        public string Escape(string segment) =>
            Uri.EscapeDataString(segment);

        public string Unescape(string segment) =>
            Uri.UnescapeDataString(segment);
    }
}