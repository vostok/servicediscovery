using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Vostok.Commons.Threading;

namespace Vostok.ServiceDiscovery
{
    // CR(kungurtsev): should be moved to Helpers folder.
    // CR(kungurtsev): this is just queue of actions, why it contains words 'Node' and 'Events' connected only with SD events?
    // CR(kungurtsev): maybe add some tests?
    internal class NodeEventsHandler : IDisposable
    {
        private const int Running = 1;
        private const int Disposed = 2;
        private readonly AtomicInt state = new AtomicInt(Running);
        private readonly Task handleTask;

        private readonly AsyncManualResetEvent onEventSignal = new AsyncManualResetEvent(false);
        private readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();

        // CR(kungurtsev): why should we start new task even before first event submit?
        // CR(kungurtsev): add NotStarted state like in ServiceLocator.
        public NodeEventsHandler()
        {
            handleTask = Task.Run(Start);
        }

        // CR(kungurtsev): maybe rename to Enqueue?
        // CR(kungurtsev): ignore all events after dispose.
        public void SubmitEvent(Action action)
        {
            queue.Enqueue(action);
            onEventSignal.Set();
        }

        // CR(kungurtsev): this is not actually a start.
        // CR(kungurtsev): replace Start & HandleEvents functions to one with nested loop seems more readable.
        private async Task Start()
        {
            while (state == Running)
            {
                await onEventSignal.WaitAsync().ConfigureAwait(false);
                onEventSignal.Reset();
                HandleEvents();
            }
        }

        private void HandleEvents()
        {
            while (state == Running && queue.TryDequeue(out var action))
            {
                // CR(kungurtsev): try-catch. One action can ruin whole queue.
                // CR(kungurtsev): log error if occured.
                action.Invoke();
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