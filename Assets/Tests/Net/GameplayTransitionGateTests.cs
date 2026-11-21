using System;
using System.Collections;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Overlink.Net.Tests
{
    public sealed class GameplayTransitionGateTests
    {
        [UnityTest]
        public IEnumerator WaitsForSaveAndRejectsDuplicateTransition()
        {
            var gate = new GameplayTransitionGate();
            var save = new TaskCompletionSource<bool>();
            int entered = 0;
            Task<bool> first = gate.RunAsync(() => save.Task, () => true, () => entered++);
            Task<bool> duplicate = gate.RunAsync(null, () => true, () => entered++);
            Assert.IsTrue(gate.IsRunning);
            Assert.AreEqual(0, entered);
            Assert.IsFalse(duplicate.Result);
            save.SetResult(true);
            while (!first.IsCompleted) yield return null;
            Assert.IsTrue(first.Result);
            Assert.AreEqual(1, entered);
        }

        [UnityTest]
        public IEnumerator SaveFailureDoesNotEnterAndRetryCanSucceed()
        {
            var gate = new GameplayTransitionGate();
            int entered = 0;
            Task<bool> failed = gate.RunAsync(() => Task.FromException(new InvalidOperationException("save failed")),
                () => true, () => entered++);
            while (!failed.IsCompleted) yield return null;
            Assert.IsTrue(failed.IsFaulted);
            Assert.AreEqual(0, entered);
            Assert.IsFalse(gate.IsRunning);
            Task<bool> retry = gate.RunAsync(() => Task.CompletedTask, () => true, () => entered++);
            while (!retry.IsCompleted) yield return null;
            Assert.IsTrue(retry.Result);
            Assert.AreEqual(1, entered);
        }

        [UnityTest]
        public IEnumerator DisconnectDuringSavePreventsSceneTransition()
        {
            var gate = new GameplayTransitionGate();
            var save = new TaskCompletionSource<bool>();
            bool connected = true;
            int entered = 0;
            Task<bool> transition = gate.RunAsync(() => save.Task, () => connected, () => entered++);
            connected = false;
            save.SetResult(true);
            while (!transition.IsCompleted) yield return null;
            Assert.IsFalse(transition.Result);
            Assert.AreEqual(0, entered);
        }
    }
}
