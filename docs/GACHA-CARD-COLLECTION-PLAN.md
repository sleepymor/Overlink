# Overlink Gacha & Card Collection Plan

**Status:** Planning baseline  
**Date:** 2026-10-29  
**Scope:** Card packs, collection, pack-opening presentation, duplicate handling, loyalty rewards, economy, and implementation sequencing.

> This document is a design plan, not an implementation specification. Probabilities, prices, currencies, and card counts must be tuned through simulation and playtesting before production launch.

## 1. High concept

Overlink's collection system gives players a satisfying way to discover, own, inspect, and build decks from the game's five existing card types:

- Creature
- Unit
- Relay
- Ability
- Protocol

The emotional centre is the pack opening: the player swipes upward to tear open a pack and reveal its cards one by one. Players may open one pack or purchase a ten-pack bundle. The collection layer then turns discoveries into decks, crafting decisions, set completion, cosmetics, and long-term progression.

The system must support the battlefield rather than undermine it. Gacha should create discovery and collection goals, but should not make paid purchases the only route to a competitive deck.

## 2. Design pillars

### 2.1 The reveal is the reward

Opening a pack is a short ritual with anticipation, readable rarity feedback, and a satisfying card reveal. The animation must remain enjoyable even when the result is a common card.

### 2.2 Collection, not pay-to-win power

Every gameplay card must be obtainable through gameplay, crafting, events, or another non-paid path. Rarity communicates collectability, complexity, or presentation value; it must not automatically mean battlefield dominance.

### 2.3 Every duplicate has value

A duplicate remains visible in the collection and contributes to a duplicate resource. A player must never receive an opaque result that feels completely wasted.

### 2.4 The collection belongs to the battlefield

The collection, deck builder, and pack contents use the existing card taxonomy and rules. The gacha is not a disconnected character-collection system.

### 2.5 Transparent and recoverable

The player can inspect rates, guarantees, pity progress, ownership, duplicate value, and purchase results. Pack resolution is server-authoritative and recoverable after disconnects.

## 3. Pack model

### 3.1 Standard pack

A standard pack contains **five cards**:

| Slot | Rule |
|---|---|
| 1 | Common or better |
| 2 | Common or better |
| 3 | Common or better |
| 4 | Uncommon or better weighting |
| 5 | Rare-or-better hit slot |

The fifth slot receives the strongest reveal treatment because it is the pack's anticipation point.

### 3.2 Rarity bands

The initial rarity taxonomy is:

- **Common** — foundational cards and accessible effects.
- **Uncommon** — specialized cards and narrower synergies.
- **Rare** — distinctive strategies and build-around pieces.
- **Epic** — advanced mechanics or highly recognizable cards.
- **Legendary** — showcase cards; not automatically stronger.
- **Signature** — alternate art, animated presentation, foil treatment, special frame, or another primarily cosmetic variant.

Signature should normally be cosmetic rather than a separate battlefield-power tier.

### 3.3 Initial probability proposal

These numbers are a tuning baseline only. They must be represented in configuration and surfaced consistently in the UI and server response.

#### Slots 1–3

| Result | Probability |
|---|---:|
| Common | 70% |
| Uncommon | 25% |
| Rare | 5% |

#### Slot 4

| Result | Probability |
|---|---:|
| Common | 35% |
| Uncommon | 45% |
| Rare | 17% |
| Epic or better | 3% |

#### Slot 5

| Result | Probability |
|---|---:|
| Rare | 70% |
| Epic | 22% |
| Legendary | 7% |
| Signature variant | 1% |

The final values must be audited against the actual card pool, set size, duplicate rules, pity rules, and expected collection completion time.

These are **rarity-roll probabilities**, not complete card-selection probabilities. A resolved pack must also identify its banner/set pool, card-type and faction weights, featured-card weights, duplicate-protection state, and any guarantee or pity override. The player-facing rate table must state whether it shows base rarity rates or effective rates after those rules. The server configuration and displayed table must come from the same versioned ruleset.

## 4. Single-pack and ten-pack purchases

### 4.1 One pack

The player can purchase or claim one standard pack. A single pack is intended for casual sessions, free rewards, first-time users, and players who want to experience every reveal at their own pace.

### 4.2 Ten-pack purchase

A ten-pack purchase resolves **ten real standard packs**, for a total of fifty cards. It is not one compressed roll disguised as ten packs.

A ten-pack purchase provides:

