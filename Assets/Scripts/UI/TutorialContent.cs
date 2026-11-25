using System;

namespace Overlink.UI
{
    /// <summary>
    /// One page of the first-run walkthrough. Wording is written from the current code: the board
    /// slots in Board.cs, the NP figures in MatchState.cs and PlayerState.cs, the lineup rules in
    /// MatchState.cs, the turn order in MatchEnums.cs, and the rules in DeckValidator.cs. No card
    /// ability text is described here.
    /// </summary>
    [Flags]
    public enum TutorialHighlight
    {
        None = 0,
        PlayerActives = 1,
        Nodes = 2,
    }

    public readonly struct TutorialPage
    {
        public readonly string Title;
        public readonly string Body;
        public readonly TutorialHighlight Highlight;

        public TutorialPage(string title, string body, TutorialHighlight highlight = TutorialHighlight.None)
        {
            Title = title;
            Body = body;
            Highlight = highlight;
        }
    }

    public static class TutorialContent
    {
        public static readonly TutorialPage[] Pages =
        {
            new TutorialPage(
                "WELCOME TO OVERLINK",
                "Overlink is a card battle over a shared battlefield. You win by destroying the enemy Core, " +
                "by controlling Nodes, by making your opponent unable to draw, or by conceding.\n\n" +
                "This short walkthrough covers the board, your resources, deck rules and how a match ends. " +
                "You can skip it at any time and replay it from Settings."),

            new TutorialPage(
                "THE BOARD",
                "Each side has a Core Active slot and four Core Bench slots. The Core is the single " +
                "front position that matters most.\n\n" +
                "The battlefield also has four Nodes. Each Node has one Active slot and two Bench slots per player.\n\n" +
                "Active units are in play and can attack. Bench units are held in reserve.\n\n" +
                "During Combat you can retreat an Active unit to swap it with a Bench unit. This costs 1 NP."),

            new TutorialPage(
                "CARD TYPES",
                "Unit: goes into a Core or Node slot. Core slots take Units only.\n\n" +
                "Relay: goes into a Node slot, Active or Bench. It cannot go into a Core slot.\n\n" +
                "Protocol: goes into your single Protocol slot. Its effect resolves when you play it.\n\n" +
                "Ability: resolves once when played. It takes no board slot."),

            new TutorialPage(
                "OPENING LINEUP",
                "Before the first turn you are offered five Units costing 3 NP or less. Place them in your " +
                "five Active slots, one per slot.\n\n" +
                "You may reroll the offer. The first reroll costs 2 NP, and each further reroll costs 1 NP more. " +
                "Confirm when your lineup is set. The match starts once both players have confirmed.",
                TutorialHighlight.PlayerActives),

            new TutorialPage(
                "NP AND COSTS",
                "NP is the resource you spend on cards, attacks and retreats. You start with 20 NP.\n\n" +
                "Your NP cap is 30. It rises to 40 while you control three Nodes.\n\n" +
                "Each turn you gain 4 NP, plus 1 for each Node you control. If you lose the coin flip, you get 1 extra NP to start.\n\n" +
                "Attacking costs 1 NP. Retreating costs 1 NP."),

            new TutorialPage(
                "NODES",
                "A Node is yours only when your unit is in its Active slot and your opponent's is not. If both sides " +
                "have an Active there, the Node is contested and neutral. It gives no NP and no bonus to either side.\n\n" +
                "Controlling Nodes gives you NP income and raises your NP cap. It also decides the Node Domination win.",
                TutorialHighlight.Nodes),

            new TutorialPage(
                "YOUR TURN",
                "Each turn runs through six phases in order:\n\n" +
                "1. Upkeep: your units ready, and you gain NP.\n" +
                "2. Draw: you draw a card.\n" +
                "3. Main: play cards and use your NP.\n" +
                "4. Combat: attack or retreat with your Active units.\n" +
                "5. End: finish your turn.\n" +
                "6. Cleanup: the turn passes to your opponent."),

            new TutorialPage(
                "DECK RULES",
                "A deck has 30 to 40 cards.\n\n" +
                "No more than 3 copies of any one card.\n\n" +
                "Every card in a deck must belong to the same faction.\n\n" +
                "The deck builder will not save a deck that breaks these rules."),

            new TutorialPage(
                "HOW YOU WIN",
                "Core destruction: reduce the enemy Core to zero durability. If both Cores fall in the same " +
                "moment, the active player wins.\n\n" +
                "Node Domination: control three of the four Nodes at the start of your Upkeep. This does not " +
                "apply on the very first turn.\n\n" +
                "Deck-Out: if you cannot draw and have no cards in hand, you lose.\n\n" +
                "Concession: either player may concede at any time."),

            new TutorialPage(
                "READY TO PLAY",
                "You can replay this walkthrough from Settings at any time."),
        };
    }
}
