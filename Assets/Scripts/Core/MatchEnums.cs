namespace Overlink.Core
{
    /// <summary>
    /// The six turn phases from GDD 2.1.2, in order. A turn runs Upkeep -> Draw -> Main -> Combat ->
    /// End -> Cleanup and then hands over to the opponent, whose turn starts again at Upkeep.
    /// </summary>
    public enum TurnPhase
    {
        Upkeep,
        Draw,
        Main,
        Combat,
        End,
        Cleanup
    }

    public enum SlotType
    {
        CoreActive,
        CoreBench,
        NodeActive,
        NodeBench
    }

    public enum PlayerIndex
    {
        Player = 0,
        Opponent = 1
    }

    public enum MatchResult
    {
        None,
        PlayerWin,
        OpponentWin,
        Draw
    }

    public enum CommandType
    {
        DrawCard,
        PlayCard,
        DeclareAttack,
        DeclareBlock,
        SwitchPosition,
        ActivateAbility,
        PassPhase,
        Mulligan,
        EndTurn,

        /// <summary>GDD 2.1.8 condition 4: either player may concede at any time, ending the match.</summary>
        Concede
    }

    public enum CommandResult
    {
        Success,
        InvalidPhase,
        InsufficientNP,
        InvalidTarget,
        SlotOccupied,
        CardNotInHand,
        UnitCannotAct,
        InvalidCommand,

        /// <summary>The nominated blocker is alive but illegal: exhausted, under Summoning Delay, or has Cannot Block.</summary>
        CannotBlock
    }

    public enum UnitState
    {
        Summoned,
        Ready,
        Exhausted,
        Destroyed
    }
}
