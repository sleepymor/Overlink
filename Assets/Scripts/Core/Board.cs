using System.Collections.Generic;

namespace Overlink.Core
{
    public sealed class BoardSlot
    {
        public SlotType Type { get; }

        /// <summary>Position within the slot array this slot belongs to. Pair with NodeIndex for Node slots.</summary>
        public int Index { get; }

        public int NodeIndex { get; }
        public CardInstance Card { get; set; }

        public BoardSlot(SlotType type, int index, int nodeIndex = -1)
        {
            Type = type;
            Index = index;
            NodeIndex = nodeIndex;
        }

        public bool IsOccupied()
        {
            return Card != null && Card.IsAlive();
        }

        public bool IsFree()
        {
            return Card == null || !Card.IsAlive();
        }

        /// <summary>
        /// True for a front-line socket: the Core Active or one of the four Node Actives. What stands in an
        /// Active socket is what the battlefields are fought with — it attacks, it is attacked, it holds a
        /// Node, and it is the only place a [Activate] ability can be fired from.
        /// </summary>
        public bool IsActive()
        {
            return Type == SlotType.CoreActive || Type == SlotType.NodeActive;
        }

        /// <summary>
        /// True for a reserve socket: the four Core Bench slots or one of the two Bench slots under a Node.
        /// A benched card is alive and can be brought in, but it is not in any battlefield, which is why
        /// blocking and targeting both exclude it.
        /// </summary>
        public bool IsBench()
        {
            return Type == SlotType.CoreBench || Type == SlotType.NodeBench;
        }

        public bool CanHost(CardType cardType)
        {
            switch (Type)
            {
                case SlotType.CoreActive:
                case SlotType.CoreBench:
                    return cardType == CardType.Unit;
                case SlotType.NodeActive:
                case SlotType.NodeBench:
                    return cardType == CardType.Unit || cardType == CardType.Relay;
                default:
                    return false;
            }
        }
    }

    public sealed class Board
    {
        // The Core is a single battlefield shared by both players: one Active slot in front and four
        // Bench slots behind it, per player. (Node slot counts below are per player as well.)
        public const int CoreActiveSlots = 1;
        public const int CoreBenchSlots = 4;

        /// <summary>GDD 2.1.7: the battlefield has four contested Nodes (Node 1-4).</summary>
        public const int NodeCount = 4;

        /// <summary>
        /// GDD 2.1.8 Node Domination: controlling this many Nodes (3 of 4) at Upkeep wins. "Controlling" is
        /// uncontested occupation, so the three must be Nodes the opponent has no body on - see
        /// <see cref="MatchState.ControlsNode"/>.
        /// </summary>
        public const int NodesForDomination = 3;

        public const int NodeActiveSlots = 1;
        public const int NodeBenchSlots = 2;

        public BoardSlot[] CoreActive { get; }
        public BoardSlot[] CoreBench { get; }
        public BoardSlot[][] NodeActive { get; }
        public BoardSlot[][] NodeBench { get; }
        public CardInstance Protocol { get; set; }

        public Board()
        {
            CoreActive = new BoardSlot[CoreActiveSlots];
            CoreBench = new BoardSlot[CoreBenchSlots];
            NodeActive = new BoardSlot[NodeCount][];
            NodeBench = new BoardSlot[NodeCount][];

            for (int i = 0; i < CoreActiveSlots; i++)
            {
                CoreActive[i] = new BoardSlot(SlotType.CoreActive, i);
            }

            for (int i = 0; i < CoreBenchSlots; i++)
            {
                CoreBench[i] = new BoardSlot(SlotType.CoreBench, i);
            }

            for (int node = 0; node < NodeCount; node++)
            {
                NodeActive[node] = new BoardSlot[NodeActiveSlots];
                NodeBench[node] = new BoardSlot[NodeBenchSlots];

                for (int i = 0; i < NodeActiveSlots; i++)
                {
                    NodeActive[node][i] = new BoardSlot(SlotType.NodeActive, i, node);
                }

                for (int i = 0; i < NodeBenchSlots; i++)
                {
                    NodeBench[node][i] = new BoardSlot(SlotType.NodeBench, i, node);
                }
            }
        }

