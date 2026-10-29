namespace Overlink.Core
{
    /// <summary>
    /// What happened in a fight, in a form the board can draw.
    /// <para>
    /// The engine has narrated all of this since the first commit, but as prose: <c>MatchState.EventLog</c> carries
    /// seventy-odd formatted sentences, and the HUD prints them. A sentence is the wrong shape for a tabletop,
    /// because it holds neither an instance id nor an amount - there is no way to put a damage number over the right
    /// card by reading <c>"Vanguard hits the Opponent Core for 1 durability"</c>. These records are the same facts
    /// with the names stripped back out, and they exist so the board can say what happened without learning any
    /// rule that would decide it.
    /// </para>
    /// <para>
    /// Deliberately <em>not</em> a replacement for the log. Every event is raised beside the sentence that already
    /// described it, through <see cref="MatchState.LogEvent(BattleEvent,string)"/>, so the MATCH LOG keeps its
    /// wording and the two can only disagree if somebody writes the pair out of step on purpose. Deriving the prose
    /// from the event instead would mean every turn-phase sentence in the engine having to become expressible as a
    /// record, which is a much larger diff for no extra feedback on the table.
    /// </para>
    /// </summary>
    public enum BattleEventKind
    {
        /// <summary>
        /// A swing was declared. Subject is the defender, Source the attacker, and this is the one kind raised
        /// <em>before</em> anything is decided, which is what gives the board a moment to show what is about to
        /// happen rather than only what happened.
        /// </summary>
        Attacked,

        /// <summary>Damage landed. Subject is the card hurt, Source the card that hurt it - the fighter that swung
        /// in combat, or the card whose ability or Protocol did - and Amount how much.</summary>
        Damaged,

        /// <summary>A card left the board. Subject is the dead card. Source is always -1, and not by omission: the
        /// one place a death is announced runs after the fight has settled, over every slot, so it never knows who
        /// won. The attributed record for a death is the <see cref="Damaged"/> beside it, in the same batch.</summary>
        Destroyed,

        /// <summary>A defender was readied to answer a swing. Subject is the blocker, Source the attacker.</summary>
        Blocked,

        /// <summary>A Bench card stepped into a vacant Active slot. Subject is the card that moved. This is the
        /// half of a death the player otherwise experiences as a stranger standing on their socket.</summary>
        Promoted,

        /// <summary>An unblocked strike reached a Core. Player is the Core's owner, Amount the durability lost -
        /// which is always one, whatever the attacker's ATK, and that is a rule the board should say out loud
        /// rather than let the player infer from a number that never moves. Source is the card that struck, because
        /// "nothing stood in the way" only lands if the player can see what stood nowhere.</summary>
        CoreStruck,

        /// <summary>Damage landed on a Core Active with no Shield up and leaked through to its controller's Core,
        /// at one tenth rounded up (GDD 2.1.7). Player is the Core's owner, Amount what it cost, Source the card the
        /// damage landed on - the one with the number the player can see. Distinguished from
        /// <see cref="CoreStruck"/> because its size varies with the blow, where a direct strike is always one, so
        /// "you leaked durability through your own card" is a different lesson from "nothing stood in the way".</summary>
        CoreTransferred,

        /// <summary>A Core Active was removed from a Core board, which costs its owner one durability. Player is
        /// the owner, Subject the card that died, Amount what it cost. Source is -1 for the same reason
        /// <see cref="Destroyed"/> carries none.</summary>
        CoreSlotLost,
    }

    /// <summary>
    /// One fact from a fight, addressed by instance id.
    /// <para>
    /// Ids rather than references, because a card that dies is removed from its slot and dropped into a discard
    /// pile while this record is still on its way to the board, and a struct holding a <see cref="CardInstance"/>
    /// would be the thing that keeps a dead body alive in whoever cached it. The presenter resolves an id through
    /// the same cache the hand uses, and an id it no longer knows is a card that has left the table - which is
    /// exactly the information a death needs.
    /// </para>
    /// </summary>
    public readonly struct BattleEvent
    {
        public readonly BattleEventKind Kind;

        /// <summary>The card this happened to, or -1 when no card is the subject (a Core losing durability).</summary>
        public readonly int SubjectId;

        /// <summary>
        /// The card responsible, or -1 when nothing on the board was.
        /// <para>
        /// Named where the cause is a card the player can see: the swing in a
        /// <see cref="BattleEventKind.Attacked"/>, the fighter in a <see cref="BattleEventKind.Damaged"/>, the
        /// striker in a <see cref="BattleEventKind.CoreStruck"/>, the card whose leak caused a
        /// <see cref="BattleEventKind.CoreTransferred"/>. Left empty by the two death kinds,
        /// <see cref="BattleEventKind.Destroyed"/> and <see cref="BattleEventKind.CoreSlotLost"/>, which share one
        /// announcement site that runs after the fight and does not know who won.
        /// </para>
        /// </summary>
        public readonly int SourceId;

        /// <summary>
        /// Which player this happened to, as <see cref="PlayerIndex"/>, or -1 when the subject is a card.
        /// <para>
        /// -1 rather than a default of <see cref="PlayerIndex.Player"/> on purpose: a card event that silently read
        /// as "happened to the player" would send every Core pulse the player's way, and the bug would look like the
        /// opponent having been hit. A card's owner is derivable from the instance, so card kinds never need this.
        /// </para>
        /// </summary>
        public readonly int Player;

        /// <summary>How much - damage dealt, durability lost, durability repaired. Zero for the kinds that move
        /// bodies rather than numbers.</summary>
        public readonly int Amount;

        public BattleEvent(BattleEventKind kind, int subjectId, int sourceId, int player, int amount)
        {
            Kind = kind;
            SubjectId = subjectId;
            SourceId = sourceId;
            Player = player;
            Amount = amount;
        }

        /// <summary>A swing declared against a defender, by an attacker.</summary>
        public static BattleEvent Attacked(CardInstance defender, CardInstance attacker)
        {
            return new BattleEvent(BattleEventKind.Attacked, Id(defender), Id(attacker), -1, 0);
        }

        /// <summary>Damage that landed on a card, from whatever hurt it.</summary>
        public static BattleEvent Damaged(CardInstance target, CardInstance source, int amount)
        {
            return new BattleEvent(BattleEventKind.Damaged, Id(target), Id(source), -1, amount);
        }

        /// <summary>A card left the board. Carries no killer by construction - see
        /// <see cref="BattleEventKind.Destroyed"/>.</summary>
        public static BattleEvent Destroyed(CardInstance victim)
        {
            return new BattleEvent(BattleEventKind.Destroyed, Id(victim), -1, -1, 0);
        }

        /// <summary>A defender was readied to answer an attacker.</summary>
        public static BattleEvent Blocked(CardInstance blocker, CardInstance attacker)
        {
            return new BattleEvent(BattleEventKind.Blocked, Id(blocker), Id(attacker), -1, 0);
        }

        /// <summary>A Bench card stepped into a vacant Active slot.</summary>
        public static BattleEvent Promoted(CardInstance card)
        {
            return new BattleEvent(BattleEventKind.Promoted, Id(card), -1, -1, 0);
        }

        /// <summary>An unblocked strike reached a Core. <paramref name="striker"/> is the card that got there.</summary>
        public static BattleEvent CoreStruck(CardInstance striker, PlayerIndex owner, int amount)
        {
            return new BattleEvent(BattleEventKind.CoreStruck, -1, Id(striker), (int)owner, amount);
        }

        /// <summary>Durability leaked through a shield-less Core Active. <paramref name="through"/> is the card the
        /// damage landed on, not the card that dealt it - the board points this at the Core that shrank, and the
        /// <see cref="Damaged"/> record immediately above it already names who hit.</summary>
        public static BattleEvent CoreTransferred(CardInstance through, PlayerIndex owner, int amount)
        {
            return new BattleEvent(BattleEventKind.CoreTransferred, -1, Id(through), (int)owner, amount);
        }

        /// <summary>A Core Active was removed from a Core board, costing its owner durability. Carries no killer for
        /// the same reason <see cref="Destroyed"/> carries none.</summary>
        public static BattleEvent CoreSlotLost(CardInstance dead, PlayerIndex owner, int amount)
        {
            return new BattleEvent(BattleEventKind.CoreSlotLost, Id(dead), -1, (int)owner, amount);
        }

        static int Id(CardInstance card)
        {
            return card != null ? card.InstanceId : -1;
        }
    }
}
