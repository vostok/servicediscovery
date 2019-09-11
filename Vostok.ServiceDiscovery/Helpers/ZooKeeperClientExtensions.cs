using System;
using System.Threading.Tasks;
using Vostok.ZooKeeper.Client.Abstractions;
using Vostok.ZooKeeper.Client.Abstractions.Model;
using Vostok.ZooKeeper.Client.Abstractions.Model.Request;

namespace Vostok.ServiceDiscovery.Helpers
{
    // CR(kungurtsev): move to zookeeper abstractions.
    public static class ZooKeeperClientExtensions
    {
        public static async Task<bool> TryUpdateDataAsync(
            this IZooKeeperClient zooKeeperClient,
            string path,
            Func<byte[], byte[]> update,
            int attempts = 5)
        {
            for (var i = 0; i < attempts; i++)
            {
                var readResult = zooKeeperClient.GetData(path);
                if (!readResult.IsSuccessful)
                    return false;

                var newData = update(readResult.Data);

                var request = new SetDataRequest(path, newData)
                {
                    Version = readResult.Stat.Version
                };

                var updateResult = await zooKeeperClient.SetDataAsync(request).ConfigureAwait(false);

                if (updateResult.Status == ZooKeeperStatus.VersionsMismatch)
                    continue;

                return updateResult.IsSuccessful;
            }

            return false;
        }
    }
}