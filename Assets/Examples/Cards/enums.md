# Card Enums Reference (Overlink TCG)

Valid enum values used by `CardDefinition` / `CardJson`. These match `Assets/Scripts/Core/Enums.cs`.
Values are case-sensitive in JSON (C# enum names).

## Faction
| JSON token | C# name | Identity |
|---|---|---|
| `fsti` | `Fsti` | Frontier Science and Technology Institute — signal/data control |
| `frti` | `Frti` | Frontier Research and Technology Institute — industrial combat |
| `fpb` | `Fpb` | Frontier Planning Bureau — attrition and defence |

Card IDs must match `^card_(fsti\|frti\|fpb)_\d{3}$` and the token must match the `faction` field (`CardValidator.cs` line 13).

## Rarity
`Common`, `Rare`, `Epic`, `Legendary`

MVP library target: 100 cards (50 Common / 30 Rare / 15 Epic / 5 Legendary).

## CardType
`Unit`, `Ability`, `Relay`, `Protocol`

- Units map to Active/Bench board positions.
- Ability is a one-shot effect with no board slot.
- Relay and Protocol are designed in the GDD but not yet implemented (deferred to post-MVP).

Non-Unit cards must have `atk=0, hp=0, shield=0`.

## Cost
Integer 0–10 inclusive (`CardValidator.MaxCost = 10`).

## Keyword
`Piercing`, `FirstStrike`, `Lifesteal`, `Draw`, `AoE`, `CannotBlock`, `Activate`, `Deploy`, `Fast`, `Shield`, `Heal`, `SummoningDelay`, `Persistent`, `Exhaust`, `Repair`

Validation notes:
- `Shield` requires `shield > 0`.
- `Draw` requires a `Draw` effect; `Heal` requires a `Heal` effect; `Repair` requires a `Repair` effect.
- `AoE` requires a `DealAoEDamage` effect.
- `Fast` requires at least one effect with `fast: true`.
- Unit-only keywords (`Piercing`, `FirstStrike`, `Lifesteal`, `Shield`, `SummoningDelay`, `CannotBlock`) require `atk > 0` or `hp > 0`.

## EffectType
`DealDamage`, `DealAoEDamage`, `Heal`, `Draw`, `GainNP`, `Buff`, `Debuff`, `GrantShield`, `Destroy`, `Summon`, `Move`, `Exhaust`, `AddKeyword`, `SwitchPosition`, `PreventBlock`, `Repair`

- `Summon`, `Move`, `AddKeyword` require a non-empty `referenceId` that exists in the card pool.
- `Repair` targets only `Self` or `Commander`, and `amount` must be at most 3 (`MaxRepairAmount`).
- Effects needing magnitude (`DealDamage`, `Heal`, `Draw`, `Buff`, etc.) require `amount > 0`.

## TargetKind
`Self`, `Commander`, `EnemyCommander`, `AlliedActive`, `EnemyActive`, `AllAlliedActive`, `AllEnemyActive`, `Node`, `EnemyNode`, `Deck`, `Discard`, `Hand`

## EffectDescriptor fields
`type`, `target`, `amount`, `amount2`, `duration`, `referenceId`, `fast`

## Top-level wrapper (CardJson)
```json
{
  "formatVersion": 1,
  "cards": [ ... ],
  "composition": {
    "total": <int>,
    "byRarity": { "common": <int>, "rare": <int>, "epic": <int>, "legendary": <int> },
    "byFaction": { "fsti": <int>, "frti": <int>, "fpb": <int> }
  }
}
```

`formatVersion` must be `1`. Unknown keys are skipped on parse.