- ten standard packs;
- fifty cards total;
- the normal ten-pack guarantee;
- +10 progress toward the lifetime loyalty counter;
- any loyalty milestone reward crossed by the purchase.

After the first pack is revealed in full, the player may choose **Reveal Remaining** to compress the remaining nine packs while still showing every card and every pack boundary.

### 4.3 Ten-pack guarantee

The starting proposal is:

> Every ten-pack purchase guarantees at least one Epic-or-better result across its ten packs.

The guarantee must be resolved server-side and displayed in the transaction result. It must not silently replace a naturally higher result.

For the initial ruleset, the ten-pack guarantee applies only to the ten purchased packs in that bundle. A free loyalty pack is a separate standard pack: it does not contribute to the ten-pack guarantee and cannot be used to satisfy it. Single-pack purchases have no bundle guarantee, but remain eligible for their applicable pity rules.

## 5. Loyalty reward: one free pack per 100 purchased packs

### 5.1 Locked rule

For every **100 purchased packs**, the player receives **one free bonus pack**.

The counter is:

- lifetime and account-bound;
- shared across pack banners and sets;
- advanced by single-pack and ten-pack purchases;
- not advanced by free bonus packs;
- not reset when a banner ends.

### 5.2 Counter behavior

- One-pack purchase: `+1` purchased pack.
- Ten-pack purchase: `+10` purchased packs.
- Free bonus pack: `+0` purchased packs.
- Milestone at 100, 200, 300, etc.: grant one free bonus pack per threshold crossed.

Example:

```text
Before purchase: 96 / 100
Purchase: 10 packs
After purchase: 106 / 100
Reward: 1 free bonus pack
Remaining progress: 6 / 100
```

The implementation should store a cumulative purchased-pack total rather than only a remainder. The number of unclaimed loyalty rewards is derived as:

```text
floor(totalPurchasedPacks / 100) - totalLoyaltyRewardsGranted
```

This supports auditability and prevents threshold loss.

### 5.3 Loyalty ledger and edge cases

The loyalty counter advances only after a pack purchase transaction is committed. Failed payments, rejected requests, and idempotent retries do not advance it twice. A transaction crossing several thresholds grants one reward per threshold crossed; for example, moving from 199 to 301 grants two free packs. Discounted packs and packs purchased with earned Signal Credits count as purchased packs; free event grants do not unless a future ruleset explicitly opts them in.

A reservation released before commit never contributed to the purchased-pack total. **Locked baseline refund policy:** after a purchase commits, never subtract its purchased-pack count, rewind lifetime loyalty progress, delete cards, or revoke a claimed Loyalty Pack. Record a linked commerce reversal, freeze further premium purchases where necessary, and apply the platform refund through a separate negative-money ledger/reconciliation record; the account may retain already-committed rewards while support handles abuse or legal exceptions. A committed but unopened manifest remains owned and is not silently deleted. Every commerce reversal is idempotent by provider event ID and transaction ID, cannot create a negative balance, and emits an audit event. Any future policy that retracts unopened rewards must replace the cumulative-total formula with a per-threshold entitlement ledger before implementation. This is a proposed safety baseline pending platform/legal review, not permission to ship monetization.

Required test cases include 0→100, 99→101, 199→301, failed payment, same-key retry, refund before resolution, refund after resolution, and reward-claim retry.

### 5.4 Loyalty UI

The shop and pack-opening screens should show:

```text
Loyalty progress: 96 / 100 packs
Buy 4 more packs to receive 1 free bonus pack
```

When a milestone is crossed, the reward is placed in the account inbox or reward queue immediately. If the player disconnects, the reward remains claimable.

### 5.5 Loyalty exploit protection

- Free packs do not advance the counter.
- A retry with the same transaction idempotency key cannot grant progress twice.
- Releasing a pre-commit reservation contributes zero; reversing a committed purchase follows §10.8 and never silently rewinds the loyalty counter.
- A completed pack transaction cannot be duplicated by reconnecting.
- The server records the counter before and after every purchase transaction.

## 6. Pack-opening experience

### 6.1 Flow

1. Player selects a set and chooses one pack or ten packs.
2. The shop displays the price, card types, featured cards, rates, guarantee, and loyalty progress.
3. The server validates and resolves the transaction.
4. The pack or pack queue appears.
5. The player swipes upward to tear open the pack.
6. Five cards are revealed one by one.
7. The player sees ownership and duplicate results.
8. The player opens another pack, reveals the remaining bundle, views the collection, or exits.

