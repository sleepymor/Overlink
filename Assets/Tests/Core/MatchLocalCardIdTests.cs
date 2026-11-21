using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Overlink.Core.Tests
{
    public sealed class MatchLocalCardIdTests
    {
        [Test]
        public void IndependentMatchesProduceSameCardIdsAndReplayLineup()
        {
            var deck = Enumerable.Range(0, 12).Select(i => new CardDefinition
            {
                Id = "unit_" + i, Name = "Unit", Type = CardType.Unit, Cost = 1, Atk = 1, Hp = 2
            }).ToList();
            var first = new MatchState(42, matchLocalCardIds: true);
            first.InitializeSetup(deck, new List<CardDefinition>(deck));
            // Unrelated allocations in this process must not influence the other match.
            for (int i = 0; i < 10; i++) new CardInstance(deck[0], PlayerIndex.Player);
            var second = new MatchState(42, matchLocalCardIds: true);
            second.InitializeSetup(deck, new List<CardDefinition>(deck));

            var a = first.GetPlayer(PlayerIndex.Player).LineupOffer;
            var b = second.GetPlayer(PlayerIndex.Player).LineupOffer;
            CollectionAssert.AreEqual(a.Select(c => c.InstanceId), b.Select(c => c.InstanceId));
            CollectionAssert.AreEqual(a.Select(c => c.DefinitionId), b.Select(c => c.DefinitionId));
            int[] ids = a.Select(c => c.InstanceId).ToArray();
            Assert.AreEqual(CommandResult.Success, new MatchEngine(first).ExecuteCommand(new ConfirmLineupCommand(PlayerIndex.Player, ids)));
            Assert.AreEqual(CommandResult.Success, new MatchEngine(second).ExecuteCommand(new ConfirmLineupCommand(PlayerIndex.Player, ids)));
        }

        [Test]
        public void EffectSummonsUseTheSameMatchLocalSequence()
        {
            var unit = new CardDefinition { Id = "unit", Name = "Unit", Type = CardType.Unit, Hp = 2 };
            var first = new MatchState(1, matchLocalCardIds: true);
            var second = new MatchState(1, matchLocalCardIds: true);
            var effect = new EffectDescriptor(EffectType.Summon, TargetKind.AlliedActive, referenceId: unit.Id);
            var firstEffects = new EffectInterpreter(first) { CardResolver = _ => unit };
            var secondEffects = new EffectInterpreter(second) { CardResolver = _ => unit };
            firstEffects.ResolveOne(null, PlayerIndex.Player, effect, "test");
            new CardInstance(unit, PlayerIndex.Player);
            secondEffects.ResolveOne(null, PlayerIndex.Player, effect, "test");
            var firstCard = first.GetPlayer(PlayerIndex.Player).Board.CoreActive[0].Card;
            var secondCard = second.GetPlayer(PlayerIndex.Player).Board.CoreActive[0].Card;
            Assert.IsNotNull(firstCard);
            Assert.IsNotNull(secondCard);
            Assert.AreEqual(1, firstCard.InstanceId);
            Assert.AreEqual(firstCard.InstanceId, secondCard.InstanceId);
        }
    }
}
