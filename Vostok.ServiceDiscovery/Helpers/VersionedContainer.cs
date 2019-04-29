using System;

namespace Vostok.ServiceDiscovery.Helpers
{
    internal class VersionedContainer<T>
        where T : class
    {
        public volatile T Value;
        private readonly object sync = new object();
        private long version = long.MinValue;

        public bool NeedUpdate(long newVersion)
        {
            return newVersion > version;
        }

        public bool Update(long newVersion, Func<T> valueProvider)
        {
            if (newVersion <= version)
                return false;

            lock (sync)
            {
                if (newVersion <= version)
                    return false;
                Value = valueProvider();
                version = newVersion;
                return true;
            }
        }

        public void Clear()
        {
            lock (sync)
            {
                Value = null;
                version = long.MinValue;
            }
        }

        public override string ToString() =>
            $"{nameof(Value)}: {Value}, {nameof(version)}: {version}";
    }
}