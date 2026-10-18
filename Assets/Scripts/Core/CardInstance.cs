using System;
using System.Linq;

namespace Overlink.Core
{
    // Runtime card instance with mutable state
    public sealed class CardInstance
    {
        public int InstanceId { get; }
        public string DefinitionId { get; }
        public CardDefinition Definition { get; }
        public int CurrentAtk { get; set; }
        public int CurrentHp { get; set; }
        public int CurrentShield { get; set; }
        public UnitState State { get; set; }
        public bool HasSummoningDelay { get; set; }
        /// <summary>
        /// Set by an <see cref="EffectType.PreventBlock"/> effect for the current combat.
        /// While true an opponent cannot use this unit as the target of a block.
        /// Cleared at the owner's Upkeep; it is a combat-duration flag, not a stat.
        /// </summary>
        public bool CannotBeBlocked { get; set; }
        public PlayerIndex Owner { get; }

        /// <summary>
        /// The Node 1 Core-multiplier ATK bonus currently folded into <see cref="CurrentAtk"/> (0 or 1).
        /// <see cref="MatchState"/> recomputes this from Node control after every action so the bonus is
        /// applied and removed immediately as control changes (GDD 2.1.7).
        /// </summary>
        public int NodeAtkBonus { get; set; }

        /// <summary>
        /// The Node 2 Core-multiplier HP bonus currently folded into <see cref="CurrentHp"/> (0 or 1).
        /// See <see cref="NodeAtkBonus"/>; Node 3 (First Strike) and Node 4 (draw on kill) are query-time
        /// bonuses held by the engine rather than folded into stats.
        /// </summary>
        public int NodeHpBonus { get; set; }

        private static int nextInstanceId = 1;

        public CardInstance(CardDefinition definition, PlayerIndex owner)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            InstanceId = nextInstanceId++;
            DefinitionId = definition.Id;
            Definition = definition;
            Owner = owner;
            CurrentAtk = definition.Atk;
            CurrentHp = definition.Hp;
            CurrentShield = definition.Shield;
            State = UnitState.Summoned;
            HasSummoningDelay = definition.Keywords != null && definition.Keywords.Contains(Keyword.SummoningDelay);
        }

        public bool CanAct()
        {
            return State == UnitState.Ready && !HasSummoningDelay;
        }

        /// <summary>
        /// Whether this card still counts as on the field.
        /// <para>
        /// GDD 4.15.1: Relays and Protocols have no ATK/HP/Shield, so they have no HP to lose. Measuring
        /// them by CurrentHp would report every Relay as dead, which would make
        /// <see cref="BoardSlot.IsOccupied"/> false and silently empty the Node that a Relay controls.
        /// Only Units are lethal-checked; everything else is alive until destroyed outright.
        /// </para>
        /// </summary>
        public bool IsAlive()
        {
            if (State == UnitState.Destroyed)
            {
                return false;
            }

            if (Definition.Type != CardType.Unit)
            {
                return true;
            }

            return CurrentHp > 0;
        }

        public void TakeDamage(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            if (CurrentShield > 0)
            {
                int shieldAbsorbed = Math.Min(CurrentShield, amount);
                CurrentShield -= shieldAbsorbed;
                amount -= shieldAbsorbed;
            }

            if (amount > 0)
            {
                CurrentHp -= amount;
                if (CurrentHp <= 0)
                {
                    CurrentHp = 0;
                    State = UnitState.Destroyed;
                }
            }
        }

        public void Heal(int amount)
        {
            if (amount <= 0 || !IsAlive())
            {
                return;
            }

            // The cap is the printed maximum plus any Node 2 Core-multiplier HP bonus, so healing can
            // restore a Core Active up to its buffed maximum (GDD 2.1.7).
            CurrentHp = Math.Min(CurrentHp + amount, Definition.Hp + NodeHpBonus);
        }

        public void AddShield(int amount)
        {
            if (amount > 0)
            {
                CurrentShield += amount;
            }
        }

        public void SetReady()
        {
            if (State == UnitState.Summoned)
            {
                State = UnitState.Ready;
                if (HasSummoningDelay)
                {
                    HasSummoningDelay = false;
                }
            }
            else if (State == UnitState.Exhausted)
            {
                State = UnitState.Ready;
            }
        }

        public void Exhaust()
        {
            if (State == UnitState.Ready)
            {
                State = UnitState.Exhausted;
            }
        }

        /// <summary>Keyword lookup that tolerates a null keyword list on hand-built definitions.</summary>
        public bool HasKeyword(Keyword keyword)
        {
            return Definition.Keywords != null && Definition.Keywords.Contains(keyword);
        }

        /// <summary>
        /// Permanently changes this unit's attack for the rest of the match. Floor is zero so a Debuff
        /// can never produce a negative attacker.
        /// </summary>
        public void ModifyAtk(int delta)
        {
            if (delta == 0)
            {
                return;
            }

            CurrentAtk = Math.Max(0, CurrentAtk + delta);
        }

        /// <summary>
        /// Permanently changes this unit's current HP. A unit driven to zero HP this way is destroyed,
        /// exactly as if it had taken lethal damage.
        /// </summary>
        public void ModifyHp(int delta)
        {
            if (delta == 0)
            {
                return;
            }

            CurrentHp = Math.Max(0, CurrentHp + delta);
            if (CurrentHp == 0)
            {
                State = UnitState.Destroyed;
            }
        }

        /// <summary>Force-destroys this unit, bypassing Shield. Used by the Destroy effect.</summary>
        public void Destroy()
        {
            CurrentHp = 0;
            State = UnitState.Destroyed;
        }
    }
}
