using System;

namespace Overlink.Core
{
    /// <summary>
    /// Presentation-only revision gate for one match and one viewing seat. It never runs rules.
    /// A new match requires a new receiver; transport authentication and payload validation
    /// must happen before calling TryApply.
    /// </summary>
    public sealed class MatchViewReceiver
    {
        readonly PlayerIndex viewer;
        public MatchViewSnapshot Current { get; private set; }

        public MatchViewReceiver(PlayerIndex viewer)
        {
            if (viewer != PlayerIndex.Player && viewer != PlayerIndex.Opponent)
                throw new ArgumentOutOfRangeException(nameof(viewer));
            this.viewer = viewer;
        }

        /// <summary>
        /// Accept the first view or a strictly newer complete view. Revision gaps are allowed:
        /// a full snapshot supersedes missed intermediate views.
        /// </summary>
        public bool TryApply(MatchViewSnapshot snapshot)
        {
            if (snapshot == null || snapshot.Viewer != viewer ||
                (Current != null && snapshot.Revision <= Current.Revision)) return false;
            Current = snapshot;
            return true;
        }
    }
}