        /// <summary>
        /// Occupation, not control: does <paramref name="player"/> have one of its own units in this board's
        /// Active socket for <paramref name="nodeIndex"/>. Each side owns its own Node sockets, so a Board can
        /// only ever see one half of a Node, and the word "controlled" would promise an answer it cannot give.
        /// <para>
        /// GDD 2.1.7's bonuses and 2.1.8's domination both belong to the player who holds a Node that the
        /// opponent does <em>not</em> also hold, which is a question about two boards. Ask
        /// <see cref="MatchState.ControlsNode"/> and <see cref="MatchState.CountControlledNodes"/> for that.
        /// </para>
        /// </summary>
        public bool HoldsNode(int nodeIndex, PlayerIndex player)
        {
            if (nodeIndex < 0 || nodeIndex >= NodeCount)
            {
                return false;
            }

            for (int i = 0; i < NodeActiveSlots; i++)
            {
                if (NodeActive[nodeIndex][i].IsOccupied() && NodeActive[nodeIndex][i].Card.Owner == player)
                {
                    return true;
                }
            }

            return false;
        }

        public BoardSlot FindSlot(CardInstance card)
        {
            if (card == null)
            {
                return null;
            }

            for (int i = 0; i < CoreActive.Length; i++)
            {
                if (CoreActive[i].Card == card)
                {
                    return CoreActive[i];
                }
            }

            for (int i = 0; i < CoreBench.Length; i++)
            {
                if (CoreBench[i].Card == card)
                {
                    return CoreBench[i];
                }
            }

            for (int node = 0; node < NodeCount; node++)
            {
                for (int i = 0; i < NodeActive[node].Length; i++)
                {
                    if (NodeActive[node][i].Card == card)
                    {
                        return NodeActive[node][i];
                    }
                }

                for (int i = 0; i < NodeBench[node].Length; i++)
                {
                    if (NodeBench[node][i].Card == card)
                    {
                        return NodeBench[node][i];
                    }
                }
            }

            return null;
        }

        public List<CardInstance> GetAllUnits(PlayerIndex player)
        {
            var units = new List<CardInstance>();

            for (int i = 0; i < CoreActive.Length; i++)
            {
                if (CoreActive[i].IsOccupied() && CoreActive[i].Card.Owner == player)
                {
                    units.Add(CoreActive[i].Card);
                }
            }

            for (int i = 0; i < CoreBench.Length; i++)
            {
                if (CoreBench[i].IsOccupied() && CoreBench[i].Card.Owner == player)
                {
                    units.Add(CoreBench[i].Card);
                }
            }

            for (int node = 0; node < NodeCount; node++)
            {
                for (int i = 0; i < NodeActive[node].Length; i++)
                {
                    if (NodeActive[node][i].IsOccupied() && NodeActive[node][i].Card.Owner == player)
                    {
                        units.Add(NodeActive[node][i].Card);
                    }
                }

                for (int i = 0; i < NodeBench[node].Length; i++)
                {
                    if (NodeBench[node][i].IsOccupied() && NodeBench[node][i].Card.Owner == player)
                    {
                        units.Add(NodeBench[node][i].Card);
                    }
                }
            }

            return units;
        }

        /// <summary>
        /// Every occupied Unit or Relay in an Active slot. Bench cards are deliberately excluded from
        /// combat queries; they may be promoted, but cannot attack, be attacked, or block while benched.
        /// </summary>
        public List<CardInstance> GetActiveUnits(PlayerIndex player)
        {
            var units = new List<CardInstance>();

            for (int i = 0; i < CoreActive.Length; i++)
            {
                if (CoreActive[i].IsOccupied() && CoreActive[i].Card.Owner == player)
                {
                    units.Add(CoreActive[i].Card);
                }
            }

            for (int node = 0; node < NodeCount; node++)
            {
                for (int i = 0; i < NodeActive[node].Length; i++)
                {
                    if (NodeActive[node][i].IsOccupied() && NodeActive[node][i].Card.Owner == player)
                    {
                        units.Add(NodeActive[node][i].Card);
                    }
                }
            }

            return units;
        }

