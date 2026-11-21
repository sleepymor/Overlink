using System;
using System.Threading.Tasks;

namespace Overlink.Net
{
    /// <summary>
    /// Serializes cloud mutations while keeping each operation's own completion and failure observable.
    /// Enqueue from Unity's main thread so continuations retain Unity's synchronization context.
    /// </summary>
    public sealed class SerializedMutationQueue
    {
        Task tail = Task.CompletedTask;

        public Task Enqueue(Func<Task> mutation)
        {
            if (mutation == null) throw new ArgumentNullException(nameof(mutation));
            Task previous = tail;
            var completion = new TaskCompletionSource<bool>();
            // Publish the new tail before invoking user code: a synchronous callback may
            // enqueue another mutation while this operation is starting.
            tail = completion.Task;
            RunAfter(previous, mutation, completion);
            return completion.Task;
        }

        static async void RunAfter(Task previous, Func<Task> mutation, TaskCompletionSource<bool> completion)
        {
            // A failed operation must not block the next attempt, but its own task remains faulted.
            try { await previous; }
            catch { /* The caller of the previous operation observes its failure. */ }
            try
            {
                await mutation();
                completion.TrySetResult(true);
            }
            catch (OperationCanceledException) { completion.TrySetCanceled(); }
            catch (Exception error) { completion.TrySetException(error); }
        }
    }
}
