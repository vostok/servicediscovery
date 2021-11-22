using System;
using System.Threading.Tasks;
using Vostok.Commons.Threading;

namespace Vostok.ServiceDiscovery.Helpers
{
    internal class ResetCallAction
    {
        private readonly AtomicBoolean invoked = false;
        private readonly Func<Task<bool>> action;

        public ResetCallAction(Action action)
        {
            this.action = () =>
            {
                action.Invoke();
                return Task.FromResult(true);
            };
        }

        public ResetCallAction(Func<Task<bool>> action)
        {
            this.action = action;
        }

        public async Task Invoke()
        {
            if (invoked)
                return;

            if (await action.Invoke().ConfigureAwait(false))
                invoked.TrySetTrue();
        }

        public ResetCallAction Reset()
        {
            invoked.TrySetFalse();
            return this;
        }
    }
}