using System.Collections.Generic;
using NUnit.Framework;

namespace Overlink.Core.Tests
{
    /// <summary>
    /// Guards for the fight-event spine: <see cref="BattleEvent"/> records raised beside the sentences that
    /// already described them.
    /// <para>
    /// The spine exists so the board can put a number over the right card, which means two things have to hold at
    /// once. A death must be announced <em>exactly</em> once - <see cref="MatchState"/> has one collection site and
    /// <see cref="EffectInterpreter"/> has another, and a record at both would make every effect kill flash twice.
    /// And an event must never outrun its sentence, because MATCH LOG and the tabletop reading from one call site is
    /// the only reason they cannot disagree. So most of these tests pin a full batch and the sentences beside it,
    /// rather than counting records on their own.
    /// </para>
    /// <para>
    /// The wording asserted here is the wording that shipped before the spine: every migrated site kept its string
    /// byte for byte, so pinning the string is also pinning the migration against a reworded log.
    /// </para>
    /// </summary>
    [TestFixture]
    public sealed class BattleEventTests
    {
        // ---- Deaths: exactly one announcement -----------------------------------------

        [Test]
        public void CombatKill_AnnouncesTheDeathOnce()
        {
            var state = new MatchState();
            CardInstance runner = OnNode(state, PlayerIndex.Player, 0, "Runner", 3, 5);
            CardInstance scout = OnNode(state, PlayerIndex.Opponent, 0, "Scout", 2, 1);

            Assert.AreEqual(CommandResult.Success, Attack(state, PlayerIndex.Player, runner, scout));

            List<BattleEvent> events = Drain(state);
            BattleEvent dead = Only(events, BattleEventKind.Destroyed);

            Assert.AreEqual(scout.InstanceId, dead.SubjectId, "The dead card is the subject.");
            Assert.AreEqual(-1, dead.SourceId,
                "The collection site does not know who won the fight, so it blames nobody.");
            Assert.AreEqual(-1, dead.Player, "A death happens to a card, not to a player.");

            // Both blows land: a defender killed in the exchange still strikes back, and the engine narrates
            // that. The killer is recoverable from the record that killed it, which is the one above the death.
            Assert.AreEqual(2, CountOf(events, BattleEventKind.Damaged),
                "The exchange is simultaneous, so the dead card's answer is still a fact the board can show.");
            BattleEvent killing = First(events, BattleEventKind.Damaged);
            Assert.AreEqual(scout.InstanceId, killing.SubjectId);
            Assert.AreEqual(runner.InstanceId, killing.SourceId,
                "and the attribution a death declines to carry is sitting immediately above it.");
        }

        /// <summary>
        /// The double-fire guard. <see cref="EffectInterpreter"/> narrates a Destroy at its own site
        /// ("Ability Sabotage: X is destroyed") with no record, deliberately: the card is not removed there, it is
        /// only marked dead, and <see cref="MatchState.CollectDeadSlot"/> is where every death is announced. Add a
        /// record to the effect site and this goes red with two Destroyed.
        /// </summary>
        [Test]
        public void EffectKill_AnnouncesTheDeathOnceNotTwice()
        {
            var state = new MatchState();
            CardInstance victim = OnCore(state, PlayerIndex.Opponent, "Watchman", 2, 4);

            var sabotage = new CardInstance(new CardDefinition
            {
                Id = "card_sabotage",
                Name = "Sabotage",
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Ability,
                Cost = 1,
                Effects = new[] { new EffectDescriptor(EffectType.Destroy, TargetKind.EnemyActive, 1) }
            }, PlayerIndex.Player);
            state.GetPlayer(PlayerIndex.Player).Hand.Add(sabotage);
            state.CurrentPhase = TurnPhase.Main;

            var engine = new MatchEngine(state);
            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(
                new PlayCardCommand(PlayerIndex.Player, sabotage.InstanceId, SlotType.CoreActive, 0)));

