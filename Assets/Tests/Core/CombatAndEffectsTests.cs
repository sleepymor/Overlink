using System.Collections.Generic;
using NUnit.Framework;

namespace Overlink.Core.Tests
{
    /// <summary>
    /// Coverage for the three behaviours that were missing from the match engine: blocking, Bench
    /// promotion, and real effect resolution.
    /// </summary>
    [TestFixture]
    public sealed class CombatAndEffectsTests
    {
        // ---- Blocking ---------------------------------------------------------------

        [Test]
        public void DeclareBlock_ByTheActivePlayer_IsRejected()
        {
            var state = new MatchState();
            var attacker = PlaceUnit(state, PlayerIndex.Player, 0, 3, 5);
            var blocker = PlaceUnit(state, PlayerIndex.Opponent, 0, 2, 5);
            state.CurrentPhase = TurnPhase.Combat;

            var engine = new MatchEngine(state);
            CommandResult result = engine.ExecuteCommand(
                new DeclareBlockCommand(PlayerIndex.Player, blocker.InstanceId, attacker.InstanceId));

            Assert.AreEqual(CommandResult.InvalidCommand, result,
                "Only the player who is not active may declare a block.");
        }

        [Test]
        public void DeclareBlock_ReroutesAttackThroughTheBlocker()
        {
            var state = new MatchState();
            var attacker = PlaceUnit(state, PlayerIndex.Player, 0, 3, 5);
            var blocker = PlaceUnit(state, PlayerIndex.Opponent, 0, 2, 5);
            state.CurrentPhase = TurnPhase.Combat;

            var engine = new MatchEngine(state);
            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(
                new DeclareBlockCommand(PlayerIndex.Opponent, blocker.InstanceId, attacker.InstanceId)));
            Assert.AreEqual(blocker.InstanceId, engine.GetPendingBlocker(attacker.InstanceId));

            int enemyCoreBefore = state.Players[1].CoreDurability;

            // Attacking "the core" is intercepted by the declared blocker.
            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(
                new DeclareAttackCommand(PlayerIndex.Player, attacker.InstanceId)));