### 6.2 Sequential reveal

Each card follows a predictable sequence:

1. Card back rises into the reveal area.
2. Energy colour and sound establish anticipation.
3. The card frame materializes.
4. Artwork flips into view.
5. Name, type, and rarity appear.
6. Ownership and duplicate value appear.
7. The card settles into the revealed row.

The presentation must not fake a higher rarity and then downgrade it. Suspense comes from timing and presentation, not misleading feedback.

### 6.3 Ten-pack presentation

The recommended flow is:

1. Ten unopened pack capsules appear in a queue.
2. The first pack opens normally and is fully interactive.
3. After the first pack, `Reveal Next` and `Reveal Remaining` become available.
4. `Reveal Next` opens one pack normally.
5. `Reveal Remaining` shows all remaining cards in a compressed but readable sequence.
6. A final summary displays all ten pack boundaries, cards, duplicates, fragments, guarantees, and loyalty rewards.

The player must always be able to inspect the individual cards after a bundle opening.

### 6.4 Input and accessibility

Swipe is the primary interaction. Also support:

- mouse drag;
- touch swipe;
- controller input;
- keyboard Space/Enter;
- a `Reveal Next` button;
- `Reveal All` or `Reveal Remaining`;
- reduced-motion mode;
- no required haptic feedback;
- rarity indicators that do not rely on colour alone;
- readable text and sufficient contrast.

## 7. Collection screens

### 7.1 Binder view

A visual grid shows:

- card artwork or missing-card silhouette;
- rarity frame;
- owned count;
- playable-copy count;
- new-card marker;
- favourite marker.

### 7.2 Inventory filters

Filter and sort by:

- card type: Creature, Unit, Relay, Ability, Protocol;
- owned or missing;
- playable or duplicate;
- rarity;
- faction or theme;
- set;
- keyword;
- deck legality;
- new or favourite.

### 7.3 Card detail

Card detail displays:

- full artwork;
- card rules;
- card type;
- rarity;
- set;
- owned copies;
- playable copies;
- extra copies;
- acquisition sources;
- alternate versions;
- `Add to Deck`;
- `Craft`;
- `Mark Favourite`.

### 7.4 Set completion

A set page should show separate progress for gameplay cards and cosmetics:

```text
Set 001
Gameplay cards: 43 / 80
Playable copies collected: 39 / 80
Signature variants: 2 / 12
```

Set completion rewards should be profile, presentation, or collection cosmetics. Completing a set should not be the only way to obtain a dominant gameplay card.

## 8. Duplicates and crafting

### 8.1 Ownership model

The collection must distinguish:

```text
Owned: 3
Playable copies: 3
Extra copies: 0
```

A later implementation may cap playable copies according to deck rules, but it must retain the total ownership count for collection history and duplicate conversion.

### 8.2 Duplicate resource

Working name: **Data Fragments**.

Duplicates provide Data Fragments according to rarity. The exact values require economy simulation. The player can see the conversion result immediately after each reveal and in the pack summary.

The deterministic initial rule is: every awarded copy increases total ownership; copies up to the current playable-copy cap remain available for decks; each copy beyond that cap grants its rarity's Data Fragment value. Duplicate protection runs before card selection, so a protected result is not silently converted after the roll. Free packs use the same ownership and conversion rules. If deck limits change later, total ownership history remains intact and only the playable/extra classification is recalculated.

### 8.3 Crafting

Players can spend Data Fragments on missing gameplay cards. Initial cost proposal:

| Rarity | Fragment cost |
|---|---:|
| Common | 50 |
| Uncommon | 150 |
| Rare | 400 |
| Epic | 1,000 |
| Legendary | 2,500 |

These are placeholders for balancing. A player should have a targeted path to a missing card without making random packs meaningless.

### 8.4 Duplicate protection

For new players and early collection progress:

- early packs should prefer cards the player does not own;
- the first few packs should avoid repeated copies of the same Common where possible;
- after the player owns the playable copies of all Commons in a set, excess Common results may preferentially convert to missing Uncommons or Data Fragments.

The server must document exactly when weighted protection applies so displayed rates remain honest. The ruleset must separate rarity-roll probabilities, card-selection weights within each rarity, duplicate-protection weights, and guarantee/pity overrides. The UI must disclose whether its table shows base rarity rates or effective rates for the player's current account state. A simulation test must compare the server's configured rates and the displayed rates for a new account, a near-complete account, and an account under pity.

