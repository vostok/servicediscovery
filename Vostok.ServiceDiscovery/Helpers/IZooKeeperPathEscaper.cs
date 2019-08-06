using JetBrains.Annotations;

namespace Vostok.ServiceDiscovery.Helpers
{
    /// <summary>
    /// An utility for transforming service discovery path segments into ZooKeeper capable ones.
    /// </summary>
    [PublicAPI]
    public interface IZooKeeperPathEscaper
    {
        string Escape(string segment);

        string Unescape(string segment);
    }
}