            // The block keeps the 3 damage off the Core; only the GDD 2.1.7 damage transfer (10% of the
            // 3 damage the shield-less Core Active blocker took, rounded up = 1) reaches it.
            Assert.AreEqual(enemyCoreBefore - 1, state.Players[1].CoreDurability,
                "A declared block keeps the direct attack off the Core; only the transfer applies.");
            Assert.AreEqual(2, blocker.CurrentHp, "Blocker should take the attacker's 3 damage.");
            Assert.AreEqual(3, attacker.CurrentHp, "Attacker should take the blocker's 2 damage.");
        }

        [Test]
        public void DeclareBlock_IsConsumedByOneAttack()
        {
            var state = new MatchState();
            var attacker = PlaceUnit(state, PlayerIndex.Player, 0, 3, 5);
            PlaceUnit(state, PlayerIndex.Opponent, 0, 2, 5);
            state.CurrentPhase = TurnPhase.Combat;

            var engine = new MatchEngine(state);
            engine.ExecuteCommand(new DeclareBlockCommand(
                PlayerIndex.Opponent, state.Players[1].Board.CoreActive[0].Card.InstanceId, attacker.InstanceId));

            engine.ExecuteCommand(new DeclareAttackCommand(PlayerIndex.Player, attacker.InstanceId));

            Assert.AreEqual(0, engine.GetPendingBlocker(attacker.InstanceId),
                "The block should be consumed by the attack it intercepted.");
        }

        [Test]
        public void DeclareBlock_PreventBlock_BypassesTheBlocker()
        {
            var state = new MatchState();
            var attacker = PlaceUnit(state, PlayerIndex.Player, 0, 3, 5);
            var blocker = PlaceUnit(state, PlayerIndex.Opponent, 0, 2, 5);
            attacker.CannotBeBlocked = true;
            state.CurrentPhase = TurnPhase.Combat;

            var engine = new MatchEngine(state);
            engine.ExecuteCommand(new DeclareBlockCommand(
                PlayerIndex.Opponent, blocker.InstanceId, attacker.InstanceId));

            int enemyCoreBefore = state.Players[1].CoreDurability;
            engine.ExecuteCommand(new DeclareAttackCommand(PlayerIndex.Player, attacker.InstanceId));

            // GDD 2.1.6 Step 4: the bypassed blocker never fights, so the Core takes the flat 1 durability.
            Assert.AreEqual(enemyCoreBefore - 1, state.Players[1].CoreDurability,
                "A PreventBlock unit must ignore the declared blocker and hit the Core for 1.");
            Assert.AreEqual(5, blocker.CurrentHp);
        }

        [Test]
        public void DeclareBlock_CannotBlockKeyword_IsRejected()
        {
            var state = new MatchState();
            var attacker = PlaceUnit(state, PlayerIndex.Player, 0, 3, 5);
            var blocker = PlaceUnit(state, PlayerIndex.Opponent, 0, 2, 5);
            blocker.Definition.Keywords = new[] { Keyword.CannotBlock };
            state.CurrentPhase = TurnPhase.Combat;

            var engine = new MatchEngine(state);
            CommandResult result = engine.ExecuteCommand(
                new DeclareBlockCommand(PlayerIndex.Opponent, blocker.InstanceId, attacker.InstanceId));

            Assert.AreEqual(CommandResult.CannotBlock, result);
        }

        [Test]
        public void DeclareBlock_CannotReuseOneBlocker()
        {
            var state = new MatchState();
            var attacker = PlaceUnit(state, PlayerIndex.Player, 0, 3, 5);
            // A second attacker cannot share the single Core Active slot, so it sits on Node 1 instead.
            var secondAttacker = PlaceNodeUnit(state, PlayerIndex.Player, 0, 2, 5);
            var blocker = PlaceUnit(state, PlayerIndex.Opponent, 0, 2, 5);
            state.CurrentPhase = TurnPhase.Combat;

            var engine = new MatchEngine(state);
            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(
                new DeclareBlockCommand(PlayerIndex.Opponent, blocker.InstanceId, attacker.InstanceId)));
            Assert.AreEqual(CommandResult.InvalidCommand, engine.ExecuteCommand(
                new DeclareBlockCommand(PlayerIndex.Opponent, blocker.InstanceId, secondAttacker.InstanceId)));
        }

        [Test]
        public void BenchUnits_CannotAttackOrBlock()
        {
            var state = new MatchState();
            var benchedAttacker = new CardInstance(UnitDef("bench_attacker", 5, 5), PlayerIndex.Player)
            {
                State = UnitState.Ready
            };
            state.Players[0].Board.CoreBench[0].Card = benchedAttacker;

            var activeAttacker = PlaceUnit(state, PlayerIndex.Player, 0, 3, 5);
            var benchedBlocker = new CardInstance(UnitDef("bench_blocker", 2, 5), PlayerIndex.Opponent)
            {
                State = UnitState.Ready
            };
            state.Players[1].Board.CoreBench[0].Card = benchedBlocker;
            state.CurrentPhase = TurnPhase.Combat;

            var engine = new MatchEngine(state);
            Assert.AreEqual(CommandResult.UnitCannotAct, engine.ExecuteCommand(
                new DeclareAttackCommand(PlayerIndex.Player, benchedAttacker.InstanceId)));
            Assert.AreEqual(CommandResult.InvalidTarget, engine.ExecuteCommand(
                new DeclareBlockCommand(PlayerIndex.Opponent, benchedBlocker.InstanceId, activeAttacker.InstanceId)));
        }

        [Test]
        public void DeclareBlock_FreshlyDeployedUnit_MayBlock()
        {
            var state = new MatchState();
            var attacker = PlaceUnit(state, PlayerIndex.Player, 0, 3, 5);
            var blocker = PlaceUnit(state, PlayerIndex.Opponent, 0, 2, 5);
            blocker.State = UnitState.Summoned;
            state.CurrentPhase = TurnPhase.Combat;

            var engine = new MatchEngine(state);
            CommandResult result = engine.ExecuteCommand(
                new DeclareBlockCommand(PlayerIndex.Opponent, blocker.InstanceId, attacker.InstanceId));

            // GDD 2.1.5: Summoning Delay restricts attacking and switching, not blocking, so a unit
            // deployed this turn may still be assigned as a blocker.
            Assert.AreEqual(CommandResult.Success, result,
                "Summoning Delay must not keep a unit off blocking duty.");
            Assert.AreEqual(blocker.InstanceId, engine.GetPendingBlocker(attacker.InstanceId));
        }

        [Test]
        public void DeclareBlock_OutsideCombat_IsRejected()
        {
            var state = new MatchState();
            var attacker = PlaceUnit(state, PlayerIndex.Player, 0, 3, 5);
            var blocker = PlaceUnit(state, PlayerIndex.Opponent, 0, 2, 5);
            state.CurrentPhase = TurnPhase.Main;

            var engine = new MatchEngine(state);
            CommandResult result = engine.ExecuteCommand(
                new DeclareBlockCommand(PlayerIndex.Opponent, blocker.InstanceId, attacker.InstanceId));

            Assert.AreEqual(CommandResult.InvalidPhase, result);
        }

        // ---- Bench promotion --------------------------------------------------------

        [Test]
        public void Destruction_PromotesFromCoreBench()
        {
            var state = new MatchState();

            var doomed = PlaceUnit(state, PlayerIndex.Opponent, 0, 1, 1);
            var benched = new CardInstance(UnitDef("bench", 2, 3), PlayerIndex.Opponent) { State = UnitState.Ready };
            state.Players[1].Board.CoreBench[0].Card = benched;

            var attacker = PlaceUnit(state, PlayerIndex.Player, 0, 5, 2);
            state.CurrentPhase = TurnPhase.Combat;

            var engine = new MatchEngine(state);
            engine.ExecuteCommand(new DeclareAttackCommand(PlayerIndex.Player, attacker.InstanceId, doomed.InstanceId));

            Assert.IsFalse(doomed.IsAlive());
            Assert.AreSame(benched, state.Players[1].Board.CoreActive[0].Card,
                "The Bench unit should be promoted into the emptied Core Active slot.");
            Assert.IsNull(state.Players[1].Board.CoreBench[0].Card, "The Bench slot should now be free.");
            CollectionAssert.Contains(state.Players[1].Discard, doomed, "The destroyed unit goes to discard.");
        }

        [Test]
        public void Destruction_PromotesFromNodeBench()
        {
            var state = new MatchState();

            var doomed = new CardInstance(UnitDef("node_active", 2, 1), PlayerIndex.Opponent) { State = UnitState.Ready };
            state.Players[1].Board.NodeActive[1][0].Card = doomed;

            var benched = new CardInstance(UnitDef("node_bench", 1, 3), PlayerIndex.Opponent) { State = UnitState.Ready };
            state.Players[1].Board.NodeBench[1][0].Card = benched;

            var attacker = new CardInstance(UnitDef("attacker", 4, 2), PlayerIndex.Player) { State = UnitState.Ready };
            state.Players[0].Board.CoreActive[0].Card = attacker;

            state.CurrentPhase = TurnPhase.Combat;
            var engine = new MatchEngine(state);
            engine.ExecuteCommand(new DeclareAttackCommand(PlayerIndex.Player, attacker.InstanceId, doomed.InstanceId));

            Assert.IsFalse(doomed.IsAlive());
            Assert.AreSame(benched, state.Players[1].Board.NodeActive[1][0].Card,
                "The Bench unit should be promoted into the emptied Node Active slot.");
            Assert.IsNull(state.Players[1].Board.NodeBench[1][0].Card);
        }

        [Test]
        public void NodeControl_FollowsActiveOccupation()
        {
            var state = new MatchState();

            var unit = new CardInstance(UnitDef("node_unit", 2, 2), PlayerIndex.Opponent) { State = UnitState.Ready };
            state.Players[1].Board.NodeActive[0][0].Card = unit;

            Assert.AreEqual(1, state.Players[1].Board.CountControlledNodes(PlayerIndex.Opponent));

            var attacker = new CardInstance(UnitDef("attacker", 4, 2), PlayerIndex.Player) { State = UnitState.Ready };
            state.Players[0].Board.CoreActive[0].Card = attacker;

            state.CurrentPhase = TurnPhase.Combat;
            var engine = new MatchEngine(state);
            engine.ExecuteCommand(new DeclareAttackCommand(PlayerIndex.Player, attacker.InstanceId, unit.InstanceId));

            Assert.IsFalse(unit.IsAlive(), "With an empty Bench and no promotion, the Node is no longer held.");
            Assert.AreEqual(0, state.Players[1].Board.CountControlledNodes(PlayerIndex.Opponent));
        }

        // ---- Effects ---------------------------------------------------------------

        [Test]
        public void Ability_DrawEffect_AddsCardsToHand()
        {
            var state = new MatchState();
            FillDeck(state, PlayerIndex.Player, 5);

            var definition = new CardDefinition
            {
                Id = "card_fsti_100",
                Name = "Reinforce",
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Ability,
                Cost = 1,
                Keywords = new[] { Keyword.Draw },
                Effects = new[] { new EffectDescriptor(EffectType.Draw, TargetKind.Self, 2) }
            };

            var ability = new CardInstance(definition, PlayerIndex.Player);
            state.Players[0].Hand.Add(ability);
            state.CurrentPhase = TurnPhase.Main;

            var engine = new MatchEngine(state);
            CommandResult result = engine.ExecuteCommand(
                new PlayCardCommand(PlayerIndex.Player, ability.InstanceId, SlotType.CoreActive, 0));

            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreEqual(2, state.Players[0].Hand.Count, "Draw 2 should fill the hand from the deck.");
            CollectionAssert.Contains(state.Players[0].Discard, ability, "Abilities go to discard, not to a slot.");
            Assert.IsNull(state.Players[0].Board.Protocol, "An Ability must never take the Protocol slot.");
        }

        [Test]
        public void Ability_DestroyEffect_RemovesTheEnemyUnit()
        {
            var state = new MatchState();

            var victim = PlaceUnit(state, PlayerIndex.Opponent, 0, 2, 4);
            var sabotage = new CardInstance(new CardDefinition
            {
                Id = "card_fsti_101",
                Name = "Sabotage",
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Ability,
                Cost = 1,
                Effects = new[] { new EffectDescriptor(EffectType.Destroy, TargetKind.EnemyActive, 1) }
            }, PlayerIndex.Player);

            state.Players[0].Hand.Add(sabotage);
            state.CurrentPhase = TurnPhase.Main;

            var engine = new MatchEngine(state);
            CommandResult result = engine.ExecuteCommand(
                new PlayCardCommand(PlayerIndex.Player, sabotage.InstanceId, SlotType.CoreActive, 0));

            Assert.AreEqual(CommandResult.Success, result);
            Assert.IsFalse(victim.IsAlive());
            Assert.IsNull(state.Players[1].Board.CoreActive[0].Card,
                "Destroy must clear the slot, not just mark the unit dead.");
            CollectionAssert.Contains(state.Players[1].Discard, victim);
        }

        [Test]
        public void Protocol_BuffEffect_AppliesToTheActiveLine()
        {
            var state = new MatchState();
            // Bulwark Protocol costs 4 but a fresh match opens on StartingNP (3), so the play would
            // always bounce with InsufficientNP. This test is about the Protocol slot and the buff, not
            // affordability, so fund it up front.
            state.Players[0].CurrentNP = PlayerState.MaxNP;

            var unit = PlaceUnit(state, PlayerIndex.Player, 0, 2, 3);
            var protocol = new CardInstance(new CardDefinition
            {
                Id = "card_fsti_102",
                Name = "Bulwark Protocol",
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Protocol,
                Cost = 4,
                Effects = new[] { new EffectDescriptor(EffectType.Buff, TargetKind.AllAlliedActive, 1, 1) }
            }, PlayerIndex.Player);

            state.Players[0].Hand.Add(protocol);
            state.CurrentPhase = TurnPhase.Main;

            var engine = new MatchEngine(state);
            CommandResult result = engine.ExecuteCommand(
                new PlayCardCommand(PlayerIndex.Player, protocol.InstanceId, SlotType.CoreActive, 0));

            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreSame(protocol, state.Players[0].Board.Protocol, "Protocols take the dedicated slot.");
            Assert.AreEqual(3, unit.CurrentAtk, "Buff should raise ATK by Amount.");
            Assert.AreEqual(4, unit.CurrentHp, "Buff should raise HP by Amount2.");
        }

        [Test]
        public void Relay_DeploysToANodeAndControlsIt()
        {
            var state = new MatchState();

            var relay = new CardInstance(new CardDefinition
            {
                Id = "card_fsti_103",
                Name = "Signal Relay",
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Relay,
                Cost = 2
            }, PlayerIndex.Player);

            state.Players[0].Hand.Add(relay);
            state.CurrentPhase = TurnPhase.Main;

            var engine = new MatchEngine(state);

            // A Relay must not be accepted onto Core slots.
            Assert.AreEqual(CommandResult.InvalidTarget, engine.ExecuteCommand(
                new PlayCardCommand(PlayerIndex.Player, relay.InstanceId, SlotType.CoreActive, 0)));

            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(
                new PlayCardCommand(PlayerIndex.Player, relay.InstanceId, SlotType.NodeActive, 0, 0)));

            Assert.AreSame(relay, state.Players[0].Board.NodeActive[0][0].Card);
            Assert.IsTrue(state.Players[0].Board.NodeActive[0][0].IsOccupied(),
                "A Relay has no HP but must still count as occupying its slot.");
            Assert.AreEqual(1, state.Players[0].Board.CountControlledNodes(PlayerIndex.Player));
        }

        [Test]
        public void Protocol_CannotBePlayedTwice()
        {
            var state = new MatchState();

            CardDefinition def = new CardDefinition
            {
                Id = "card_fsti_104",
                Name = "Repairs",
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Protocol,
                Cost = 1,
                Effects = new[] { new EffectDescriptor(EffectType.Repair, TargetKind.Self, 3) }
            };

            var first = new CardInstance(def, PlayerIndex.Player);
            var second = new CardInstance(def, PlayerIndex.Player);
            state.Players[0].Hand.Add(first);
            state.Players[0].Hand.Add(second);
            state.CurrentPhase = TurnPhase.Main;

            var engine = new MatchEngine(state);

            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(
                new PlayCardCommand(PlayerIndex.Player, first.InstanceId, SlotType.CoreActive, 0)));
            Assert.AreEqual(CommandResult.SlotOccupied, engine.ExecuteCommand(
                new PlayCardCommand(PlayerIndex.Player, second.InstanceId, SlotType.CoreActive, 0)),
                "Only one Protocol may be in play at a time.");
        }

        [Test]
        public void EffectInterpreter_Repair_IsCappedAndRestoresCore()
        {
            var state = new MatchState();
            state.Players[0].CoreDurability = 1;

            var engine = new MatchEngine(state);
            engine.Effects.ResolveOne(null, PlayerIndex.Player,
                new EffectDescriptor(EffectType.Repair, TargetKind.Self, CardValidator.MaxRepairAmount),
                "Test Repair");

            // Repair restores durability but can never exceed the starting maximum of 3 (GDD 2.1.7).
            Assert.AreEqual(PlayerState.StartingCoreDurability, state.Players[0].CoreDurability);
        }

        [Test]
        public void EffectInterpreter_Heal_DoesNotExceedMaxHp()
        {
            var state = new MatchState();
            var unit = PlaceUnit(state, PlayerIndex.Player, 0, 2, 3);
            unit.CurrentHp = 1;

            var engine = new MatchEngine(state);
            engine.Effects.ResolveOne(unit, PlayerIndex.Player,
                new EffectDescriptor(EffectType.Heal, TargetKind.Self, 5), "Test Heal");

            Assert.AreEqual(3, unit.CurrentHp, "Heal must not push HP past the printed maximum.");
        }

        [Test]
        public void MatchEngine_EndTurn_MakesProgressFromDraw()
        {
            var state = new MatchState();
            state.CurrentPhase = TurnPhase.Draw;

            var engine = new MatchEngine(state);
            CommandResult result = engine.ExecuteCommand(new EndTurnCommand(PlayerIndex.Player));

            Assert.AreEqual(CommandResult.Success, result);
            Assert.AreEqual(PlayerIndex.Opponent, state.ActivePlayer,
                "Ending a turn at Draw must hand over instead of doing nothing.");
        }

        // ---- helpers ---------------------------------------------------------------

        private static CardDefinition UnitDef(string name, int atk, int hp)
        {
            return new CardDefinition
            {
                Id = "card_fsti_" + (System.Math.Abs(name.GetHashCode()) % 900 + 100),
                Name = name,
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Unit,
                Cost = 1,
                Atk = atk,
                Hp = hp
            };
        }

        private static void FillDeck(MatchState state, PlayerIndex owner, int count)
        {
            PlayerState player = state.GetPlayer(owner);
            for (int i = 0; i < count; i++)
            {
                player.Deck.Add(new CardInstance(UnitDef("filler_" + i, 1, 1), owner));
            }
        }

        private static CardInstance PlaceUnit(MatchState state, PlayerIndex owner, int slot, int atk, int hp)
        {
            var unit = new CardInstance(UnitDef(owner + "_unit_" + slot, atk, hp), owner)
            {
                State = UnitState.Ready
            };

            state.GetPlayer(owner).Board.CoreActive[slot].Card = unit;
            return unit;
        }

        private static CardInstance PlaceNodeUnit(MatchState state, PlayerIndex owner, int node, int atk, int hp)
        {
            var unit = new CardInstance(UnitDef(owner + "_nodeunit_" + node, atk, hp), owner)
            {
                State = UnitState.Ready
            };

            state.GetPlayer(owner).Board.NodeActive[node][0].Card = unit;
            return unit;
        }
    }
}
