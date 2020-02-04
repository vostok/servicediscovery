using System;
using System.Threading;
using FluentAssertions;
using FluentAssertions.Extensions;
using NUnit.Framework;
using Vostok.Commons.Testing;
using Vostok.Logging.Abstractions;
using Vostok.Logging.Console;
using Vostok.ServiceDiscovery.Helpers;

namespace Vostok.ServiceDiscovery.Tests.Helpers
{
    [TestFixture]
    internal class ActionsQueue_Tests
    {
        private readonly ILog log = new SynchronousConsoleLog();
        private readonly TimeSpan defaultTimeout = 500.Milliseconds();

        [Test]
        public void Should_handle_events()
        {
            using (var queue = new ActionsQueue(log))
            {
                var tmp = false;
                queue.Enqueue(() => tmp = true);

                Action action = () => tmp.Should().BeTrue();
                action.ShouldPassIn(defaultTimeout);
            }
        }

        [Test]
        public void Should_keep_handling_order()
        {
            using (var queue = new ActionsQueue(log))
            {
                var tmp = 0;
                Action act1 = () => { tmp += 1; };
                Action act2 = () => { tmp *= 2; };

                queue.Enqueue(act1);
                queue.Enqueue(act2);

                Action check = () => tmp.Should().Be(2);
                check.ShouldPassIn(defaultTimeout);
            }
        }

        [Test]
        public void Should_invoke_events_when_pause_between_enqueue()
        {
            using (var queue = new ActionsQueue(log))
            {
                var tmp = 1;
                queue.Enqueue(() => tmp = 2);

                Action check = () => tmp.Should().Be(2);
                check.ShouldPassIn(defaultTimeout);

                Thread.Sleep(defaultTimeout);

                queue.Enqueue(() => tmp = 3);

                check = () => tmp.Should().Be(3);
                check.ShouldPassIn(defaultTimeout);
            }
        }

        [Test]
        public void Should_not_handle_new_events_after_dispose()
        {
            var queue = new ActionsQueue(log);
            queue.Dispose();

            var tmp = false;
            queue.Enqueue(() => tmp = true);

            Action action = () => tmp.Should().BeFalse();
            action.ShouldNotFailIn(defaultTimeout);
        }

        [Test]
        public void Should_not_handle_enqueued_actions_after_dispose()
        {
            var queue = new ActionsQueue(log);

            var tmp = 1;

            queue.Enqueue(
                () =>
                {
                    tmp = 2;
                    Thread.Sleep(defaultTimeout - 100.Milliseconds());
                });
            queue.Enqueue(() => tmp = 4);

            Action waitStartAction = () => tmp.Should().Be(2);
            waitStartAction.ShouldPassIn(50.Milliseconds());

            queue.Dispose();

            Action action2 = () => tmp.Should().Be(2);
            action2.ShouldNotFailIn(defaultTimeout + 100.Milliseconds());
        }

        [Test]
        public void Should_not_fail_if_action_throws_exception()
        {
            var tmp = 1;
            Action act = () =>
            {
                using (var queue = new ActionsQueue(log))
                {
                    queue.Enqueue(() => throw new Exception());
                    queue.Enqueue(() => tmp = 3);

                    Action action = () => tmp.Should().Be(3);
                    action.ShouldPassIn(defaultTimeout);
                }
            };

            act.Should().NotThrow();
        }
    }
}