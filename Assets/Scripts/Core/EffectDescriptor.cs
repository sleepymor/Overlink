namespace Overlink.Core
{
    public readonly struct EffectDescriptor
    {
        public const int InstantDuration = 0;
        public const int PermanentDuration = -1;

        public EffectType Type { get; }
        public TargetKind Target { get; }
        /// <summary>Primary magnitude. For Buff/Debuff this is the ATK delta.</summary>
        public int Amount { get; }
        /// <summary>Secondary magnitude. For Buff/Debuff this is the HP delta.</summary>
        public int Amount2 { get; }
        /// <summary>Instant (0), permanent (-1), or a positive duration in turns.</summary>
        public int Duration { get; }
        /// <summary>
        /// ReferenceId carries a card identity per GDD 4.15.2, and EffectDescriptor has no dedicated keyword payload field
        /// because the GDD defines no such field, so an AddKeyword effect cannot name which keyword it grants until that
        /// contract is specified.
        /// </summary>
        public string ReferenceId { get; }
        public bool Fast { get; }

        public EffectDescriptor(
            EffectType type,
            TargetKind target,
            int amount = 0,
            int amount2 = 0,
            int duration = InstantDuration,
            string referenceId = null,
            bool fast = false)
        {
            Type = type;
            Target = target;
            Amount = amount;
            Amount2 = amount2;
            Duration = duration;
            ReferenceId = referenceId;
            Fast = fast;
        }
    }
}
