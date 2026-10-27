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
        Concede,

        /// <summary>
        /// Opening lineup (not a GDD 2.1.3 mulligan): throw the whole offered five back and take a fresh
        /// offer, for 2 NP escalating by 1 with every reroll already taken. Legal only while setup is in
        /// progress, and only for the player's own offer, so it is exempt from the active-player rule the
        /// way a block is.
        /// </summary>
        RerollLineup,

        /// <summary>
        /// Assigns the opening offer to the five Active sockets and closes this player's setup. Once both
        /// players have confirmed, the match goes live: the unchosen cards return to their decks, both
        /// players draw their opening hand, and the coin winner's first Upkeep runs.
        /// </summary>
        ConfirmLineup,

        /// <summary>
        /// Combat-phase retreat: an Active unit trades places with one of its owner's Bench units for 1 NP,
        /// and both end the exchange Exhausted. This is the second of the two things an Active unit may do
        /// on its controller's Combat step, the other being to attack.
        /// </summary>
        Retreat
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
