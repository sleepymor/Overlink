using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Overlink.Net.Tests
{
    public sealed class SerializedMutationQueueTests
    {
        [UnityTest]
        public IEnumerator OperationsRunInOrderAndEachWaitsForConfirmation()
        {
            var queue = new SerializedMutationQueue();
            var release = new TaskCompletionSource<bool>();
            var events = new List<int>();
            Task first = queue.Enqueue(async () => { events.Add(1); await release.Task; events.Add(2); });
            Task second = queue.Enqueue(() => { events.Add(3); return Task.CompletedTask; });
            Assert.IsFalse(first.IsCompleted);
            Assert.IsFalse(second.IsCompleted);
            CollectionAssert.AreEqual(new[] { 1 }, events);
            release.SetResult(true);
            while (!second.IsCompleted) yield return null;
            Assert.IsFalse(second.IsFaulted);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, events);
        }

        [UnityTest]
        public IEnumerator FailedOperationRemainsFaultedAndDoesNotBlockRetry()
        {
            var queue = new SerializedMutationQueue();
            Task failed = queue.Enqueue(() => Task.FromException(new InvalidOperationException("save failed")));
            bool retried = false;
            Task retry = queue.Enqueue(() => { retried = true; return Task.CompletedTask; });
            while (!retry.IsCompleted) yield return null;
            Assert.IsFalse(retry.IsFaulted);
            Assert.IsTrue(failed.IsFaulted);
            Assert.IsInstanceOf<InvalidOperationException>(failed.Exception.InnerException);
            Assert.IsTrue(retried);
        }

        [UnityTest]
        public IEnumerator ReentrantEnqueueWaitsForTheCurrentOperation()
        {
            var queue = new SerializedMutationQueue();
            var release = new TaskCompletionSource<bool>();
            var events = new List<int>();
            Task nested = null;
            Task first = queue.Enqueue(async () =>
            {
                events.Add(1);
                nested = queue.Enqueue(() => { events.Add(3); return Task.CompletedTask; });
                await release.Task;
                events.Add(2);
            });
            CollectionAssert.AreEqual(new[] { 1 }, events);
            Assert.IsFalse(nested.IsCompleted);
            release.SetResult(true);
            while (!nested.IsCompleted) yield return null;
            Assert.IsFalse(first.IsFaulted);
            Assert.IsFalse(nested.IsFaulted);
            CollectionAssert.AreEqual(new[] { 1, 2, 3 }, events);
        }
    }
}
