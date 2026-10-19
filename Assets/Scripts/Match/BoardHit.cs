using Overlink.Core;

namespace Overlink.Match
{
    /// <summary>
    /// What kind of thing a pointer landed on. Six of these are ordinary sockets; the rest are the objects on
    /// the table that are <em>not</em> sockets but are still worth clicking: the two piles, the two Core
    /// counters and the Node plates.
    /// </summary>
    public enum BoardHitKind
    {
        /// <summary>An Active or Bench socket, either player's.</summary>
        Socket,

        /// <summary>Either player's Protocol socket.</summary>
        Protocol,

        /// <summary>A Node's plate, in the gap between its ranks.</summary>
        Plate,

        /// <summary>The face-down draw stack.</summary>
        DeckPile,

        /// <summary>The spent pile.</summary>
        DiscardPile,

        /// <summary>The player's own Core durability strip.</summary>
        PlayerCore,

        /// <summary>The opponent's Core durability strip - the target of an unblocked direct attack.</summary>
        OpponentCore,
    }

    /// <summary>
    /// One spot on the tabletop under the pointer, as the presenter's geometry sees it.
    /// <para>
    /// This is a <b>description, not a verdict</b>. It says what is there and who it belongs to; whether the
    /// player may attack it, deploy to it or block with it is decided by
    /// <see cref="GameCoordinator"/>, next to the rules. Keeping the two apart is what lets the same
    /// hit-test serve a drag, a details click and a combat click without any of them borrowing the others'
    /// legality.
    /// </para>
    /// </summary>
    public sealed class BoardHit
    {
        public BoardHitKind Kind;

        /// <summary>Whose object this is. Meaningful for every kind except the two piles.</summary>
        public PlayerIndex Owner;

        /// <summary>Slot coordinates, valid when <see cref="Kind"/> is <see cref="BoardHitKind.Socket"/>.</summary>
        public SlotType SlotType;
        public int SlotIndex = -1;
        public int NodeIndex = -1;

        /// <summary>The unit standing there, when there is one.</summary>
        public CardInstance Card;

        /// <summary>The tile this was resolved from, so the caller can highlight exactly what it hit.</summary>
        public BattlefieldPresenter.Tile Tile;

        /// <summary>Which of the four contested Nodes a <see cref="BoardHitKind.Plate"/> is.</summary>
        public int Node => NodeIndex;

        /// <summary>
        /// A short human name for the spot, used as the reader panel's title and in rejection notices.
        /// Never a sentence: callers splice it into their own phrasing.
        /// </summary>
        public string Describe()
        {
            switch (Kind)
            {
                case BoardHitKind.DeckPile: return "DECK";
                case BoardHitKind.DiscardPile: return "DISCARD";
                case BoardHitKind.PlayerCore: return "YOUR CORE";
                case BoardHitKind.OpponentCore: return "OPPONENT CORE";
                case BoardHitKind.Protocol: return Owner == PlayerIndex.Player ? "YOUR PROTOCOL" : "ENEMY PROTOCOL";
                case BoardHitKind.Plate: return $"NODE {NodeIndex + 1}";
                default: return $"{(Owner == PlayerIndex.Player ? "YOUR" : "ENEMY")} {SlotLocation()}";
            }
        }

        string SlotLocation()
        {
            switch (SlotType)
            {
                case SlotType.CoreActive: return "CORE ACTIVE";
                case SlotType.CoreBench: return $"CORE BENCH {SlotIndex + 1}";
                case SlotType.NodeActive: return $"NODE {NodeIndex + 1} ACTIVE";
                case SlotType.NodeBench: return $"NODE {NodeIndex + 1} BENCH {SlotIndex + 1}";
                default: return "SOCKET";
            }
        }

        /// <summary>True when this is one of the player's own hosting sockets.</summary>
        public bool IsOwnSocket => Kind == BoardHitKind.Socket && Owner == PlayerIndex.Player;
    }
}