## 9. Pity and guarantees

Initial proposal:

- ten-pack purchase: at least one Epic-or-better result across its ten purchased packs;
- after 30 eligible packs without a Legendary: the next eligible hit slot becomes Legendary-or-better;
- after 60 eligible packs without the featured card: guarantee the active banner's featured card or its defined crafting-equivalent reward.

### 9.1 Eligibility and precedence

For v1, purchased single packs, purchased ten-pack packs, and free loyalty packs all increment the Legendary pity counter and may satisfy Legendary pity. Free loyalty packs do not satisfy a ten-pack bundle guarantee. Event packs are excluded until an event ruleset opts them in explicitly. The fifth hit slot is the eligible slot for Legendary pity. Featured-card pity is banner-scoped and does not silently target an expired banner.

For a ten-pack transaction, resolve conditions in this order:

1. Resolve featured-card pity if the banner counter reaches its threshold.
2. Resolve Legendary pity for the eligible hit slot if its counter reaches its threshold.
3. Apply the ten-pack Epic-or-better guarantee if no result in the ten purchased packs qualifies.
4. Resolve remaining slots normally.

A naturally qualifying result satisfies and resets the corresponding pity counter. A guarantee or pity result also satisfies and resets it. One card may satisfy multiple conditions; the transaction ledger records each satisfied condition rather than awarding replacement cards.

### 9.2 Featured-card target

Each banner declares featured card IDs and a deterministic selection rule. The active banner ID, target pool, and equivalent reward are stored with pity state. If the player already owns the selected featured card at the playable-copy cap, featured pity awards a selectable missing featured card or a fixed fragment voucher defined by that banner. When a banner ends, its counter and target remain claimable through the banner's stated carryover policy; they must not disappear silently.

Pity counters must be:

- server-side;
- visible to the player;
- account-bound;
- carried across sessions;
- reset only after the guaranteed reward is actually awarded.

The UI must use direct language such as `12 packs until Legendary pity`, not vague language such as `luck is improving`.

## 10. Currency and economy system

The currency system is part of the gacha feature, not a shop detail. Every currency has one clear purpose, a visible balance, auditable transactions, and a defined source/sink relationship.

### 10.1 Currency roles

| Currency | Role | Earned from | Spent on | Transferable? | Expires? |
|---|---|---|---|---|---|
| **Signal Credits** | Free gameplay currency | battles, daily/weekly missions, tutorial, events, collection milestones | standard packs and selected non-premium collection services | No | No |
| **Overlink Crystals** | Optional premium currency | direct purchase and approved promotions | packs and clearly labelled cosmetics | No | No |
| **Data Fragments** | Duplicate/crafting currency | duplicate conversion, collection rewards, events | targeted gameplay-card crafting and selected older-set cosmetics | No | No |
| **Loyalty Packs** | Reward entitlement, not currency | every 100 purchased packs | one standard pack opening | No | Inbox policy |

The first three are balances. A Loyalty Pack is an entitlement in the reward inbox and must not be converted into Signal Credits or Overlink Crystals. None of these currencies may be traded between players at launch.

### 10.2 Currency boundaries

The following conversions are prohibited:

- Overlink Crystals → Signal Credits;
- Signal Credits → Overlink Crystals;
- Data Fragments → either currency;
- Loyalty Packs → currency;
- paid purchase → direct gameplay power outside normal card-acquisition rules.

Keeping balances separate prevents premium currency from obscuring free progression and makes the economy auditable.

### 10.3 Pack price model

Use **Pack Units (PU)** as an internal balancing unit. PU is not shown to players and is not a currency; it makes simulation independent from the eventual currency name or real-money exchange rate.

| Product | Price | Contents | Loyalty progress |
|---|---:|---|---:|
| Single pack | 1.0 PU | 1 standard pack / 5 cards | +1 purchased pack |
| Ten-pack | 9.0 PU | 10 standard packs / 50 cards | +10 purchased packs |
| Loyalty pack | 0 PU | 1 standard pack / 5 cards | +0 purchased packs |

The ten-pack is a proposed 10% quantity discount, not a different random product. Its bundle guarantee still applies. Prices are per currency, not exchangeable balances: the server offer explicitly lists either a Signal Credit price or an Overlink Crystal price, with no split tender. A pack bought with earned Signal Credits still counts as a purchased pack for loyalty; only awarded/free pack entitlements do not. Final player-facing prices and real-money Crystal bundles must be chosen only after free-income, completion-time, and local-currency price simulation. No Crystal-to-PU exchange rate or cash price is locked by this plan. Avoid stranded Crystal remainders: offer a direct local-currency pack purchase or bundle denominations that permit an exact pack purchase, subject to platform rules. Disclose both real-money and Crystal prices before checkout; never advertise the PU discount as an effective cash discount without checking bundle denominations and applicable taxes.

