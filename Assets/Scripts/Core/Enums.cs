namespace Overlink.Core
{
    public enum CardType
    {
        Unit,
        Ability,
        Relay,
        Protocol
    }

    public enum Rarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    public enum Faction
    {
        Fsti,
        Frti,
        Fpb
    }

    public enum Keyword
    {
        Piercing,
        FirstStrike,
        Lifesteal,
        Draw,
        AoE,
        CannotBlock,
        Activate,
        Deploy,
        Fast,
        Shield,
        Heal,
        SummoningDelay,
        Persistent,
        Exhaust,
        Repair
    }

    public enum EffectType
    {
        DealDamage,
        DealAoEDamage,
        Heal,
        Draw,
        GainNP,
        Buff,
        Debuff,
        GrantShield,
        Destroy,
        Summon,
        Move,
        Exhaust,
        AddKeyword,
        SwitchPosition,
        PreventBlock,
        Repair
    }

    public enum TargetKind
    {
        Self,
        Commander,
        EnemyCommander,
        AlliedActive,
        EnemyActive,
        AllAlliedActive,
        AllEnemyActive,
        Node,
        EnemyNode,
        Deck,
        Discard,
        Hand
    }

    public enum Severity
    {
        Error,
        Warning,
        Info
    }
}
