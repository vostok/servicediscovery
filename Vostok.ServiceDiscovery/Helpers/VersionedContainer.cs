using Vostok.Commons.Threading;

namespace Vostok.ServiceDiscovery.Helpers
{
    internal class VersionedContainer<T>
        where T : class
    {
        public volatile T Value;
        private readonly object sync = new object();
        private readonly AtomicLong version = long.MinValue;

        public bool NeedUpdate(long newVersion)
        {
            return newVersion > version;
        }

        public bool Update(long newVersion, T value)
        {
            if (newVersion <= version)
                return false;

            lock (sync)
            {
                if (!version.TryIncreaseTo(newVersion))
                    return false;
                Value = value;
                return true;
            }
        }

        public void Clear()
        {
            lock (sync)
            {
                Value = null;
                version.Value = long.MinValue;
            }
        }

        public override string ToString() =>
            $"{nameof(Value)}: {Value}, {nameof(version)}: {version}";
    }
}