### 10.4 Free-player income target

The free economy must provide a predictable baseline rather than relying only on lucky drops. Initial tuning targets, measured after the tutorial:

- daily missions: **0.25 PU equivalent per day**;
- weekly mission track: **1.0 PU equivalent per week**;
- first-time tutorial and account milestones: **5.0 PU equivalent total**;
- ordinary battlefield play: additional variable rewards, capped by a daily activity budget;
- seasonal/event rewards: bonus packs, Signal Credits, Data Fragments, and cosmetics, budgeted separately.

This baseline is roughly **2.75 PU equivalent per week** before variable battle rewards. The target is one standard pack approximately every 2–3 days from routine play; the final result depends on battle rewards and the final pack price.

For the initial simulation baseline, define **1 standard pack = 100 Signal Credits** and therefore **1 PU = 100 Signal Credits**. The 0.25-PU daily target is exactly 25 Credits and the 1.0-PU weekly target is exactly 100 Credits, paid as integer grants. These are proposed tuning values, not a real-money exchange rate. If the pack price changes, regenerate the whole payout table and retain the nominal-versus-realized comparison; do not silently round away income. Free-pack entitlements are counted separately, not added to the credit-income baseline. Premium currency is never included in free-income metrics. The 2.75 PU weekly baseline is 7 × 0.25 + 1.0; this implies one pack every ~2.55 days without battle rewards only for single-pack purchases. One discounted ten-pack takes ~3.27 baseline weeks, so the final tuning must check both purchase paths and weekly reward timing.

### 10.5 Sources and sinks

**Signal Credits sources:** tutorial completion, first-win and daily battlefield rewards, daily/weekly missions, account and collection milestones, events, and approved compensation grants.

**Signal Credits sinks:** standard single packs, standard ten-pack purchases, and selected non-premium collection services if needed.

**Overlink Crystal sources:** explicit real-money purchase bundles and platform-approved promotional grants. Refunds and chargebacks are commerce-ledger adjustments, not ordinary income.

**Overlink Crystal sinks:** single packs, ten-pack bundles, and clearly labelled cosmetic pack-opening or collection effects.

**Data Fragment sources:** copies beyond the playable-copy cap, collection milestones, events, duplicate-conversion grants, and approved compensation for unavailable rewards.

**Data Fragment sinks:** targeted gameplay-card crafting, selected older-set access, and optional Signature cosmetics if approved.

### 10.6 Caps, expiry, and inflation controls

- Signal Credits, Overlink Crystals, and Data Fragments do not expire.
- Each balance has a server-side maximum of **9,999,999** units unless safely migrated.
- Pre-claim previews warn about cap overflow. At commit the server either atomically records an inbox overflow entitlement with balance unchanged for the excess, or rejects the entire transaction before taking payment. No partial credited reward or silent deletion; redemption retries use the original grant ID.
- Daily and weekly mission payouts have explicit caps and reset times.
- Event rewards state their expiry policy before claim; unclaimed event rewards are not silently converted.
- Duplicate values and crafting costs are versioned. Balance patches affect future transactions and never rewrite committed ledgers.
- There is no passive interest, automatic decay, or forced spending.
- Simulation measures currency inflow, outflow, average balance, pack acquisition rate, time to starter deck, and time to first crafted Rare/Epic. There is no planned Data Fragment-to-pack conversion; crafting is its sink.

### 10.7 Overflow entitlement and claim policy

An overflow entitlement is a durable, non-expiring `CurrencyGrant` record containing `grantId`, account ID, currency type, integer amount, source transaction, ruleset version, creation time, and status (`Pending`, `Claimed`, `Reversed`). It is not a second currency and cannot be spent until claimed. Pending grants are ordered by creation time and remain attached to the account through relaunches. When the balance has room, the player claims a grant with a unique `claimId`; the server atomically marks it claimed and credits the exact amount, or returns the existing result on an idempotent retry. A failed claim changes neither balance nor entitlement. Refunds can reverse a pending grant only through the linked commerce-reversal transaction; a claimed grant follows the committed-refund policy below.

