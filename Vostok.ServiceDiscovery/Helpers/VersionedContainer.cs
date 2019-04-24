using System;

namespace Vostok.ServiceDiscovery.Helpers
{
    internal class VersionedContainer<T> where T : class
    {
        public volatile T Value;
        private long version = long.MinValue;
        private readonly object sync = new object();

        public void Update(long newVersion, Func<T> valueProvider)
        {
            if (newVersion <= version)
                return;

            lock (sync)
            {
                if (newVersion <= version)
                    return;
                Value = valueProvider();
                version = newVersion;
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