            List<BattleEvent> events = Drain(state);
            Assert.AreEqual(1, CountOf(events, BattleEventKind.Destroyed),
                "Only the collection site announces a death. The effect's own sentence stays prose-only, " +
                "or every ability kill flashes twice on the board.");
            Assert.AreEqual(victim.InstanceId, Only(events, BattleEventKind.Destroyed).SubjectId);
            AssertSentence(state, "Ability Sabotage: Watchman is destroyed",
                "and the effect's own narration survives untouched, with no record of its own.");
        }

        /// <summary>
        /// The third death path, and the one with no fighter at all: a Protocol whose battlefield falls. It is
        /// reached directly rather than through a command because nothing in the engine currently destroys a
        /// Protocol in play - the announcement site is what is under test, not the way a card gets there.
        /// </summary>
        [Test]
        public void LossWithNoKiller_AnnouncesTheDeathAnyway()
        {
            var state = new MatchState();
            var protocol = new CardInstance(new CardDefinition
            {
                Id = "card_aegis",
                Name = "Aegis",
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Protocol,
                Cost = 1
            }, PlayerIndex.Player);
            state.GetPlayer(PlayerIndex.Player).Board.Protocol = protocol;

            protocol.Destroy();
            state.ResolveDestructionAndPromotion();

            List<BattleEvent> events = Drain(state);
            BattleEvent dead = Only(events, BattleEventKind.Destroyed);
            Assert.AreEqual(protocol.InstanceId, dead.SubjectId);
            Assert.AreEqual(-1, dead.SourceId, "A loss with no killer must still say so, with nothing to blame.");
            Assert.AreEqual("Player Protocol Aegis is destroyed", LastSentence(state),
                "and the sentence that has always described it must be the one beside it.");
        }

        // ---- The Core cost of a death, and the asymmetry that is not a cost --------------

        /// <summary>
        /// The Shield on the victim is not decoration: it is what keeps the GDD 2.1.7 leak off this test, so the
        /// durability the Core loses here is the one the death itself costs and nothing else. Pin both numbers and
        /// you can read a Core's drop from the records alone.
        /// </summary>
        [Test]
        public void CoreActiveLoss_CostsOneDurabilityAndNamesTheCard()
        {
            var state = new MatchState();
            CardInstance striker = OnCore(state, PlayerIndex.Player, "Breaker", 5, 9);
            CardInstance doomed = OnCore(state, PlayerIndex.Opponent, "Pin", 0, 1);
            doomed.AddShield(1);
            int before = state.GetPlayer(PlayerIndex.Opponent).CoreDurability;

            Assert.AreEqual(CommandResult.Success, Attack(state, PlayerIndex.Player, striker, doomed));

            List<BattleEvent> events = Drain(state);
            BattleEvent lost = Only(events, BattleEventKind.CoreSlotLost);

            Assert.AreEqual(doomed.InstanceId, lost.SubjectId,
                "The board must know which socket went empty, not just that durability moved.");
            Assert.AreEqual((int)PlayerIndex.Opponent, lost.Player, "It is the owner who pays.");
            Assert.AreEqual(1, lost.Amount, "GDD 2.1.7: each Core Active lost costs exactly 1.");
            Assert.AreEqual(0, CountOf(events, BattleEventKind.CoreTransferred),
                "The Shield held the opening blow, so nothing leaked through it and this test measures one cost.");
            Assert.AreEqual(before - 1, state.GetPlayer(PlayerIndex.Opponent).CoreDurability,
                "and the record agrees with the counter it describes.");
            Assert.AreEqual("Opponent loses 1 Core durability (Core Active destroyed)", LastSentence(state));
        }

