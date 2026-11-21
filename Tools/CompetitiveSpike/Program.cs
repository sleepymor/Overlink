using System;
using System.Collections.Generic;
using System.Linq;
using Overlink.Core;
using Overlink.Net;

// Local portability smoke test only. This does not implement a match server or prove
// cross-runtime determinism, secure randomness, persistence, or UGS access controls.
internal static class Program
{
    private static int Main()
    {
        try
        {
            string first = RunMatch(8142);
            string replay = RunMatch(8142);
            if (!string.Equals(first, replay, StringComparison.Ordinal))
                throw new InvalidOperationException("Identical inputs produced different smoke-test results.");
            CheckView();
            Console.WriteLine("PASS: Core compiled and a seeded match replayed in the .NET 6 process.");
            Console.WriteLine("Not verified: UGS deployment, persistence, security, or Unity platform determinism.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void CheckView()
    {
        var state = new MatchState(1);
        state.Players[0].Hand.Add(new CardInstance(new CardDefinition { Id = "hidden" }, PlayerIndex.Player));
        state.Players[0].Deck.Add(new CardInstance(new CardDefinition { Id = "host-deck" }, PlayerIndex.Player));
        state.Players[0].LineupOffer.Add(new CardInstance(new CardDefinition { Id = "host-offer" }, PlayerIndex.Player));
        state.Players[1].Hand.Add(new CardInstance(new CardDefinition { Id = "guest-hand" }, PlayerIndex.Opponent));
        state.Players[1].Deck.Add(new CardInstance(new CardDefinition { Id = "guest-deck" }, PlayerIndex.Opponent));
        state.Players[1].LineupOffer.Add(new CardInstance(new CardDefinition { Id = "guest-offer" }, PlayerIndex.Opponent));
        state.Players[0].Discard.Add(new CardInstance(new CardDefinition { Id = "public" }, PlayerIndex.Player));
        foreach (PlayerIndex seat in new[] { PlayerIndex.Player, PlayerIndex.Opponent })
        {
            var snapshot = new MatchViewSnapshot(state, seat, 1);
            int own = (int)seat, other = 1 - own;
            if (snapshot.Players[own].Hand.Count != 1 || snapshot.Players[own].LineupOffer.Count != 1 ||
                snapshot.Players[other].Hand.Count != 0 || snapshot.Players[other].LineupOffer.Count != 0 ||
                snapshot.Players[other].HandCount != 1 || snapshot.Players[other].DeckCount != 1)
                throw new InvalidOperationException("Seat visibility failed for " + seat);
        }
        var data = MatchViewEncoder.EncodeGuest(new MatchViewSnapshot(state, PlayerIndex.Opponent, 1));
        if (!MatchViewDecoder.TryDecode(data, out var view) || view.Players[0].Hand.Count != 0 ||
            view.Players[0].Discard.Count != 1 || view.Players[0].Discard[0].DefinitionId != "public")
            throw new InvalidOperationException("Redacted snapshot round-trip failed.");
        for (int i = 0; i < data.Length; i++)
            if (MatchViewDecoder.TryDecode(data.Take(i).ToArray(), out _))
                throw new InvalidOperationException("Truncated snapshot accepted at length " + i);
        Console.WriteLine("PASS: public discard and hidden hand snapshot round-trip; truncated payloads rejected.");
    }

    private static string RunMatch(int seed)
    {
        var definitions = Enumerable.Range(1, 30).Select(i => new CardDefinition
        {
            Id = "spike_" + i.ToString("000"), Name = "Spike " + i,
            Type = CardType.Unit, Cost = 1, Hp = 3
        }).ToList();
        var state = new MatchState(seed, true);
        state.InitializeMatch(new List<CardDefinition>(definitions), new List<CardDefinition>(definitions));
        var engine = new MatchEngine(state, definitions);
        string initial = Describe(state);
        CommandResult result = engine.ExecuteCommand(new ConcedeCommand(PlayerIndex.Opponent));
        if (result != CommandResult.Success || !state.IsMatchOver())
            throw new InvalidOperationException("Concession smoke test failed: " + result);
        return initial + "|" + result + "|" + state.Result + "|" + state.ResultReason;
    }

    private static string Describe(MatchState state)
    {
        return string.Join("|", state.Players.Select(player =>
            string.Join(",", player.Deck.Select(card => card.InstanceId + ":" + card.DefinitionId)) + "/" +
            string.Join(",", player.Hand.Select(card => card.InstanceId + ":" + card.DefinitionId))));
    }
}