        /// <summary>
        /// Every occupied Core Active slot's unit. This is the "Active" group that Relay and Protocol
        /// support effects refer to when they say "Core Actives".
        /// </summary>
        public List<CardInstance> GetCoreActiveUnits(PlayerIndex player)
        {
            var units = new List<CardInstance>();
            for (int i = 0; i < CoreActive.Length; i++)
            {
                if (CoreActive[i].IsOccupied() && CoreActive[i].Card.Owner == player)
                {
                    units.Add(CoreActive[i].Card);
                }
            }

            return units;
        }

        /// <summary>Every occupied unit or Relay in any Node slot, across all four contested Nodes.</summary>
        public List<CardInstance> GetNodeUnits(PlayerIndex player)
        {
            var units = new List<CardInstance>();
            for (int node = 0; node < NodeCount; node++)
            {
                for (int i = 0; i < NodeActive[node].Length; i++)
                {
                    if (NodeActive[node][i].IsOccupied() && NodeActive[node][i].Card.Owner == player)
                    {
                        units.Add(NodeActive[node][i].Card);
                    }
                }

                for (int i = 0; i < NodeBench[node].Length; i++)
                {
                    if (NodeBench[node][i].IsOccupied() && NodeBench[node][i].Card.Owner == player)
                    {
                        units.Add(NodeBench[node][i].Card);
                    }
                }
            }

            return units;
        }

        /// <summary>Resolves a slot type plus optional node index to the concrete slot, or null.</summary>
        public BoardSlot GetSlot(SlotType slotType, int index, int nodeIndex = -1)
        {
            switch (slotType)
            {
                case SlotType.CoreActive:
                    return index >= 0 && index < CoreActive.Length ? CoreActive[index] : null;
                case SlotType.CoreBench:
                    return index >= 0 && index < CoreBench.Length ? CoreBench[index] : null;
                case SlotType.NodeActive:
                    if (nodeIndex >= 0 && nodeIndex < NodeCount && index >= 0 && index < NodeActive[nodeIndex].Length)
                    {
                        return NodeActive[nodeIndex][index];
                    }
                    return null;
                case SlotType.NodeBench:
                    if (nodeIndex >= 0 && nodeIndex < NodeCount && index >= 0 && index < NodeBench[nodeIndex].Length)
                    {
                        return NodeBench[nodeIndex][index];
                    }
                    return null;
                default:
                    return null;
            }
        }

        /// <summary>
        /// First free legal slot for the given card type. Units fill the Core Active, then each Node's
        /// Active (so a fresh board spreads across all five battlefields instead of stacking only in the
        /// Core), then the Core Bench, then the Node Benches. Returns null when the board is full.
        /// </summary>
        public bool TryFindFreeSlot(CardType cardType, out BoardSlot slot)
        {
            slot = null;

            if (cardType == CardType.Unit)
            {
                slot = FirstFree(CoreActive);
                if (slot != null) return true;

                for (int node = 0; node < NodeCount; node++)
                {
                    slot = FirstFree(NodeActive[node]);
                    if (slot != null) return true;
                }

                slot = FirstFree(CoreBench);
                if (slot != null) return true;
            }

            if (cardType == CardType.Unit || cardType == CardType.Relay)
            {
                for (int node = 0; node < NodeCount; node++)
                {
                    slot = FirstFree(NodeActive[node]);
                    if (slot != null) return true;
                }

                for (int node = 0; node < NodeCount; node++)
                {
                    slot = FirstFree(NodeBench[node]);
                    if (slot != null) return true;
                }
            }

            slot = null;
            return false;
        }

        private static BoardSlot FirstFree(BoardSlot[] slots)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].IsOccupied())
                {
                    return slots[i];
                }
            }

            return null;
        }
    }
}
