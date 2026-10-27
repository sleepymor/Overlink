using System;

namespace Overlink.Core
{
    public abstract class GameCommand
    {
        public PlayerIndex Player { get; }
        public CommandType Type { get; }

        protected GameCommand(PlayerIndex player, CommandType type)
        {
            Player = player;
            Type = type;
        }
    }

    public sealed class DrawCardCommand : GameCommand
    {
        public DrawCardCommand(PlayerIndex player) : base(player, CommandType.DrawCard)
        {
        }
    }

    public sealed class PlayCardCommand : GameCommand
    {
        public int CardInstanceId { get; }
        public SlotType TargetSlotType { get; }
        public int SlotIndex { get; }
        public int NodeIndex { get; }

        public PlayCardCommand(PlayerIndex player, int cardInstanceId, SlotType slotType, int slotIndex, int nodeIndex = -1)
            : base(player, CommandType.PlayCard)
        {
            CardInstanceId = cardInstanceId;
            TargetSlotType = slotType;
            SlotIndex = slotIndex;
            NodeIndex = nodeIndex;
        }
    }

    /// <summary>
    /// GDD 2.1.8 condition 4 (Concession) and §4's command list: the only command that may be issued by
    /// the non-active player, because conceding is a private decision that is legal in any phase.
    /// </summary>
    public sealed class ConcedeCommand : GameCommand
    {
        public ConcedeCommand(PlayerIndex player) : base(player, CommandType.Concede)
        {
        }
    }

    public sealed class DeclareAttackCommand : GameCommand
    {
        public int AttackerInstanceId { get; }
        public int DefenderInstanceId { get; }

        public DeclareAttackCommand(PlayerIndex player, int attackerInstanceId, int defenderInstanceId = -1)
            : base(player, CommandType.DeclareAttack)
        {
            AttackerInstanceId = attackerInstanceId;
            DefenderInstanceId = defenderInstanceId;
        }
    }

    public sealed class DeclareBlockCommand : GameCommand
    {
        public int BlockerInstanceId { get; }
        public int AttackerInstanceId { get; }

        public DeclareBlockCommand(PlayerIndex player, int blockerInstanceId, int attackerInstanceId)
            : base(player, CommandType.DeclareBlock)
        {
            BlockerInstanceId = blockerInstanceId;
            AttackerInstanceId = attackerInstanceId;
        }
    }

    /// <summary>Use a printed Activate skill instead of this unit's attack this Combat step.</summary>
    public sealed class ActivateAbilityCommand : GameCommand
    {
        public int CardInstanceId { get; }

        public ActivateAbilityCommand(PlayerIndex player, int cardInstanceId)
            : base(player, CommandType.ActivateAbility)
        {
            CardInstanceId = cardInstanceId;
        }
    }

    public sealed class SwitchPositionCommand : GameCommand
    {
        public int CardInstanceId { get; }
        public SlotType TargetSlotType { get; }
        public int SlotIndex { get; }
        public int NodeIndex { get; }

        public SwitchPositionCommand(PlayerIndex player, int cardInstanceId, SlotType slotType, int slotIndex, int nodeIndex = -1)
            : base(player, CommandType.SwitchPosition)
        {
            CardInstanceId = cardInstanceId;
            TargetSlotType = slotType;
            SlotIndex = slotIndex;
            NodeIndex = nodeIndex;
        }
    }

    public sealed class PassPhaseCommand : GameCommand
    {
        public PassPhaseCommand(PlayerIndex player) : base(player, CommandType.PassPhase)
        {
        }
    }

    public sealed class EndTurnCommand : GameCommand
    {
        public EndTurnCommand(PlayerIndex player) : base(player, CommandType.EndTurn)
        {
        }
    }

    public sealed class MulliganCommand : GameCommand
    {
        public int[] CardInstanceIds { get; }

        public MulliganCommand(PlayerIndex player, int[] cardInstanceIds) : base(player, CommandType.Mulligan)
        {
            CardInstanceIds = cardInstanceIds ?? Array.Empty<int>();
        }
    }

    /// <summary>
    /// Throws the whole opening offer back and takes a fresh one. There is nothing to nominate: a reroll
    /// replaces all five cards or nothing, which is what makes its price escalate — a player who could keep
    /// one card from a good offer would pay for the reroll twice over in the long run.
    /// </summary>
    public sealed class RerollLineupCommand : GameCommand
    {
        public RerollLineupCommand(PlayerIndex player) : base(player, CommandType.RerollLineup)
        {
        }
    }

    /// <summary>
    /// Fields the opening lineup. <see cref="ActiveInstanceIds"/> is in battlefield order — Core Active
    /// first, then Node 1-4 Actives — and an entry of 0 leaves that socket empty, so a player whose offer
    /// came up short can still close setup rather than deadlock it.
    /// </summary>
    public sealed class ConfirmLineupCommand : GameCommand
    {
        /// <summary>Socket count the array has to name: the Core Active plus one Active per Node.</summary>
        public static int RequiredLength => Board.CoreActiveSlots + Board.NodeCount * Board.NodeActiveSlots;

        public int[] ActiveInstanceIds { get; }

        public ConfirmLineupCommand(PlayerIndex player, int[] activeInstanceIds)
            : base(player, CommandType.ConfirmLineup)
        {
            ActiveInstanceIds = activeInstanceIds ?? Array.Empty<int>();
        }
    }

    /// <summary>
    /// Trades an Active unit out to a Bench socket and brings one Bench unit in for it. Both instance ids
    /// are named rather than the destination slot, because in a retreat the destination is whichever socket
    /// the incoming unit already holds — there is no choice to make beyond which unit comes in.
    /// </summary>
    public sealed class RetreatCommand : GameCommand
    {
        public int ActiveInstanceId { get; }
        public int BenchInstanceId { get; }

        public RetreatCommand(PlayerIndex player, int activeInstanceId, int benchInstanceId)
            : base(player, CommandType.Retreat)
        {
            ActiveInstanceId = activeInstanceId;
            BenchInstanceId = benchInstanceId;
        }
    }
}