        /// <summary>
        /// The half of that rule the board is most likely to invent wrongly. A Node Active dying costs its owner
        /// nothing, so there must be no <see cref="BattleEventKind.CoreSlotLost"/> at all - a presenter that
        /// pulses the Core on every death would be teaching a rule the engine does not have.
        /// </summary>
        [Test]
        public void NodeActiveLoss_CostsNoDurability()
        {
            var state = new MatchState();
            CardInstance runner = OnNode(state, PlayerIndex.Player, 0, "Runner", 3, 5);
            CardInstance scout = OnNode(state, PlayerIndex.Opponent, 0, "Scout", 2, 1);
            int before = state.GetPlayer(PlayerIndex.Opponent).CoreDurability;

            Assert.AreEqual(CommandResult.Success, Attack(state, PlayerIndex.Player, runner, scout));

            List<BattleEvent> events = Drain(state);
            Assert.AreEqual(0, CountOf(events, BattleEventKind.CoreSlotLost),
                "Only a Core Active carries a durability cost when it dies.");
            Assert.AreEqual(1, CountOf(events, BattleEventKind.Destroyed),
                "but it is still a death, and it must still be shown as one.");
            Assert.AreEqual(before, state.GetPlayer(PlayerIndex.Opponent).CoreDurability);
        }

        // ---- Promotion: the other half of a death ---------------------------------------

        [Test]
        public void CoreBenchPromotion_IsAnnouncedForTheCardThatMoved()
        {
            var state = new MatchState();
            CardInstance striker = OnCore(state, PlayerIndex.Player, "Breaker", 5, 9);
            CardInstance doomed = OnCore(state, PlayerIndex.Opponent, "Pin", 0, 1);
            doomed.AddShield(1);
            var benched = new CardInstance(Unit("Relief", 1, 3), PlayerIndex.Opponent)
            {
                State = UnitState.Ready
            };
            state.GetPlayer(PlayerIndex.Opponent).Board.CoreBench[0].Card = benched;

            Assert.AreEqual(CommandResult.Success, Attack(state, PlayerIndex.Player, striker, doomed));

            List<BattleEvent> events = Drain(state);
            BattleEvent promoted = Only(events, BattleEventKind.Promoted);
            Assert.AreEqual(benched.InstanceId, promoted.SubjectId,
                "The subject is the card that stepped up, which is the one the player has never seen before.");
            Assert.AreEqual(-1, promoted.SourceId, "Nobody moves a card into a socket; the vacancy does.");
            Assert.AreEqual(-1, promoted.Player);
            Assert.AreEqual("Opponent promotes Relief from Core Bench to Core Active 0", LastSentence(state));
        }

        [Test]
        public void NodeBenchPromotion_IsAnnouncedForTheCardThatMoved()
        {
            var state = new MatchState();
            CardInstance striker = OnCore(state, PlayerIndex.Player, "Breaker", 4, 5);
            CardInstance holder = OnNode(state, PlayerIndex.Opponent, 0, "Holder", 0, 1);
            var benched = new CardInstance(Unit("Filler", 1, 3), PlayerIndex.Opponent)
            {
                State = UnitState.Ready
            };
            state.GetPlayer(PlayerIndex.Opponent).Board.NodeBench[0][0].Card = benched;

            Assert.AreEqual(CommandResult.Success, Attack(state, PlayerIndex.Player, striker, holder));

            List<BattleEvent> events = Drain(state);
            Assert.AreEqual(benched.InstanceId, Only(events, BattleEventKind.Promoted).SubjectId);
            Assert.AreEqual("Opponent promotes Filler to Node 1 Active", LastSentence(state),
                "Node promotions are numbered from one, and the record sits beside that same string.");
        }

        // ---- Blocks and Core strikes -----------------------------------------------------

        /// <summary>
        /// A block is the one fact whose two ids are easiest to invert: <see cref="MatchEngine.GetPendingBlocker"/>
        /// is keyed by attacker and hands back the blocker, so a record built straight from that call reads
        /// backwards. Subject is the wall, source is the swing.
        /// </summary>
        [Test]
        public void Block_NamesTheBlockerAsSubjectAndTheAttackerAsSource()
        {
            var state = new MatchState();
            CardInstance attacker = OnCore(state, PlayerIndex.Player, "Charger", 3, 5);
            CardInstance blocker = OnCore(state, PlayerIndex.Opponent, "Wall", 2, 5);
            state.CurrentPhase = TurnPhase.Combat;

            var engine = new MatchEngine(state);
            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(
                new DeclareBlockCommand(PlayerIndex.Opponent, blocker.InstanceId, attacker.InstanceId)));

