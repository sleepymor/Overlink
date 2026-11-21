using System;

namespace Overlink.Core
{
    /// <summary>
    /// Host-side command gate. Peer IDs must come from the authenticated transport callback,
    /// never from a client-provided payload. This does not authenticate a Relay connection.
    /// </summary>
    public sealed class AuthoritativeCommandBoundary
    {
        readonly MatchEngine engine;
        readonly ulong playerPeer;
        readonly ulong opponentPeer;

        public long Revision { get; private set; }

        public AuthoritativeCommandBoundary(MatchEngine engine, ulong playerPeer, ulong opponentPeer)
        {
            this.engine = engine ?? throw new ArgumentNullException(nameof(engine));
            if (playerPeer == opponentPeer) throw new ArgumentException("Seats must have different peers.");
            this.playerPeer = playerPeer;
            this.opponentPeer = opponentPeer;
        }

        /// <summary>Only accepted commands advance the revision. Rejected commands do not mutate it.</summary>
        public CommandResult Execute(ulong senderPeer, long expectedRevision, GameCommand command)
        {
            if (command == null || expectedRevision != Revision) return CommandResult.InvalidCommand;
            PlayerIndex seat;
            if (senderPeer == playerPeer) seat = PlayerIndex.Player;
            else if (senderPeer == opponentPeer) seat = PlayerIndex.Opponent;
            else return CommandResult.InvalidCommand;
            if (command.Player != seat) return CommandResult.InvalidCommand;

            CommandResult result = engine.ExecuteCommand(command);
            if (result == CommandResult.Success) Revision++;
            return result;
        }
    }
}
