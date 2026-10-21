# DEVLOG #01 — "We're Building Overlink With a Team of AI Agents"

**Target length:** 3–5 minutes (~700 narration words @ ~150 wpm ≈ 4:40)
**Voice:** first person, calm, honest, a little dry
**Status note for editor:** every claim below is true as of 2026-10-18. Do not add "tests pass" or "release date" claims — neither exists yet. Lines marked [CUT] can be dropped to hit 3:00 without breaking the flow.

---

## 0:00–0:15 — Cold open

**Narration:**
"Every card game pitch has the same sentence in it. 'Imagine Hearthstone, but...' — and then somebody says something about territory. Well. Here's mine."

**B-roll:** black screen, one card slides in, title reveal: **OVERLINK**.

---

## 0:15–0:40 — The hook

**Narration:**
"Overlink is a tactical card battler. You and your opponent each control a Core, and between you sits a grid of Nodes. Cards don't just trade damage — where your units are *standing* decides who gets paid. Hold a Node, you gain extra energy every turn and a permanent buff to your whole lineup. Lose it, the buff dies instantly. Win the board position, and the math starts winning for you."

**B-roll:** battlefield mockup/prototype camera pan over the four Nodes; a unit deploy animates into a Node Active slot; NP counter ticking up.

---

## 0:40–1:35 — The rules, in sixty seconds

**Narration:**
"The match runs on six phases: Upkeep, Draw, Main, Combat, End, Cleanup. In Upkeep you gain one energy — plus one more for every Node you control. In Main you deploy; in Combat you attack and block.

Three ways to win. Wreck their Core. Dominate the board — control three of the four Nodes at the start of your turn. Or grind them out until they can't draw.

Deck rules keep it honest: thirty to forty cards, max three copies, one faction in the MVP. Right now that's FSTI — the net-holders. Persistent support cards live in Bench slots, and each player gets exactly one Protocol slot, so a Protocol replaces the last one instead of stacking. One Protocol, big decision.

[CUT] Effects are data, not code. Every card is an ordered list of typed effect descriptors, all of it validated at three points: while you edit, in the manager tool, and again at runtime startup. A card that could never work can't reach a build."

**B-roll:** phase tracker HUD advancing; quick overlays labelling Core / Nodes / Bench / Protocol; win-condition icon flashes.

---

## 1:35–2:50 — The actual devlog part: how it's being built

**Narration:**
"Now — the part that makes this a devlog and not a trailer. I'm building this with a small team, and 'team' here includes AI agents. A coordinator, an executor that writes the code, and a reviewer whose entire job is to disagree with me before the code reaches a build.

And honestly? The reviewer earned her keep this week.

We have a game design document — over a hundred pages, versioned, locked with a checksum so nobody can quietly edit it. The agents were building against a plan derived from that doc, and a static review caught two places where the plan and the document had quietly drifted apart.

One: the plan had the board at three Nodes. The document says four. Three versus four isn't a typo — it changes the whole win condition, because domination means three of four, not two of three.

Two — [CUT] this one is worse. The plan had Core durability at twenty. The document says three. Twenty is a war of attrition. Three is every hit matters and blocking is life or death. Completely different game. Nobody noticed because the plan was written before the final document revisions landed.

Both got fixed in code this week. Node count, domination threshold, durability, the full six-phase turn with damage transfer and the Node buffs. And the reviewer is still running — she failed our last check-in on three counts I'll talk about in the next log."

**B-roll:** terminal scrolling, git status churn, the GDD PDF next to the plan doc, a side-by-side of the two diffs; red FAILED verdict card on screen.

---

## 2:50–3:40 — Where it stands right now

**Narration:**
"So here's the honest status picture. The rules engine and the battlefield presentation exist in code. There's a playable scene with the grid, the hand UI, drag-to-deploy, a deterministic AI opponent that has to pass the same legality checks you do — it can't cheat, which means when it wins, it won fair.

There's a twenty-card pilot set, and a card manager window for authoring and validating them.

[CUT] What's not done: the exported JSON the runtime is supposed to load is still empty, the scene isn't in the build list yet, and the combat targeting has a flaw the reviewer caught where an attack can grab a target across the wrong battlefield. And none of it has been compiled or tested by me on camera yet, because the Unity editor holds a lock file until we're done writing.

That's the real state of the project, not the polished version. I'd rather show you this than a highlight reel."

**B-roll:** gameplay scene under construction, wireframe battlefield with slot labels, HUD log panel, cards fanned in hand; the empty `cards.json` shown briefly for honesty.

---

## 3:40–4:40 — Next log + outro

**Narration:**
"Next up: fixing the targeting scoping, generating the real card JSON and having the game actually load it, putting the scene into the build list, and then turning Unity back on for a proper compile-and-test session on camera. After that, twenty cards tuned into a deck that plays like the design doc says it should, and a match that survives contact with the reviewer.

If you want to watch an AI-assisted pipeline either work spectacularly or fail in public — subscribe, I'll be doing both here.

Overlink. Slafurry Studios. Thanks for watching."

**B-roll:** roadmap checklist items sliding into place; final Overlink logo card; itch.io mention only if already set up, otherwise leave it off.

---

## Alternate cold opens

1. **Numbers hook:** "Two numbers were wrong in our build plan this week. One of them turned a three-hit game into a twenty-hit game. Let me show you how we caught it."
2. **Reviewer hook:** "I have a teammate whose job is to tell me I'm wrong. Today she did it in writing, with line numbers."
3. **Gameplay-first hook:** open on the Node-control NP tick, then "this is the mechanic the whole game hangs on."

## Claims to avoid (accuracy guardrails)

- Don't say tests passed, the game "compiles", or a match "completed" — Unity verification is still pending and nothing has been executed.
- Don't state release dates, prices, or a drop date.
- Don't promise online PvP; MVP is single-player by design.
- Keep distribution vague until itch.io page actually exists.
- Card distribution (11/4/2/3 vs the plan's 11/4/3/2) is an open decision — say "twenty-card pilot set", not a type breakdown, until it's locked.

## Editing notes

- Runtime estimate: cold open → outro ≈ 4:30–4:40. Cutting both [CUT] blocks lands ≈ 3:30.
- The 1:35–2:50 segment is the emotional core; give it the most screen time and the most visuals.
- If recording screen capture live, the "under construction" section works best with the Gameplay scene actually open — do it after the Unity lock is legitimately released.