            List<BattleEvent> events = Drain(state);
            BattleEvent blocked = Only(events, BattleEventKind.Blocked);
            Assert.AreEqual(blocker.InstanceId, blocked.SubjectId, "The blocker is the card that just stood up.");
            Assert.AreEqual(attacker.InstanceId, blocked.SourceId, "and it stands against a named swing.");
            Assert.AreEqual(-1, blocked.Player);
            Assert.AreEqual(0, blocked.Amount, "Readying a card moves it, it does not cost it anything here.");
            Assert.AreEqual("Opponent readies Wall to block Charger", LastSentence(state));
        }

        [Test]
        public void UnblockedCoreStrike_ReportsOneDurabilityWhateverTheAttack()
        {
            var state = new MatchState();
            CardInstance giant = OnCore(state, PlayerIndex.Player, "Colossus", 9, 9);
            int before = state.GetPlayer(PlayerIndex.Opponent).CoreDurability;
            state.CurrentPhase = TurnPhase.Combat;

            var engine = new MatchEngine(state);
            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(
                new DeclareAttackCommand(PlayerIndex.Player, giant.InstanceId)));

            List<BattleEvent> events = Drain(state);
            BattleEvent struck = Only(events, BattleEventKind.CoreStruck);

            Assert.AreEqual(1, struck.Amount,
                "GDD 2.1.6 Step 4: a direct Core strike costs exactly 1 no matter the ATK, so the board must " +
                "never print a bigger number here - a '9' over the Core would be a rule the engine does not have.");
            Assert.AreEqual(giant.InstanceId, struck.SourceId,
                "Nothing stood in the way, and the player is entitled to see what did the walking.");
            Assert.AreEqual(-1, struck.SubjectId, "A Core is a counter, not a card with an id.");
            Assert.AreEqual((int)PlayerIndex.Opponent, struck.Player);
            Assert.AreEqual(before - 1, state.GetPlayer(PlayerIndex.Opponent).CoreDurability);
            Assert.AreEqual("Colossus hits the Opponent Core for 1 durability", LastSentence(state));
        }

        // ---- Damage, piercing and the leak that follows it ------------------------------

        [Test]
        public void CombatDamage_CarriesTheAmountAndBothCards()
        {
            var state = new MatchState();
            CardInstance runner = OnNode(state, PlayerIndex.Player, 0, "Runner", 3, 5);
            CardInstance scout = OnNode(state, PlayerIndex.Opponent, 0, "Scout", 2, 5);

            Assert.AreEqual(CommandResult.Success, Attack(state, PlayerIndex.Player, runner, scout));

            List<BattleEvent> events = Drain(state);
            BattleEvent hit = First(events, BattleEventKind.Damaged);
            Assert.AreEqual(scout.InstanceId, hit.SubjectId);
            Assert.AreEqual(runner.InstanceId, hit.SourceId);
            Assert.AreEqual(3, hit.Amount);
            Assert.AreEqual(2, scout.CurrentHp, "and the amount is the amount that came off.");
        }

        /// <summary>
        /// Piercing has its own branch and its own sentence, which makes it the easiest site in the engine to
        /// migrate and then forget. The record must be the same shape as an ordinary blow - the board draws the
        /// same number over the same card - while the sentence says the word the player cannot infer.
        /// </summary>
        [Test]
        public void PiercingDamage_RaisesTheSameRecordAsAnOrdinaryBlow()
        {
            var state = new MatchState();
            CardInstance lance = OnNode(state, PlayerIndex.Player, 0, "Lancer", 4, 5);
            lance.Definition.Keywords = new[] { Keyword.Piercing };

            CardInstance guard = OnNode(state, PlayerIndex.Opponent, 0, "Guard", 0, 5);
            guard.AddShield(3);
            int logBefore = state.EventLog.Count;

            Assert.AreEqual(CommandResult.Success, Attack(state, PlayerIndex.Player, lance, guard));

            List<BattleEvent> events = Drain(state);
            BattleEvent hit = First(events, BattleEventKind.Damaged);
            Assert.AreEqual(guard.InstanceId, hit.SubjectId);
            Assert.AreEqual(lance.InstanceId, hit.SourceId);
            Assert.AreEqual(4, hit.Amount);
            Assert.AreEqual(3, guard.CurrentShield, "Piercing ignores Shield entirely - it is not even spent.");
            Assert.AreEqual(1, guard.CurrentHp, "and all four points came off the card.");
            Assert.AreEqual(BattleEventKind.Attacked, events[0].Kind);
            Assert.AreEqual("Lancer deals 4 piercing damage to Guard", TailOf(state.EventLog, logBefore)[1],
                "the record stays ordinary while the sentence says piercing.");
        }

        [Test]
        public void EffectDamage_NamesTheCardWhoseAbilityDidIt()
        {
            var state = new MatchState();
            // EnemyActive resolves to Core Actives only, which is why the victim stands there.
            CardInstance warden = OnCore(state, PlayerIndex.Opponent, "Warden", 2, 5);

            var bolt = new CardInstance(new CardDefinition
            {
                Id = "card_bolt",
                Name = "Bolt",
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Ability,
                Cost = 1,
                Effects = new[] { new EffectDescriptor(EffectType.DealDamage, TargetKind.EnemyActive, 2) }
            }, PlayerIndex.Player);
            state.GetPlayer(PlayerIndex.Player).Hand.Add(bolt);
            state.CurrentPhase = TurnPhase.Main;

            var engine = new MatchEngine(state);
            Assert.AreEqual(CommandResult.Success, engine.ExecuteCommand(
                new PlayCardCommand(PlayerIndex.Player, bolt.InstanceId, SlotType.CoreActive, 0)));

            List<BattleEvent> events = Drain(state);
            BattleEvent hit = Only(events, BattleEventKind.Damaged);
            Assert.AreEqual(warden.InstanceId, hit.SubjectId);
            Assert.AreEqual(bolt.InstanceId, hit.SourceId,
                "An effect's damage is still somebody's card doing it; the board credits the ability that was played.");
            Assert.AreEqual(2, hit.Amount);
            Assert.AreEqual(-1, hit.Player);
            AssertSentence(state, "Ability Bolt: Warden takes 2 damage",
                "and the prefix the effect site builds is its own, not the card's name alone.");
        }

        /// <summary>
        /// The transfer rule is (damage + 9) / 10 and only ever fires off a shield-less Core Active, so a twelve
        /// damage hit costs two durability. The board prints this number, which is the only place in the
        /// presentation where a player can see the tenth damage point do anything at all.
        /// </summary>
        [Test]
        public void LeakThroughAShieldlessCoreActive_IsRecordedAgainstItsOwner()
        {
            var state = new MatchState();
            CardInstance sapper = OnNode(state, PlayerIndex.Player, 0, "Sapper", 12, 2);
            CardInstance bulwark = OnCore(state, PlayerIndex.Opponent, "Bulwark", 0, 40);
            int before = state.GetPlayer(PlayerIndex.Opponent).CoreDurability;

            Assert.AreEqual(CommandResult.Success, Attack(state, PlayerIndex.Player, sapper, bulwark));

            List<BattleEvent> events = Drain(state);
            BattleEvent leak = Only(events, BattleEventKind.CoreTransferred);
            Assert.AreEqual(bulwark.InstanceId, leak.SourceId, "The card that leaked is the one with a number on it.");
            Assert.AreEqual(-1, leak.SubjectId, "The Core that paid has no id to carry.");
            Assert.AreEqual((int)PlayerIndex.Opponent, leak.Player);
            Assert.AreEqual(2, leak.Amount, "(12 + 9) / 10 = 2.");
            Assert.AreEqual(before - 2, state.GetPlayer(PlayerIndex.Opponent).CoreDurability);
            Assert.AreEqual(0, CountOf(events, BattleEventKind.CoreStruck),
                "A leak is not a direct strike. Printing the two as one message would teach the player that " +
                "hurting a Core Active is the same thing as getting past its defenders.");
            AssertSentence(state, "Bulwark transfers 2 Core durability (12 damage, no Shield)",
                "and the sentence names the leaking card, not the attacker - which is what the record's source says too.");
        }

        [Test]
        public void LeakIsSilentWhileShieldStands()
        {
            var state = new MatchState();
            CardInstance sapper = OnNode(state, PlayerIndex.Player, 0, "Sapper", 12, 2);
            CardInstance bulwark = OnCore(state, PlayerIndex.Opponent, "Bulwark", 0, 40);
            bulwark.AddShield(20);
            int before = state.GetPlayer(PlayerIndex.Opponent).CoreDurability;

            Assert.AreEqual(CommandResult.Success, Attack(state, PlayerIndex.Player, sapper, bulwark));

            List<BattleEvent> events = Drain(state);
            Assert.AreEqual(0, CountOf(events, BattleEventKind.CoreTransferred),
                "GDD 2.1.7: a Core Active that still had Shield up when the blow landed does not transfer.");
            Assert.AreEqual(before, state.GetPlayer(PlayerIndex.Opponent).CoreDurability);

            BattleEvent hit = First(events, BattleEventKind.Damaged);
            Assert.AreEqual(bulwark.InstanceId, hit.SubjectId);
            Assert.AreEqual(12, hit.Amount);
            Assert.AreEqual(8, bulwark.CurrentShield, "Twelve of the twenty held, and none of it reached the card.");
            Assert.AreEqual(40, bulwark.CurrentHp);
        }

        // ---- The pairing invariant ------------------------------------------------------

        /// <summary>
        /// The claim the whole spine is built on, pinned end to end: one exchange produces four facts, and the four
        /// sentences that describe them are the four that already shipped, in the same order. Add a record without
        /// a sentence, or a sentence without a record, or reword either, and this fails - which is the point of
        /// MATCH LOG and the tabletop reading from one call site.
        /// <para>
        /// Both fighters stand on Nodes here on purpose: it is the one board position where a blow cannot leak
        /// durability, so nothing narrates between a record and its sentence and the two line up index for index.
        /// </para>
        /// </summary>
        [Test]
        public void Exchange_PairsEveryRecordWithTheSentenceThatAlreadyDescribedIt()
        {
            var state = new MatchState();
            CardInstance runner = OnNode(state, PlayerIndex.Player, 0, "Runner", 3, 5);
            CardInstance scout = OnNode(state, PlayerIndex.Opponent, 0, "Scout", 2, 1);
            int logBefore = state.EventLog.Count;

            Assert.AreEqual(CommandResult.Success, Attack(state, PlayerIndex.Player, runner, scout));

            List<BattleEvent> events = Drain(state);
            string[] sentences = TailOf(state.EventLog, logBefore);

            Assert.AreEqual(4, events.Count, "Declared, two blows, one death.");
            Assert.AreEqual(events.Count, sentences.Length,
                "Every record rides in beside the sentence that describes it. One more of either is a board and a " +
                "log telling different stories.");

            Assert.AreEqual(BattleEventKind.Attacked, events[0].Kind);
            Assert.AreEqual(scout.InstanceId, events[0].SubjectId,
                "A swing is recorded against the card it came down on, not the card that came down.");
            Assert.AreEqual(runner.InstanceId, events[0].SourceId);
            Assert.AreEqual("Runner attacks Scout", sentences[0]);

            Assert.AreEqual(BattleEventKind.Damaged, events[1].Kind);
            Assert.AreEqual("Runner deals 3 damage to Scout", sentences[1]);

            Assert.AreEqual(BattleEventKind.Damaged, events[2].Kind);
            Assert.AreEqual("Scout deals 2 damage to Runner", sentences[2]);

            Assert.AreEqual(BattleEventKind.Destroyed, events[3].Kind);
            Assert.AreEqual("Scout is destroyed", sentences[3]);
        }

        /// <summary>
        /// Card kinds carry -1 in <see cref="BattleEvent.Player"/> on purpose: a default of
        /// <see cref="PlayerIndex.Player"/> would send every Core pulse the player's way and look like the
        /// opponent having been hit. This is the same exchange as above, read through the field nobody can derive
        /// from the card alone.
        /// </summary>
        [Test]
        public void CardKinds_NeverClaimAPlayer()
        {
            var state = new MatchState();
            CardInstance runner = OnNode(state, PlayerIndex.Player, 0, "Runner", 3, 5);
            CardInstance scout = OnNode(state, PlayerIndex.Opponent, 0, "Scout", 2, 1);

            Assert.AreEqual(CommandResult.Success, Attack(state, PlayerIndex.Player, runner, scout));

            List<BattleEvent> events = Drain(state);
            Assert.AreEqual(4, events.Count);
            foreach (BattleEvent e in events)
            {
                Assert.AreEqual(-1, e.Player,
                    $"{e.Kind} is about a card, and a card's owner is derivable from the instance. " +
                    "Only the Core kinds name a player, because a Core has no owner to look up.");
            }
        }

        // ---- The queue itself -----------------------------------------------------------

        [Test]
        public void DrainEvents_HandsOverEachFactOnceAndThenNothing()
        {
            var state = new MatchState();
            CardInstance runner = OnNode(state, PlayerIndex.Player, 0, "Runner", 3, 5);
            CardInstance scout = OnNode(state, PlayerIndex.Opponent, 0, "Scout", 2, 1);

            Assert.AreEqual(CommandResult.Success, Attack(state, PlayerIndex.Player, runner, scout));

            List<BattleEvent> first = Drain(state);
            Assert.AreEqual(4, first.Count);
            Assert.AreEqual(0, state.PendingEvents.Count, "Draining must empty the queue.");

            List<BattleEvent> second = Drain(state);
            Assert.AreEqual(0, second.Count, "and a second drain must not hand the same facts over again.");
        }

        [Test]
        public void DrainEvents_WithNothingRaised_ReturnsTheSharedEmptyBatch()
        {
            var state = new MatchState();

            IReadOnlyList<BattleEvent> first = state.DrainEvents();
            IReadOnlyList<BattleEvent> second = state.DrainEvents();

            Assert.AreEqual(0, first.Count, "A fresh match has nothing to animate.");
            Assert.AreSame(first, second,
                "The empty case is polled every frame by a presenter with a match in front of it, so it must not " +
                "allocate one empty array sixty times a second.");
        }

        /// <summary>
        /// The batch is a snapshot, not the live list. A presenter holds one across the frames it takes to animate
        /// it while the engine keeps playing, so a returned reference to <see cref="MatchState.PendingEvents"/>
        /// would grow under its feet - and clearing the queue would then be clearing the presenter's own copy out
        /// of the middle of a death animation.
        /// </summary>
        [Test]
        public void DrainedBatch_IsASnapshotTheEngineCannotChange()
        {
            var state = new MatchState();
            CardInstance runner = OnNode(state, PlayerIndex.Player, 0, "Runner", 3, 9);
            CardInstance scout = OnNode(state, PlayerIndex.Opponent, 0, "Scout", 2, 9);

            Assert.AreEqual(CommandResult.Success, Attack(state, PlayerIndex.Player, runner, scout));

            IReadOnlyList<BattleEvent> held = state.DrainEvents();
            int heldCount = held.Count;
            Assert.AreEqual(3, heldCount, "Declared and two blows; nobody died.");

            // A second swing, while the first batch is still in somebody's hands.
            runner.SetReady();
            Assert.AreEqual(CommandResult.Success, Attack(state, PlayerIndex.Player, runner, scout));

            Assert.AreEqual(heldCount, held.Count,
                "The batch already handed over must not have grown; a view iterating a list the engine is still " +
                "appending to is a mutation during enumeration waiting for its timing.");
            Assert.AreEqual(heldCount, state.PendingEvents.Count,
                "and the new facts must be queued behind it, not folded into it.");
        }

        // ---- helpers --------------------------------------------------------------------

        private static CardDefinition Unit(string name, int atk, int hp)
        {
            return new CardDefinition
            {
                Id = "card_" + name,
                Name = name,
                Faction = Faction.Fsti,
                Rarity = Rarity.Common,
                Type = CardType.Unit,
                Cost = 1,
                Atk = atk,
                Hp = hp
            };
        }

        private static CardInstance OnCore(MatchState state, PlayerIndex owner, string name, int atk, int hp)
        {
            var unit = new CardInstance(Unit(name, atk, hp), owner) { State = UnitState.Ready };
            state.GetPlayer(owner).Board.CoreActive[0].Card = unit;
            return unit;
        }

        private static CardInstance OnNode(MatchState state, PlayerIndex owner, int node, string name, int atk, int hp)
        {
            var unit = new CardInstance(Unit(name, atk, hp), owner) { State = UnitState.Ready };
            state.GetPlayer(owner).Board.NodeActive[node][0].Card = unit;
            return unit;
        }

        /// <summary>
        /// Declares one swing and returns the engine's verdict. The phase is set here rather than by each test
        /// because every test in this file is about combat, and asserting the verdict is what stops a rejected
        /// command from passing a test by quietly doing nothing.
        /// </summary>
        private static CommandResult Attack(MatchState state, PlayerIndex who, CardInstance striker, CardInstance target)
        {
            state.CurrentPhase = TurnPhase.Combat;
            return new MatchEngine(state).ExecuteCommand(
                new DeclareAttackCommand(who, striker.InstanceId, target.InstanceId));
        }

        private static List<BattleEvent> Drain(MatchState state)
        {
            return new List<BattleEvent>(state.DrainEvents());
        }

        private static int CountOf(List<BattleEvent> events, BattleEventKind kind)
        {
            int count = 0;
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Kind == kind)
                {
                    count++;
                }
            }
            return count;
        }

        private static BattleEvent First(List<BattleEvent> events, BattleEventKind kind)
        {
            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Kind == kind)
                {
                    return events[i];
                }
            }

            Assert.Fail($"No {kind} record in the batch ({events.Count} event(s) drained).");
            return default;
        }

        private static BattleEvent Only(List<BattleEvent> events, BattleEventKind kind)
        {
            int count = CountOf(events, kind);
            Assert.AreEqual(1, count, $"Exactly one {kind} record was expected in the batch.");
            return First(events, kind);
        }

        private static string LastSentence(MatchState state)
        {
            List<string> log = state.EventLog;
            Assert.IsTrue(log.Count > 0, "The engine narrates every fact it raises a record for.");
            return log[log.Count - 1];
        }

        /// <summary>
        /// Pins a sentence that is not necessarily the last line, because some sites narrate one more thing
        /// afterwards (an ability announces its own resolution once its effects are done). Alignment is asserted
        /// positionally only in the pairing test, where nothing else speaks; everywhere else this asks the narrower
        /// question of whether the sentence the record was raised beside is still the one that shipped.
        /// </summary>
        private static void AssertSentence(MatchState state, string expected, string why)
        {
            CollectionAssert.Contains(state.EventLog, expected, why);
        }

        private static string[] TailOf(List<string> log, int from)
        {
            var tail = new string[log.Count - from];
            for (int i = from; i < log.Count; i++)
            {
                tail[i - from] = log[i];
            }
            return tail;
        }
    }
}