A Loyalty Pack uses the same durable claim pattern but is a `PackEntitlement`, not a currency grant. `claimId` consumption, five-card server resolution, duplicate conversion, pity, loyalty progress (`+0`), durable manifest creation, and entitlement status change commit atomically. Disconnects after commit return the existing manifest; retries never consume the entitlement or roll a second pack. This claim transaction is covered by the same failure-injection and recovery tests as purchases.

### 10.8 Ledger and reversal policy

Balances are server-owned integer minor units; no floating-point balance arithmetic. Every grant, spend, reservation, entitlement, fragment conversion, and reversal has an immutable transaction ID, account, currency, signed delta, reason, ruleset version, and idempotency key. The spend, reward manifest, pity, fragments, and loyalty changes commit atomically as described in §12; clients may show a pending reservation but not spend it twice. Multiple simultaneous requests serialize against the same account balance and loyalty counter.

On an uncommitted payment failure, release the reservation without granting cards or loyalty. **Locked baseline refund policy:** after a purchase commits, never subtract its purchased-pack count, rewind lifetime loyalty progress, delete cards, or revoke a claimed Loyalty Pack. Record a linked commerce reversal, freeze further premium purchases where necessary, and apply the platform refund through a separate negative-money ledger/reconciliation record; the account may retain already-committed rewards while support handles abuse or legal exceptions. A committed but unopened manifest remains owned under this baseline and is not silently deleted. Every commerce reversal is idempotent by provider event ID and transaction ID, cannot create a negative balance, and emits an audit event. Any future policy that retracts unopened rewards must replace the cumulative-total formula with a per-threshold entitlement ledger before implementation. Keep source-of-funds/grant distinctions needed by platform refund law. These rules are a proposed safety baseline pending platform/legal review, not permission to ship monetization.

### 10.9 Economy telemetry and simulation

Collect aggregated, privacy-minimal per-currency grants and spends by source/sink and ruleset; balance distributions and cap hits; free-pack time-to-acquire by cohort; pack-vs-craft choices; duplicate/fragment accumulation; affordable ten-pack time; purchased-pack-to-loyalty-award reconciliation; refund/chargeback reconciliation; and outlier/fraud alerts. Simulate fresh, casual, regular, and lapsed free players across the full first-set cycle, with a zero-battle-reward scenario, using fixed seeded rolls and worst-case pity. Publish a versioned budget sheet with final prices, mission payouts, fragment conversion and crafting cost tables, simulations, and tolerance bands before enabling the live shop.

### 10.10 Monetization safeguards

- Final real-money price is visible before confirmation.
- Ten-pack purchases require explicit confirmation and show the exact card count.
- Purchase history, transaction status, and refund support are accessible.
- No misleading near-miss rarity animations.
- Paid packs do not become the only route to competitive cards.
- Cosmetic products are clearly labelled.
- The player sees rates, guarantees, pity state, loyalty progress, and the currency being spent before confirmation.
- Premium currency is never the only representation of a real-money price.

### 10.11 Economy acceptance criteria

Before monetized implementation is approved, a deterministic economy simulation must prove. This benchmark gate is the same gate represented by §14 Milestone 5 and §15 rules-appendix items 10–11; those sections must be satisfied by the same versioned evidence, not separate informal checklists.

**Benchmark gate:** name at least one legal starter/benchmark deck pool, list its card IDs and copy limits, define its free sources and Fragment crafting costs, and prove from a fresh account that it is obtainable within a documented progression budget. Until this evidence exists, the economy is planning-complete but monetization-blocked.

1. A new free player can build the defined legal starter deck without a random pack result.
2. The free-player baseline earns the documented weekly PU-equivalent amount.
3. A ten-pack costs 9.0 PU and advances loyalty by exactly 10; a Loyalty Pack costs 0 PU and advances it by 0.
4. Every 100 purchased packs grants exactly one Loyalty Pack, including threshold crossings and idempotent retries.
5. Every repeatable currency source has a declared daily, weekly, or event payout budget; simulations demonstrate sustainable inflow/outflow and cap handling without forcibly deleting earned balances.
6. No paid-only gameplay card is required by the benchmark deck pool.
7. Refunds, failed payments, cap overflow, and disconnected claims reconcile without negative balances or duplicated rewards.
8. The server ledger and client-displayed balance agree after every committed transaction.

## 11. First launch set

The first proposed set is **80 cards**, subject to deck-legality review:

