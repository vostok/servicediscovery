using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Vostok.Commons.Threading;

namespace Vostok.ServiceDiscovery
{
    internal class NodeEventsHandler : IDisposable
    {
        private const int Running = 1;
        private const int Disposed = 2;
        private readonly AtomicInt state = new AtomicInt(Running);
        private readonly Task handleTask;

        private readonly AsyncManualResetEvent onEventSignal = new AsyncManualResetEvent(false);
        private readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();

        public NodeEventsHandler()
        {
            handleTask = Task.Run(Start);
        }

        public void SubmitEvent(Action action)
        {
            queue.Enqueue(action);
            onEventSignal.Set();
        }

        private async Task Start()
        {
            while (state == Running)
            {
                await onEventSignal.WaitAsync();
                onEventSignal.Reset();
                HandleEvents();
            }
        }

        private void HandleEvents()
        {
            while (queue.TryDequeue(out var action))
            {
                if (state == Disposed)
                    return;

                action.Invoke();
            }
        }

        public void Dispose()
        {
            state.TryIncreaseTo(Disposed);
            onEventSignal.Set();
            handleTask.GetAwaiter().GetResult();
        }
    }
}