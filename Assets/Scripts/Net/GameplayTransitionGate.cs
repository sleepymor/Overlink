using System;
using System.Threading.Tasks;

namespace Overlink.Net
{
    /// <summary>Prevents duplicate transitions and never enters gameplay after an unsuccessful save.</summary>
    public sealed class GameplayTransitionGate
    {
        public bool IsRunning { get; private set; }

        public async Task<bool> RunAsync(Func<Task> prepare, Func<bool> stillConnected, Action enter)
        {
            if (IsRunning) return false;
            if (stillConnected == null) throw new ArgumentNullException(nameof(stillConnected));
            if (enter == null) throw new ArgumentNullException(nameof(enter));
            IsRunning = true;
            try
            {
                if (prepare != null) await prepare();
                if (!stillConnected()) return false;
                enter();
                return true;
            }
            finally { IsRunning = false; }
        }
    }
}
