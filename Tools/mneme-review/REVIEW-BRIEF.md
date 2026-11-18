# Mneme Review Brief — 150-Card Pool for Overlink TCG

**All paths below are ABSOLUTE.** You are read-only; everything you need is in
`E:\Games\Overlink\Tools\mneme-review\` plus read access to the repo.

## Objective

Master asked for 150 well-balanced card definitions for the Overlink TCG:
- 150 cards total; a single name may appear at multiple rarities but each entry is
  a distinct card with its own ID (in the delivered pool all 150 IDs AND all 150
  names are unique).
- Free composition across card types (Unit/Ability/Relay/Protocol).
- Names must NOT be edgy; draw inspiration from faction themes:
  - FSTI: Science & Technology
  - FRTI: Industrial / Mechanical
  - FPB: Drawing / Planning / Art (per Master's instruction; the repo AGENTS.md
    describes FPB as attrition/defence — both angles were used).
- Cards must pass `CardValidator` rules and be parseable by `CardJson`.

## Delivered artifact

`E:\Games\Overlink\Assets\Cards\card_pool.json`
- 102,820 bytes, written 2026-11-13 00:40:22 local.
- Copy for your convenience: `E:\Games\Overlink\Tools\mneme-review\card_pool.json`
- formatVersion: 1
- composition block: total=150, common=75/rare=45/epic=22/legendary=8,
  fsti=50/frti=50/fpb=50 (composition block matches actual card counts — verified).
- Types: Unit=88, Ability=35, Relay=11, Protocol=16.

Note: Relay and Protocol are valid `CardType` enum values in `Enums.cs`, but the
repo AGENTS.md says Relay/Protocol are "designed in GDD, not yet implemented —
deferred to post-MVP" in the runtime. The validator accepts them
(`ValidateIdentity` only checks `Enum.IsDefined`). Including them is a design
choice consistent with Master's "free composition" instruction. Flag this if you
judge it a material risk for the runtime (e.g. deckbuilder or match engine might
not handle Relay/Protocol cards).

## Evidence files (all in E:\Games\Overlink\Tools\mneme-review\)

1. `card_pool.json` — the delivered artifact (copy).
2. `CardValidator.cs` — the REAL validator (copy of
   `E:\Games\Overlink\Assets\Scripts\Core\CardValidator.cs`). Read this to check
   my Python re-implementation faithfully.
3. `Enums.cs` — real enum definitions.
4. `enums.md` — project's own enum reference doc (copy of
   `E:\Games\Overlink\Assets\Examples\Cards\enums.md`).
5. `validate_card_pool.py` — my Python re-implementation of CardValidator rules
   used to check the pool. Result: 150 cards, 0 errors, 0 warnings, 0 infos.
6. `validation_output.txt` — captured validator output + composition + uniqueness.
7. `generate_card_pool.py` — the generator script that produced the pool (card
   data is inline in this file; the JSON is derived, not hand-typed).

## What I verified myself

- Python re-validation against CardValidator.cs rules: 0 errors / 0 warnings.
- 150 unique IDs, 150 unique names, composition block matches counts.
- No .NET SDK on this host (`dotnet --version` → no SDKs found), so the real C#
  validator/tests were NOT executed. That is the main gap: my Python validator
  could have drifted from the real C# rules.

## Specific questions for you

1. **Validator fidelity:** Read `CardValidator.cs` and `validate_card_pool.py`.
   Does my Python re-implementation miss or misstate any rule? In particular:
   - Unit-only keyword warning on non-Unit cards (I warn but do not error — matches C#?).
   - `NeedsMagnitude` set (C# includes Exhaust, PreventBlock, AddKeyword, Summon, Move —
     did I include all?).
   - Repair target/amount cap.
   - Fast-effect-without-Fast-keyword warning (not error).
   - The `HasEffect`/`HasAnyFastEffect` semantics.
2. **Spot-check cards by hand:** pick ~10 cards across factions/rarities/types in
   `card_pool.json` and manually trace them through the C# validator logic. Any errors?
3. **Keyword/effect consistency:** do abilityText strings match the effects array
   (e.g. cards claiming "Draw 2" whose effects have amount 2)? Sample broadly.
4. **Balance sanity:** costs vs stats vs rarity — any egregious outliers
   (e.g. a Common strictly better than an Epic, a 1-cost with too much value)?
   The user asked for "well balanced"; give your independent read.
5. **Naming/theme fit:** any names that read as edgy, off-theme for their faction,
   or generic enough to be confusing? Master explicitly said no edgy names.
6. **Summon/Move/AddKeyword referenceIds:** the C# validator requires referenced
   IDs to exist in the pool. My pool uses no Summon/Move/AddKeyword effects at all —
   confirm this is true in the file (search for those effect types).
7. **artPath:** all point at `Assets/Art/Cards/<id>.png` which does not exist yet.
   Validator treats missing ArtPath as Info only; non-existent paths are not checked.
   Confirm no code path in the repo requires these files to exist at load time
   (check `E:\Games\Overlink\Assets\Scripts\Cards\` if needed) — or flag as a risk.

## Verdict format

Reply with VERIFIED / FAILED / INCONCLUSIVE plus findings. If you cannot read a
file, say which one — do not guess.
