using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Vostok.Commons.Threading;
using Vostok.Logging.Abstractions;

namespace Vostok.ServiceDiscovery.Helpers
{
    // CR(kungurtsev): maybe add some tests?
    internal class ActionsQueue : IDisposable
    {
        private const int NotStarted = 0;
        private const int Running = 1;
        private const int Disposed = 2;

        private readonly AtomicInt state = new AtomicInt(NotStarted);
        private readonly AsyncManualResetEvent onEventSignal = new AsyncManualResetEvent(false);
        private readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();
        private readonly ILog log;

        private Task handleTask;

        public ActionsQueue(ILog log)
        {
            this.log = log.ForContext<ActionsQueue>();
        }

        public void Enqueue(Action action)
        {
            if (state.TryIncreaseTo(Running))
                handleTask = Task.Run(Handle);

            if (state == Disposed)
                return;

            queue.Enqueue(action);
            onEventSignal.Set();
        }

        private async Task Handle()
        {
            while (state == Running)
            {
                await onEventSignal.WaitAsync().ConfigureAwait(false);
                onEventSignal.Reset();

                while (state == Running && queue.TryDequeue(out var action))
                {
                    try
                    {
                        action.Invoke();
                    }
                    catch (Exception e)
                    {
                        log.Error(e);
                    }
                }
            }
        }

        public void Dispose()
        {
            if (state.TryIncreaseTo(Disposed))
            {
                onEventSignal.Set();
                handleTask?.GetAwaiter().GetResult();
            }
        }
    }
}