- all five card types represented;
- enough cards per faction or archetype to build legal decks;
- a small number of high-rarity showcase cards;
- no gameplay card required for a competitive deck locked behind Legendary rarity;
- 8–12 Signature cosmetic variants;
- one visual theme and one pack-opening environment.

Before content lock, verify:

```text
minimum distinct cards needed = minimum deck size / maximum copies per card
```

For example, a 30-card deck with a three-copy limit needs at least 10 distinct cards. If decks are faction-restricted, that requirement applies independently to each faction or legal deck pool.

## 12. Server-authoritative transaction model

The client presents the animation; it does not decide the rewards.

### 12.1 Durable transaction state machine

A purchase is represented by a durable state machine:

```text
Requested → Reserved → Resolved → Committed → Presented → Acknowledged
```

- **Requested:** idempotency key accepted; no currency or reward mutation yet.
- **Reserved:** currency is held and the offer/ruleset is pinned.
- **Resolved:** card results, duplicate conversion, pity, and loyalty deltas are calculated but not yet exposed as final inventory state.
- **Committed:** currency, inventory, pity counters, purchased-pack total, and loyalty reward queue are written atomically in one durable ledger operation.
- **Presented:** the client has received the committed manifest; presentation is optional for reward ownership.
- **Acknowledged:** the client confirms display, but acknowledgement does not determine whether the reward exists.

If the server fails between states, retrying the same idempotency key resumes or returns the existing transaction. It never rolls a second reward. Failure-injection tests must cover currency reservation, resolution, commit, manifest delivery, and client disconnect at each boundary.

### 12.2 Required flow

1. Client requests a pack purchase with an idempotency key.
2. Server validates the account, currency, set, pack count, and current shop offer.
3. Server reserves the currency and pins the versioned ruleset.
4. Server resolves card rewards using server-side randomness.
5. Server applies duplicate, pity, guarantee, and loyalty rules.
6. Server atomically commits the transaction and reward manifest.
7. Server returns the already-resolved manifest.
8. Client animates the result.
9. Client acknowledges presentation; the reward remains stored regardless.

Each transaction records:

- transaction id;
- idempotency key;
- account id;
- set id;
- pack count;
- currency and cost;
- card rewards;
- duplicate conversion;
- pity state before and after;
- purchased-pack total before and after;
- loyalty rewards granted;
- ruleset version, banner ID, and guarantee/pity conditions satisfied;
- timestamp;
- transaction status.

If the player disconnects after payment, reopening the gacha screen must show:

```text
Unclaimed rewards available
```

The player must never be charged twice or receive a second roll because the first animation was interrupted.

## 13. Analytics and balance telemetry

Track events without collecting unnecessary personal information:

- shop viewed;
- pack type selected;
- purchase confirmed;
- purchase failed;
- pack opened;
- card revealed;
- rarity result;
- duplicate result;
- crafting action;
- ten-pack reveal skipped or completed;
- loyalty milestone reached;
- free bonus pack claimed;
- unclaimed reward recovered;
- collection and deck changes.

Use these metrics to tune pacing, economy, and usability. Do not use them to hide rates or manipulate players with misleading scarcity.

## 14. Implementation milestones

### Milestone 1 — Offline pack-opening prototype

Build deterministic test data only:

- one-pack screen;
- pack animation;
- swipe-up sequential reveal;
- five-card result;
- duplicate display;
- collection update;
- reveal-next and reveal-all controls.

Acceptance:

- all five cards reveal in order;
- drag, button, keyboard, and controller alternatives work;
- reduced-motion mode skips unnecessary effects;
- leaving and returning to the screen does not lose the resolved test result.

### Milestone 2 — Collection data model

Add:

- card catalog;
- set definitions;
- rarity and card type;
- ownership counts;
- duplicate fragments;
- collection filters;
- card detail;
- deck-builder integration.

Acceptance:

- ownership survives relaunch;
- missing cards are visible;
- duplicates are counted correctly;
- cards can be added to legal decks;
- card type and set filters match the existing battlefield taxonomy.

### Milestone 3 — Ten-pack flow

Add:

- ten-pack queue;
- ten-pack guarantee;
- per-pack result grouping;
- reveal-next and reveal-remaining modes;
- final summary;
- loyalty progress simulation.

Acceptance:

- ten packs produce exactly fifty cards;
- each result is attributable to one pack;
- the guarantee is visible and auditable;
- a ten-pack purchase advances loyalty by exactly ten;
- crossing 100 grants exactly one free bonus pack.

### Milestone 4 — Server-authoritative resolution

Add:

- transaction records;
- server-side rolls;
- pity state;
- loyalty state;
- idempotency;
- retry handling;
- unclaimed reward recovery;
- anti-duplication validation.

Acceptance:

- disconnects do not lose rewards;
- retries do not duplicate rewards or loyalty progress;
- the client cannot alter the reward manifest;
- purchase and reward ledgers reconcile.

### Milestone 5 — Economy and live tuning

Add:

- real currencies;
- shop configuration;
- pity display;
- collection milestones;
- events;
- analytics;
- probability and cost simulation;
- integer-unit currency ledger, per-currency offer pricing, source/sink configuration, grants and crafting costs;
- refund/chargeback reconciliation, overflow entitlement recovery, and seeded economy simulations (§10).

Acceptance:

- displayed rates equal server configuration;
- costs and rewards can be simulated before release;
- a player can obtain a legal starter deck without depending on random luck;
- the first set has a documented free/craftable acquisition path;
- a defined free-play test account constructs at least one legal starter deck within a named progression budget;
- the benchmark deck pool and competitive-power comparison are named before monetized implementation;
- no gameplay card required by the benchmark pool is exclusive to paid packs.

## 15. Rules appendix required before live implementation

Before Milestone 3 or any monetized implementation, the following machine-readable rules must exist in server configuration and be covered by fixtures:

1. Pack and banner identifiers.
2. Card-pool membership and within-rarity selection weights.
3. Card-type, faction, and featured-card weights.
4. Signature treatment and guarantee eligibility.
5. Guarantee precedence and eligible slots.
6. Pity scope, increment timing, reset timing, and eligible pack types.
7. Duplicate-protection and effective-rate disclosure.
8. Loyalty/refund state transitions and threshold examples.
9. Transaction state transitions and failure recovery.
10. Free-acquisition and starter-deck acceptance tests.
11. Integer currency denominations, per-currency offers and checkout bundle denominations, daily/weekly earning caps and reset times, grants/sinks, balance caps, overflow entitlement rules, refund handling, and economy simulation tolerances. Item 10 must point to the §10.11 benchmark gate; item 11 must use the same evidence and tolerance values required by §10.11 and §14 Milestone 5.

The rules appendix must define whether Signature is an independent cosmetic overlay or a mutually exclusive result, whether it counts toward rarity guarantees, and how a featured-card pity target behaves when the player already owns it or its banner expires.

## 16. Decisions locked for the current plan

1. Standard pack contains five cards.
2. Players can purchase one pack or ten packs.
3. Ten packs resolve as ten real packs and produce fifty cards.
4. Ten-pack purchases include an initial Epic-or-better guarantee proposal.
5. Every 100 purchased packs grants one free bonus pack.
6. Free bonus packs do not advance the purchased-pack counter.
7. Loyalty progress is lifetime, account-bound, and shared across banners.
8. Pack reveals proceed by upward swipe, with non-swipe accessibility alternatives.
9. Duplicates become Data Fragments.
10. Gameplay cards remain obtainable outside paid gacha.
11. Pack results are server-authoritative.
12. Player trading is deferred for launch.
13. First launch set target is 80 cards, pending deck-legality and economy review.

## 17. Open decisions before implementation

- Final per-currency pack prices, premium bundle denominations and local-currency checkout policy. The planning baseline uses 100 Signal Credits per standard pack and 25/100-Credit daily/weekly grants; final prices remain a tuning decision.
- Benchmark deck card IDs, free acquisition routes, Fragment conversion values, crafting costs, and fresh-account progression budget. Monetization remains blocked until this evidence exists.
- Whether premium currency is available at the first public launch.
- Exact rarity probabilities after simulation.
- Exact duplicate-to-Fragment values.
- Exact pity thresholds and whether pity is banner-specific or global.
- Final first-set size after faction/deck validation.
- Whether Signature variants can be crafted or only earned through special collection rewards.
- Platform/legal review of the locked no-retraction refund baseline, chargeback handling, and age/region purchase controls; revise only with a replacement per-threshold ledger design.
- Mail/inbox implementation for free loyalty packs and disconnected transactions.
- Whether old sets remain permanently available or rotate through a shop.

The first implementation should be the offline single-pack reveal prototype. Do not begin with monetization or live-service infrastructure: validate the swipe rhythm, anticipation, readability, and collection feedback with deterministic cards first.
