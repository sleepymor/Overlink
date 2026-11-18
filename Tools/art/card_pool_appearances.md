# Overlink — Card Pool Appearance Descriptions

Visual reference for the 150-card pool (`Assets/Cards/card_pool.json`), grouped by faction.
One section per card with grounded art direction drawn from the card's real stats,
ability text, design note, and the prompt subject/focal/props from the art pipeline
descriptions. Rarity cues escalate with cost/power (Common = plain/workwear,
Legendary = gilded/iconic).

This is a DESIGN DOCUMENT ONLY. The SDXL positive prompt itself keeps **at most
3 named props** per card (`Tools/art/descriptions_*.py`); this document is not
fed to the generator and the verified prompt pipeline is unchanged.

## FSTI — Frontier Science & Technology Institute

*(clean labs, holographic displays, sensors, data streams — cool blues, whites, glass, chrome)*

### Common

#### Lab Intern (`card_fsti_001`)

FSTI · Common · Unit · cost 1 | atk 1 | hp 2 | shield 0 | keywords: Deploy

**Ability:** Deploy: Draw 1 card.

**Design intent:** Cheap cantrip body. Basic card-draw engine.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead.
- **Pose:** steady working stance, alert.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** humming lab bench; data tablet; safety goggles.
- **Rarity cue:** plain / workwear / utilitarian

#### Relay Operator (`card_fsti_002`)

FSTI · Common · Unit · cost 2 | atk 2 | hp 2 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** Vanilla 2-drop. Efficient stats for early board.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead.
- **Pose:** steady working stance, alert.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** wall of signal relays; operator headset; glowing data lines.
- **Rarity cue:** plain / workwear / utilitarian

#### Data Clerk (`card_fsti_003`)

FSTI · Common · Unit · cost 1 | atk 1 | hp 1 | shield 0 | keywords: Deploy

**Ability:** Deploy: Gain 1 NP.

**Design intent:** Cheap NP accelerator. Trades board presence for tempo.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead.
- **Pose:** steady working stance, alert.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** dual-screen terminal; reflected number streams.
- **Rarity cue:** plain / workwear / utilitarian

#### Frequency Scanner (`card_fsti_004`)

FSTI · Common · Unit · cost 2 | atk 1 | hp 3 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** Defensive 2-drop. Higher HP for holding positions.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead.
- **Pose:** steady working stance, alert.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** handheld scanner; swept waveform; oscilloscope glow.
- **Rarity cue:** plain / workwear / utilitarian

#### Network Technician (`card_fsti_005`)

FSTI · Common · Unit · cost 3 | atk 2 | hp 3 | shield 0 | keywords: Deploy

**Ability:** Deploy: Draw 1 card.

**Design intent:** Mid-cost body with card draw. Bread and butter.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead.
- **Pose:** steady working stance, alert.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** server rack; pulsing fiber cables.
- **Rarity cue:** plain / workwear / utilitarian

#### Optics Researcher (`card_fsti_006`)

FSTI · Common · Unit · cost 3 | atk 3 | hp 2 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** Aggressive 3-drop. Glass cannon profile.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead.
- **Pose:** steady working stance, alert.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** benchtop laser rig; glass prism; split beam.
- **Rarity cue:** plain / workwear / utilitarian

#### Patch Compiler (`card_fsti_007`)

FSTI · Common · Unit · cost 2 | atk 2 | hp 1 | shield 0 | keywords: Deploy

**Ability:** Deploy: Gain 1 NP.

**Design intent:** Low HP NP generator. Expendable accelerator.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead.
- **Pose:** steady working stance, alert.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** curved monitor; cascading code; green build glyph.
- **Rarity cue:** plain / workwear / utilitarian

#### Survey Drone (`card_fsti_008`)

FSTI · Common · Unit · cost 1 | atk 1 | hp 1 | shield 0 | keywords: Piercing

**Ability:** Piercing.

**Design intent:** Cheapest piercing unit. Chip damage through shields.

**Art direction:**

- **Subject:** Mecha — structure-centred machine subject, no humans
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** quad-rotor drone; sensor pod; projected scan grid.
- **Rarity cue:** plain / workwear / utilitarian

#### Lab Workstation (`card_fsti_009`)

FSTI · Common · Unit · cost 4 | atk 2 | hp 4 | shield 0 | keywords: Activate, Draw

**Ability:** Activate: Draw 1 card.

**Design intent:** Repeatable draw engine. High HP to survive.

**Art direction:**

- **Subject:** Mecha — structure-centred machine subject, no humans
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** research bench; robotic arm; microscope array.
- **Rarity cue:** plain / workwear / utilitarian

#### Bandwidth Monitor (`card_fsti_010`)

FSTI · Common · Unit · cost 3 | atk 2 | hp 2 | shield 0 | keywords: Deploy

**Ability:** Deploy: Gain 2 NP.

**Design intent:** Bigger NP burst on deploy. Worth the 3-cost.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead.
- **Pose:** steady working stance, alert.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** wall of throughput gauges; climbing green graphs.
- **Rarity cue:** plain / workwear / utilitarian

#### Campus Courier (`card_fsti_011`)

FSTI · Common · Unit · cost 2 | atk 2 | hp 2 | shield 0 | keywords: Deploy

**Ability:** Deploy: Buff an allied active unit +1 ATK this turn.

**Design intent:** Cheap tempo swing. Pairs well with aggressive openers.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead.
- **Pose:** steady working stance, alert.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** courier satchel; signal device; campus walkway.
- **Rarity cue:** plain / workwear / utilitarian

#### Waveform Analyst (`card_fsti_012`)

FSTI · Common · Unit · cost 4 | atk 3 | hp 3 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** Solid vanilla 4-drop. Reliable mid-game body.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead.
- **Pose:** steady working stance, alert.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** 3D holographic wave; desk console.
- **Rarity cue:** plain / workwear / utilitarian

#### Calibration Probe (`card_fsti_013`)

FSTI · Common · Unit · cost 0 | atk 1 | hp 1 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** Free 1/1. Board filler, combo enabler.

**Art direction:**

- **Subject:** Mecha — structure-centred machine subject, no humans
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** tripod instrument; fine measuring beam; calibration target.
- **Rarity cue:** plain / workwear / utilitarian

#### Spectrum Mapper (`card_fsti_014`)

FSTI · Common · Unit · cost 5 | atk 3 | hp 4 | shield 0 | keywords: Deploy, Draw

**Ability:** Deploy: Draw 2 cards.

**Design intent:** Big draw burst on a decent body. Late-game refuel.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead.
- **Pose:** steady working stance, alert.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** panoramic spectrum display; light band chart.
- **Rarity cue:** plain / workwear / utilitarian

#### Telemetry Receiver (`card_fsti_015`)

FSTI · Common · Unit · cost 4 | atk 2 | hp 5 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** Beefy common. Holds bench position well.

**Art direction:**

- **Subject:** Mecha — structure-centred machine subject, no humans
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** large dish antenna; feed horn; receiver station.
- **Rarity cue:** plain / workwear / utilitarian

#### Quick Analysis (`card_fsti_016`)

FSTI · Common · Ability · cost 1 | keywords: Draw

**Ability:** Draw 1 card.

**Design intent:** Cheapest cantrip. Cycles the deck.

**Art direction:**

- **Subject:** Effect — a stylized analytic readout panel snapping into place over a scanned object
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a stylized analytic readout panel snapping into place over a scanned object in action.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** infographic readout; scanned object; blue accent lines.
- **Rarity cue:** plain / workwear / utilitarian

#### Signal Boost (`card_fsti_017`)

FSTI · Common · Ability · cost 1 | keywords: -

**Ability:** Give an allied active unit +2 ATK this turn.

**Design intent:** Combat trick. Cheap damage buff.

**Art direction:**

- **Subject:** Effect — concentric signal rings amplifying outward from a single antenna mast
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — concentric signal rings amplifying outward from a single antenna mast in action.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** antenna mast; concentric signal rings.
- **Rarity cue:** plain / workwear / utilitarian

#### System Ping (`card_fsti_018`)

FSTI · Common · Ability · cost 0 | keywords: -

**Ability:** Gain 1 NP.

**Design intent:** Free NP. Enables turn-1 acceleration.

**Art direction:**

- **Subject:** Effect — one sonar pulse crossing a dark network map, lighting a single node
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — one sonar pulse crossing a dark network map, lighting a single node in action.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** network map; sonar pulse ring; lit node.
- **Rarity cue:** plain / workwear / utilitarian

#### Firmware Update (`card_fsti_019`)

FSTI · Common · Ability · cost 2 | keywords: -

**Ability:** Give an allied active unit +1 ATK permanently.

**Design intent:** Permanent buff. Stacks over turns.

**Art direction:**

- **Subject:** Effect — a microcontroller chip glowing as new firmware writes into it
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a microcontroller chip glowing as new firmware writes into it in action.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** microcontroller chip; progress bar glyph.
- **Rarity cue:** plain / workwear / utilitarian

#### Rapid Scan (`card_fsti_020`)

FSTI · Common · Ability · cost 1 | keywords: Fast, Draw

**Ability:** Fast: Draw 1 card.

**Design intent:** Fast-speed cantrip. Playable on opponent's turn.

**Art direction:**

- **Subject:** Effect — a fast scan line racing across a target with cyan speed streaks
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a fast scan line racing across a target with cyan speed streaks in action.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** scan line; cyan speed streaks; marked target.
- **Rarity cue:** plain / workwear / utilitarian

#### Diagnostic Sweep (`card_fsti_021`)

FSTI · Common · Ability · cost 2 | keywords: Draw

**Ability:** Draw 2 cards.

**Design intent:** Core draw spell. Refuels hand.

**Art direction:**

- **Subject:** Effect — a radial diagnostic sweep checking a grid of subsystems, one node flagged amber
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a radial diagnostic sweep checking a grid of subsystems, one node flagged amber in action.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** subsystem grid; radial sweep arc; amber flagged node.
- **Rarity cue:** plain / workwear / utilitarian

#### Signal Repeater (`card_fsti_022`)

FSTI · Common · Relay · cost 2 | keywords: Activate, Draw

**Ability:** Activate: Draw 1 card.

**Design intent:** Common relay that generates cards each turn.

**Art direction:**

- **Subject:** Mecha — a relay pylon re-broadcasting a beam onward
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** relay pylon; re-broadcast beam; signal arcs.
- **Rarity cue:** plain / workwear / utilitarian

#### Micro Transmitter (`card_fsti_023`)

FSTI · Common · Relay · cost 1 | keywords: Activate

**Ability:** Activate: Gain 1 NP.

**Design intent:** Cheap NP relay. Slow but persistent value.

**Art direction:**

- **Subject:** Mecha — a tiny circuit-board transmitter with a hairline antenna and a glowing core
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** circuit-board transmitter; hairline antenna; glowing core.
- **Rarity cue:** plain / workwear / utilitarian

#### Batch Download (`card_fsti_024`)

FSTI · Common · Protocol · cost 3 | keywords: Persistent, Draw

**Ability:** Persistent: Draw 1 card at the start of each turn.

**Design intent:** Ongoing draw engine. High value if it stays up.

**Art direction:**

- **Subject:** Effect — stacked data blocks streaming down into a storage buffer as progress ticks fill
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — stacked data blocks streaming down into a storage buffer as progress ticks fill in action.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** streaming data blocks; storage buffer; progress ticks.
- **Rarity cue:** plain / workwear / utilitarian

#### Network Handshake (`card_fsti_025`)

FSTI · Common · Protocol · cost 2 | keywords: Persistent

**Ability:** Persistent: Gain 1 NP at the start of each turn.

**Design intent:** Ongoing NP generation. Ramps into big plays.

**Art direction:**

- **Subject:** Effect — two network nodes exchanging a linked glow at the moment connection establishes
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — two network nodes exchanging a linked glow at the moment connection establishes in action.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, flat utilitarian composition.
- **Prompt props (≤3 cap):** two linked nodes; connection glow.
- **Rarity cue:** plain / workwear / utilitarian

### Rare

#### Photonics Engineer (`card_fsti_026`)

FSTI · Rare · Unit · cost 3 | atk 2 | hp 3 | shield 0 | keywords: Deploy, Draw

**Ability:** Deploy: Draw 1 card. Gain 1 NP.

**Design intent:** Card draw plus NP on deploy. Key engine piece.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead.
- **Pose:** steady working stance, alert.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, clean professional composition.
- **Prompt props (≤3 cap):** laser bench; aligned lens array; bright light filaments.
- **Rarity cue:** refined / specialist / reinforced

#### Algorithm Tuner (`card_fsti_027`)

FSTI · Rare · Unit · cost 4 | atk 3 | hp 3 | shield 0 | keywords: Deploy

**Ability:** Deploy: Give all allied active units +1 ATK this turn.

**Design intent:** Board-wide ATK buff on deploy. Rewards wide boards.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead.
- **Pose:** steady working stance, alert.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, clean professional composition.
- **Prompt props (≤3 cap):** floating decision-tree hologram; re-weighting branches.
- **Rarity cue:** refined / specialist / reinforced

#### Accelerator Tech (`card_fsti_028`)

FSTI · Rare · Unit · cost 5 | atk 4 | hp 3 | shield 0 | keywords: Piercing

**Ability:** Piercing.

**Design intent:** Mid-cost piercing threat. Punches through shields.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead.
- **Pose:** aggressive combat stance, weight forward, ready to strike.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, clean professional composition.
- **Prompt props (≤3 cap):** particle accelerator ring; charged beam; magnet housings.
- **Rarity cue:** refined / specialist / reinforced

#### Quantum Statistician (`card_fsti_029`)

FSTI · Rare · Unit · cost 4 | atk 2 | hp 4 | shield 0 | keywords: Activate, Draw

**Ability:** Activate: Draw 1 card. Gain 1 NP.

**Design intent:** Repeatable draw+NP engine. High priority removal target.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead.
- **Pose:** steady working stance, alert.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, clean professional composition.
- **Prompt props (≤3 cap):** probability glyph cloud; entangled-particle diagram.
- **Rarity cue:** refined / specialist / reinforced

#### Field Calibrator (`card_fsti_030`)

FSTI · Rare · Unit · cost 3 | atk 3 | hp 2 | shield 0 | keywords: Deploy

**Ability:** Deploy: Debuff an enemy active unit -2 ATK this turn.

**Design intent:** Offensive deploy. Weakens the opponent's attacker.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead.
- **Pose:** steady working stance, alert.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, clean professional composition.
- **Prompt props (≤3 cap):** calibration wand; distorted field grid; alignment markers.
- **Rarity cue:** refined / specialist / reinforced

#### Cryogenics Specialist (`card_fsti_031`)

FSTI · Rare · Unit · cost 5 | atk 3 | hp 4 | shield 0 | keywords: Deploy, Exhaust

**Ability:** Deploy: Exhaust an enemy active unit.

**Design intent:** Removal-adjacent. Exhausts a key enemy unit.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead.
- **Pose:** steady working stance, alert.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, clean professional composition.
- **Prompt props (≤3 cap):** cryo-unit; venting cold vapor; frost creep.
- **Rarity cue:** refined / specialist / reinforced

#### Simulation Operator (`card_fsti_032`)

FSTI · Rare · Unit · cost 6 | atk 4 | hp 5 | shield 0 | keywords: Deploy, Draw

**Ability:** Deploy: Draw 2 cards.

**Design intent:** Big body with deep draw. Late-game card advantage.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead, braced posture.
- **Pose:** aggressive combat stance, weight forward, ready to strike.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, clean professional composition.
- **Prompt props (≤3 cap):** full-room projection; blue wireframe terrain.
- **Rarity cue:** refined / specialist / reinforced

#### Interference Jammer (`card_fsti_033`)

FSTI · Rare · Unit · cost 3 | atk 2 | hp 3 | shield 0 | keywords: Deploy

**Ability:** Deploy: Prevent an enemy active unit from blocking this turn.

**Design intent:** Opens up attacks. Bypasses a blocker.

**Art direction:**

- **Subject:** Mecha — structure-centred machine subject, no humans
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, clean professional composition.
- **Prompt props (≤3 cap):** jammer device; scrambled noise field; flickering reticle.
- **Rarity cue:** refined / specialist / reinforced

#### Thermal Imager (`card_fsti_034`)

FSTI · Rare · Unit · cost 2 | atk 2 | hp 2 | shield 0 | keywords: FirstStrike

**Ability:** FirstStrike.

**Design intent:** Efficient first striker. Trades up against bigger units.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead.
- **Pose:** steady working stance, alert.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, clean professional composition.
- **Prompt props (≤3 cap):** thermal visor; false-color heat signatures; outlined hot target.
- **Rarity cue:** refined / specialist / reinforced

#### Overclocked Sequence (`card_fsti_035`)

FSTI · Rare · Ability · cost 3 | keywords: Draw

**Ability:** Draw 3 cards.

**Design intent:** Deep draw. Major hand refill.

**Art direction:**

- **Subject:** Effect — a rapid cascade of glowing process steps compressing into one bright output burst
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a rapid cascade of glowing process steps compressing into one bright output burst in action.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, clean professional composition.
- **Prompt props (≤3 cap):** cascading process steps; compressed output burst.
- **Rarity cue:** refined / specialist / reinforced

#### Phase Shift (`card_fsti_036`)

FSTI · Rare · Ability · cost 2 | keywords: Fast

**Ability:** Fast: Give an allied active unit +3 ATK this turn.

**Design intent:** Strong combat trick at fast speed.

**Art direction:**

- **Subject:** Effect — a single unit half-shifted out of phase, translucent edges and a doubled afterimage
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a single unit half-shifted out of phase, translucent edges and a doubled afterimage in action.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, clean professional composition.
- **Prompt props (≤3 cap):** translucent edges; doubled afterimage; cyan shimmer.
- **Rarity cue:** refined / specialist / reinforced

#### Exploit Vulnerability (`card_fsti_037`)

FSTI · Rare · Ability · cost 3 | keywords: -

**Ability:** Debuff all enemy active units -1 ATK this turn.

**Design intent:** Board-wide debuff. Punishes wide enemy boards.

**Art direction:**

- **Subject:** Effect — a red-highlighted crack in a defensive grid with a slim probe slipping through the gap
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a red-highlighted crack in a defensive grid with a slim probe slipping through the gap in action.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, clean professional composition.
- **Prompt props (≤3 cap):** defense grid; red crack highlight; slim probe.
- **Rarity cue:** refined / specialist / reinforced

#### Data Cache (`card_fsti_038`)

FSTI · Rare · Relay · cost 3 | keywords: Activate, Draw

**Ability:** Activate: Draw 1 card and gain 1 NP.

**Design intent:** Efficient relay. Draw and NP in one activation.

**Art direction:**

- **Subject:** Mecha — a retrieval arm pulling one bright data shard from a stack of glowing memory cores
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, clean professional composition.
- **Prompt props (≤3 cap):** stacked memory cores; retrieval arm; bright data shard.
- **Rarity cue:** refined / specialist / reinforced

#### Recursive Loop (`card_fsti_039`)

FSTI · Rare · Protocol · cost 4 | keywords: Persistent, Draw

**Ability:** Persistent: Draw 2 cards at the start of each turn.

**Design intent:** Premium card-draw engine. Game-warping if unanswered.

**Art direction:**

- **Subject:** Effect — a self-referential spiral of repeating glyphs feeding back into itself
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a self-referential spiral of repeating glyphs feeding back into itself in action.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, clean professional composition.
- **Prompt props (≤3 cap):** glyph spiral; feedback loop arcs.
- **Rarity cue:** refined / specialist / reinforced

#### Feedback Protocol (`card_fsti_040`)

FSTI · Rare · Protocol · cost 3 | keywords: Persistent

**Ability:** Persistent: Give all allied active units +1 ATK at the start of each turn.

**Design intent:** Ongoing board-wide buff. Snowballs if not removed.

**Art direction:**

- **Subject:** Effect — a live control-loop diagram whose output arcs back to reinforce its input
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a live control-loop diagram whose output arcs back to reinforce its input in action.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, clean professional composition.
- **Prompt props (≤3 cap):** control-loop diagram; reinforcing output arc.
- **Rarity cue:** refined / specialist / reinforced

### Epic

#### Entanglement Specialist (`card_fsti_041`)

FSTI · Epic · Unit · cost 6 | atk 4 | hp 5 | shield 0 | keywords: Deploy, Draw

**Ability:** Deploy: Draw 3 cards and gain 2 NP.

**Design intent:** Major refuel on deploy. Restocks hand and ramps.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead, braced posture.
- **Pose:** aggressive combat stance, weight forward, ready to strike.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, premium materials, commanding presence.
- **Prompt props (≤3 cap):** two linked particles; correlation lines; lab gap.
- **Rarity cue:** authoritative / premium / commanding

#### Mainframe Guardian (`card_fsti_042`)

FSTI · Epic · Unit · cost 7 | atk 5 | hp 6 | shield 0 | keywords: Piercing, Deploy

**Ability:** Piercing. Deploy: Debuff all enemy active units -2 ATK this turn.

**Design intent:** Top-end threat. Piercing plus board-wide debuff.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead, braced posture.
- **Pose:** aggressive combat stance, weight forward, ready to strike.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, premium materials, commanding presence.
- **Prompt props (≤3 cap):** towering mainframe; rolling dampening field; custodian armor.
- **Rarity cue:** authoritative / premium / commanding

#### Parallel Processor (`card_fsti_043`)

FSTI · Epic · Unit · cost 5 | atk 3 | hp 4 | shield 0 | keywords: Activate, Draw

**Ability:** Activate: Draw 2 cards.

**Design intent:** Repeatable deep draw. Must-answer engine.

**Art direction:**

- **Subject:** Mecha — structure-centred machine subject, no humans
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, premium materials, commanding presence.
- **Prompt props (≤3 cap):** multi-core cluster; parallel light lanes.
- **Rarity cue:** authoritative / premium / commanding

#### Senior Researcher (`card_fsti_044`)

FSTI · Epic · Unit · cost 5 | atk 3 | hp 5 | shield 0 | keywords: Deploy

**Ability:** Deploy: Give all allied active units +2 ATK permanently.

**Design intent:** Permanent board-wide buff. Game-changing deploy.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead, braced posture.
- **Pose:** braced, defensive, immovable presence.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, premium materials, commanding presence.
- **Prompt props (≤3 cap):** holo-table; broadcast upgrade ring.
- **Rarity cue:** authoritative / premium / commanding

#### Wavelength Disruptor (`card_fsti_045`)

FSTI · Epic · Unit · cost 6 | atk 4 | hp 4 | shield 0 | keywords: Deploy, Exhaust

**Ability:** Deploy: Exhaust all enemy active units.

**Design intent:** Mass exhaust. Clears the way for an alpha strike.

**Art direction:**

- **Subject:** Mecha — structure-centred machine subject, no humans
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, premium materials, commanding presence.
- **Prompt props (≤3 cap):** dissonant wave emitter; stalled machinery; wavefront distortion.
- **Rarity cue:** authoritative / premium / commanding

#### Learning Cycle (`card_fsti_046`)

FSTI · Epic · Ability · cost 4 | keywords: Draw

**Ability:** Draw 4 cards. Gain 2 NP.

**Design intent:** Massive hand refill plus NP. Turns the corner.

**Art direction:**

- **Subject:** Effect — a neural network diagram brightening as it trains, weights shifting and output sharpening
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a neural network diagram brightening as it trains, weights shifting and output sharpening in action.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, premium materials, commanding presence.
- **Prompt props (≤3 cap):** neural network diagram; shifting weight links; sharpened output node.
- **Rarity cue:** authoritative / premium / commanding

#### System Override (`card_fsti_047`)

FSTI · Epic · Ability · cost 5 | keywords: Exhaust

**Ability:** Exhaust all enemy active units and prevent them from blocking this turn.

**Design intent:** Premium removal spell. Full board lockdown for one turn.

**Art direction:**

- **Subject:** Effect — a red override glyph seizing control while locked-out units freeze mid-action
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a red override glyph seizing control while locked-out units freeze mid-action in action.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, premium materials, commanding presence.
- **Prompt props (≤3 cap):** red override glyph; frozen units; lockout rings.
- **Rarity cue:** authoritative / premium / commanding

#### Infinite Regression (`card_fsti_048`)

FSTI · Epic · Protocol · cost 5 | keywords: Persistent, Draw

**Ability:** Persistent: Draw 2 cards and gain 2 NP at the start of each turn.

**Design intent:** Ultimate ongoing engine. Must be removed immediately.

**Art direction:**

- **Subject:** Effect — a fractal corridor of ever-repeating data, each level spawning the next
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a fractal corridor of ever-repeating data, each level spawning the next in action.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, premium materials, commanding presence.
- **Prompt props (≤3 cap):** fractal data corridor; nested repeating layers.
- **Rarity cue:** authoritative / premium / commanding

### Legendary

#### Dr. Lumen (`card_fsti_049`)

FSTI · Legendary · Unit · cost 8 | atk 5 | hp 7 | shield 0 | keywords: Deploy, Activate, Draw

**Ability:** Deploy: Draw 3 cards and gain 3 NP. Activate: Draw 1 card.

**Design intent:** FSTI legendary. Massive deploy value plus repeatable draw.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** white coat over workwear, safety goggles pushed up on the forehead, braced posture.
- **Pose:** aggressive combat stance, weight forward, ready to strike.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, dramatic rim light, gilded edges, monumental scale.
- **Prompt props (≤3 cap):** orbiting data halo; high-collared coat; instrumented observatory hall.
- **Rarity cue:** gilded / iconic / monumental

#### Nexus Core (`card_fsti_050`)

FSTI · Legendary · Protocol · cost 9 | keywords: Persistent, Draw

**Ability:** Persistent: Draw 3 cards, gain 3 NP, and give all allied active units +1 ATK at the start of each turn.

**Design intent:** FSTI ultimate protocol. Overwhelming recurring value.

**Art direction:**

- **Subject:** Mecha — a vast crystalline processing core radiating light as the hub of every FSTI network
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** cool overhead lab light, holographic blue rim, glass-and-chrome background, dramatic rim light, gilded edges, monumental scale.
- **Prompt props (≤3 cap):** crystalline processing core; radiating network conduits; command ring.
- **Rarity cue:** gilded / iconic / monumental

## FRTI — Frontier Research & Technology Institute

*(heavy industry, forge and steel, machinery, combustion — hot oranges, iron greys, sparks, rivets)*

### Common

#### Assembly Worker (`card_frti_001`)

FRTI · Common · Unit · cost 1 | atk 2 | hp 1 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** Aggressive 1-drop. High ATK, low survivability.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy work apron, heat-resistant gloves, soot-streaked face.
- **Pose:** steady working stance, alert.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** conveyor line; chassis parts; work gloves and apron.
- **Rarity cue:** plain / workwear / utilitarian

#### Rivet Gunner (`card_frti_002`)

FRTI · Common · Unit · cost 2 | atk 3 | hp 1 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** Glass cannon 2-drop. Hits hard, dies fast.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy work apron, heat-resistant gloves, soot-streaked face.
- **Pose:** steady working stance, alert.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** pneumatic rivet gun; steel plate; burst of air.
- **Rarity cue:** plain / workwear / utilitarian

#### Frame Hauler (`card_frti_003`)

FRTI · Common · Unit · cost 2 | atk 2 | hp 3 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** Balanced 2-drop. Solid stats for cost.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy work apron, heat-resistant gloves, soot-streaked face.
- **Pose:** steady working stance, alert.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** heavy metal frame; straining hydraulics; factory floor.
- **Rarity cue:** plain / workwear / utilitarian

#### Welding Apprentice (`card_frti_004`)

FRTI · Common · Unit · cost 1 | atk 1 | hp 2 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** Cheap blocker. Defensive 1-drop for FRTI.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy work apron, heat-resistant gloves, soot-streaked face.
- **Pose:** steady working stance, alert.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** welding mask; bright fusion arc; shower of sparks.
- **Rarity cue:** plain / workwear / utilitarian

#### Piston Driver (`card_frti_005`)

FRTI · Common · Unit · cost 3 | atk 4 | hp 2 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** Aggressive 3-drop. Premium ATK for cost.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy work apron, heat-resistant gloves, soot-streaked face.
- **Pose:** aggressive combat stance, weight forward, ready to strike.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** piston press; stamping mechanical arms.
- **Rarity cue:** plain / workwear / utilitarian

#### Conveyor Operator (`card_frti_006`)

FRTI · Common · Unit · cost 3 | atk 3 | hp 3 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** Vanilla 3-drop. Even stats distribution.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy work apron, heat-resistant gloves, soot-streaked face.
- **Pose:** steady working stance, alert.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** moving belt of components; flow control panel.
- **Rarity cue:** plain / workwear / utilitarian

#### Crane Rigger (`card_frti_007`)

FRTI · Common · Unit · cost 4 | atk 4 | hp 3 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** ATK-heavy 4-drop. Classic FRTI profile.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy work apron, heat-resistant gloves, soot-streaked face.
- **Pose:** aggressive combat stance, weight forward, ready to strike.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** gantry crane cab; hooked massive load; swing cables.
- **Rarity cue:** plain / workwear / utilitarian

#### Foundry Stoker (`card_frti_008`)

FRTI · Common · Unit · cost 4 | atk 3 | hp 4 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** Durable 4-drop. Balanced between offence and defence.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy work apron, heat-resistant gloves, soot-streaked face.
- **Pose:** steady working stance, alert.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** roaring furnace mouth; stoking shovel; soot-streaked face.
- **Rarity cue:** plain / workwear / utilitarian

#### Impact Hammer (`card_frti_009`)

FRTI · Common · Unit · cost 3 | atk 3 | hp 2 | shield 0 | keywords: FirstStrike

**Ability:** FirstStrike.

**Design intent:** First striker. Trades up in combat reliably.

**Art direction:**

- **Subject:** Mecha — structure-centred machine subject, no humans
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** huge drop-hammer; mid-swing momentum arc; anvil block.
- **Rarity cue:** plain / workwear / utilitarian

#### Drill Sergeant (`card_frti_010`)

FRTI · Common · Unit · cost 2 | atk 2 | hp 2 | shield 0 | keywords: Deploy

**Ability:** Deploy: Give an allied active unit +1 ATK this turn.

**Design intent:** Cheap buff on deploy. Supports aggressive openers.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy work apron, heat-resistant gloves, soot-streaked face.
- **Pose:** steady working stance, alert.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** tool belt; barked order ripple; nearby unit sharpening.
- **Rarity cue:** plain / workwear / utilitarian

#### Load Bearer (`card_frti_011`)

FRTI · Common · Unit · cost 5 | atk 4 | hp 4 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** Premium vanilla 5-drop. Strong stat line.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy work apron, heat-resistant gloves, soot-streaked face.
- **Pose:** aggressive combat stance, weight forward, ready to strike.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** heavy crate; shoulder rig; steady stance.
- **Rarity cue:** plain / workwear / utilitarian

#### Turbine Mechanic (`card_frti_012`)

FRTI · Common · Unit · cost 4 | atk 5 | hp 2 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** High ATK glass cannon at 4 NP. Aggressive curve topper.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy work apron, heat-resistant gloves, soot-streaked face.
- **Pose:** aggressive combat stance, weight forward, ready to strike.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** spinning turbine; blurring blades; service wrench.
- **Rarity cue:** plain / workwear / utilitarian

#### Scrap Collector (`card_frti_013`)

FRTI · Common · Unit · cost 1 | atk 1 | hp 1 | shield 0 | keywords: Deploy

**Ability:** Deploy: Gain 1 NP.

**Design intent:** Cheap NP accelerator. Fuels bigger plays.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy work apron, heat-resistant gloves, soot-streaked face.
- **Pose:** steady working stance, alert.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** magnetic grapnel; gathered scrap metal.
- **Rarity cue:** plain / workwear / utilitarian

#### Boiler Guard (`card_frti_014`)

FRTI · Common · Unit · cost 5 | atk 5 | hp 3 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** High-ATK 5-drop. Threatens lethal damage.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy work apron, heat-resistant gloves, soot-streaked face.
- **Pose:** aggressive combat stance, weight forward, ready to strike.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** pressure boiler; climbing gauge; thick plating.
- **Rarity cue:** plain / workwear / utilitarian

#### Junction Welder (`card_frti_015`)

FRTI · Common · Unit · cost 0 | atk 1 | hp 1 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** Free body. Board presence at no cost.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy work apron, heat-resistant gloves, soot-streaked face.
- **Pose:** steady working stance, alert.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** pipe junction; small quick weld arc.
- **Rarity cue:** plain / workwear / utilitarian

#### Overclock Engine (`card_frti_016`)

FRTI · Common · Ability · cost 1 | keywords: -

**Ability:** Give an allied active unit +2 ATK this turn.

**Design intent:** Basic combat buff. Cheap and effective.

**Art direction:**

- **Subject:** Mecha — an engine pushed past its redline, glowing hot as power surges into a connected unit
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** overclocked engine block; red-hot glow; surging power cable.
- **Rarity cue:** plain / workwear / utilitarian

#### Reinforced Plating (`card_frti_017`)

FRTI · Common · Ability · cost 1 | keywords: -

**Ability:** Grant an allied active unit 2 Shield.

**Design intent:** Defensive ability. Adds temporary shield.

**Art direction:**

- **Subject:** Effect — steel plates being bolted over a frame as a shield layer clamps into place
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — steel plates being bolted over a frame as a shield layer clamps into place in action.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** bolted steel plates; clamping shield layer.
- **Rarity cue:** plain / workwear / utilitarian

#### Heavy Swing (`card_frti_018`)

FRTI · Common · Ability · cost 2 | keywords: -

**Ability:** Deal 3 damage to an enemy active unit.

**Design intent:** Direct damage removal. Clears small-to-mid threats.

**Art direction:**

- **Subject:** Effect — a massive wrench-beam mid-arc with kinetic impact lines aimed at a target
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a massive wrench-beam mid-arc with kinetic impact lines aimed at a target in action.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** massive wrench-beam; kinetic impact lines.
- **Rarity cue:** plain / workwear / utilitarian

#### Industrial Surge (`card_frti_019`)

FRTI · Common · Ability · cost 2 | keywords: -

**Ability:** Give an allied active unit +3 ATK this turn.

**Design intent:** Strong single-target buff. Big damage spike.

**Art direction:**

- **Subject:** Effect — a burst of factory power channeled into one unit with energy arcs around it
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a burst of factory power channeled into one unit with energy arcs around it in action.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** channeled power burst; energy arcs; factory conduits.
- **Rarity cue:** plain / workwear / utilitarian

#### Quick Weld (`card_frti_020`)

FRTI · Common · Ability · cost 1 | keywords: Fast

**Ability:** Fast: Give an allied active unit +2 ATK this turn.

**Design intent:** Fast-speed combat trick. Surprises in combat.

**Art direction:**

- **Subject:** Effect — a fast bright spot-weld flaring instantly, speed emphasised over buildup
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a fast bright spot-weld flaring instantly, speed emphasised over buildup in action.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** spot-weld flare; speed streaks.
- **Rarity cue:** plain / workwear / utilitarian

#### Controlled Demolition (`card_frti_021`)

FRTI · Common · Ability · cost 3 | keywords: -

**Ability:** Deal 2 damage to all enemy active units.

**Design intent:** AoE without keyword. Clears small boards.

**Art direction:**

- **Subject:** Effect — precisely rigged charges collapsing a structure, shockwave rolling outward
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — precisely rigged charges collapsing a structure, shockwave rolling outward in action.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** rigged charges; collapsing structure; rolling shockwave.
- **Rarity cue:** plain / workwear / utilitarian

#### Supply Depot (`card_frti_022`)

FRTI · Common · Relay · cost 2 | keywords: Activate

**Ability:** Activate: Give an allied active unit +1 ATK permanently.

**Design intent:** Structure that steadily buffs. Slow but persistent value.

**Art direction:**

- **Subject:** Mecha — a warehouse of crates and fuel drums forming a permanent reinforcement line
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** stacked crates; fuel drums; feed conveyor.
- **Rarity cue:** plain / workwear / utilitarian

#### Ore Processor (`card_frti_023`)

FRTI · Common · Relay · cost 2 | keywords: Activate

**Ability:** Activate: Gain 2 NP.

**Design intent:** NP relay. Industrial resource generation.

**Art direction:**

- **Subject:** Mecha — raw ore crushed and refined into usable energy on a conveyor and grinder line
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** ore conveyor; grinder wheel; refined energy output.
- **Rarity cue:** plain / workwear / utilitarian

#### Production Line (`card_frti_024`)

FRTI · Common · Protocol · cost 3 | keywords: Persistent

**Ability:** Persistent: Give all allied active units +1 ATK at the start of each turn.

**Design intent:** Ongoing ATK buff. Rewards keeping units alive.

**Art direction:**

- **Subject:** Effect — an assembly line in motion, each station adding strength to what rolls off the end
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — an assembly line in motion, each station adding strength to what rolls off the end in action.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** assembly stations; rolling output; strengthening sparks.
- **Rarity cue:** plain / workwear / utilitarian

#### Shift Change (`card_frti_025`)

FRTI · Common · Protocol · cost 2 | keywords: Persistent

**Ability:** Persistent: Gain 1 NP at the start of each turn.

**Design intent:** Ongoing NP generation. Fuels FRTI's expensive units.

**Art direction:**

- **Subject:** Effect — a factory gate cycling through shift change as the output line never pauses
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a factory gate cycling through shift change as the output line never pauses in action.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, flat utilitarian composition.
- **Prompt props (≤3 cap):** factory gate; time clock; shift bell.
- **Rarity cue:** plain / workwear / utilitarian

### Rare

#### Forge Master (`card_frti_026`)

FRTI · Rare · Unit · cost 4 | atk 4 | hp 4 | shield 0 | keywords: Deploy

**Ability:** Deploy: Give an allied active unit +2 ATK permanently.

**Design intent:** Strong body with permanent buff on deploy.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy work apron, heat-resistant gloves, soot-streaked face.
- **Pose:** aggressive combat stance, weight forward, ready to strike.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, clean professional composition.
- **Prompt props (≤3 cap):** anvil; raised hammer; tempered edge glow.
- **Rarity cue:** refined / specialist / reinforced

#### Press Operator (`card_frti_027`)

FRTI · Rare · Unit · cost 5 | atk 5 | hp 4 | shield 0 | keywords: FirstStrike

**Ability:** FirstStrike.

**Design intent:** Big first striker. Dominates combat step.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy work apron, heat-resistant gloves, soot-streaked face.
- **Pose:** aggressive combat stance, weight forward, ready to strike.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, clean professional composition.
- **Prompt props (≤3 cap):** giant hydraulic press; command console; descending force.
- **Rarity cue:** refined / specialist / reinforced

#### Furnace Tender (`card_frti_028`)

FRTI · Rare · Unit · cost 6 | atk 6 | hp 4 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** High ATK 6-drop. Pure damage threat.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy reinforced work apron, heat-resistant gloves, soot-streaked face.
- **Pose:** aggressive combat stance, weight forward, ready to strike.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, clean professional composition.
- **Prompt props (≤3 cap):** blast furnace; regulating valves; heat shimmer.
- **Rarity cue:** refined / specialist / reinforced

#### Demolition Expert (`card_frti_029`)

FRTI · Rare · Unit · cost 4 | atk 3 | hp 3 | shield 0 | keywords: Deploy, AoE

**Ability:** Deploy: Deal 2 damage to all enemy active units.

**Design intent:** AoE on deploy. Board clear attached to a body.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy work apron, heat-resistant gloves, soot-streaked face.
- **Pose:** steady working stance, alert.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, clean professional composition.
- **Prompt props (≤3 cap):** broad charge set; rippling wide blast; detonator.
- **Rarity cue:** refined / specialist / reinforced

#### Alloy Smelter (`card_frti_030`)

FRTI · Rare · Unit · cost 5 | atk 4 | hp 5 | shield 0 | keywords: Deploy

**Ability:** Deploy: Grant an allied active unit 3 Shield.

**Design intent:** Big body that also shields an ally. Defensive utility.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy work apron, heat-resistant gloves, soot-streaked face, braced posture.
- **Pose:** aggressive combat stance, weight forward, ready to strike.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, clean professional composition.
- **Prompt props (≤3 cap):** molten alloy pour; mold; cooling protective shell.
- **Rarity cue:** refined / specialist / reinforced

#### Slag Breaker (`card_frti_031`)

FRTI · Rare · Unit · cost 3 | atk 4 | hp 2 | shield 0 | keywords: Piercing

**Ability:** Piercing.

**Design intent:** Cheap piercing aggro. Ignores shield HP.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy work apron, heat-resistant gloves, soot-streaked face.
- **Pose:** aggressive combat stance, weight forward, ready to strike.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, clean professional composition.
- **Prompt props (≤3 cap):** slag block; piercing swing; armor-split sparks.
- **Rarity cue:** refined / specialist / reinforced

#### Girder Layer (`card_frti_032`)

FRTI · Rare · Unit · cost 3 | atk 2 | hp 4 | shield 0 | keywords: Deploy

**Ability:** Deploy: Deal 2 damage to an enemy active unit.

**Design intent:** Deploy removal on a solid body. Versatile.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy work apron, heat-resistant gloves, soot-streaked face.
- **Pose:** steady working stance, alert.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, clean professional composition.
- **Prompt props (≤3 cap):** steel girder; rigging lines; striking beam end.
- **Rarity cue:** refined / specialist / reinforced

#### Engine Guardian (`card_frti_033`)

FRTI · Rare · Unit · cost 6 | atk 5 | hp 5 | shield 0 | keywords: Deploy

**Ability:** Deploy: Deal 3 damage to an enemy active unit.

**Design intent:** Premium removal on a big body. Stabilizes the board.

**Art direction:**

- **Subject:** Mecha — structure-centred machine subject, no humans
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, clean professional composition.
- **Prompt props (≤3 cap):** engine block body; striking arm; guardian stance.
- **Rarity cue:** refined / specialist / reinforced

#### Railgun Prototype (`card_frti_034`)

FRTI · Rare · Unit · cost 4 | atk 5 | hp 2 | shield 0 | keywords: CannotBlock

**Ability:** CannotBlock. High ATK but cannot defend.

**Design intent:** Pure aggro. Cannot block, so all-in offense.

**Art direction:**

- **Subject:** Mecha — structure-centred machine subject, no humans
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, clean professional composition.
- **Prompt props (≤3 cap):** electromagnetic cannon rig; crackling charge coils.
- **Rarity cue:** refined / specialist / reinforced

#### Precision Strike (`card_frti_035`)

FRTI · Rare · Ability · cost 3 | keywords: -

**Ability:** Deal 5 damage to an enemy active unit.

**Design intent:** Heavy single-target removal. Kills most mid-cost units.

**Art direction:**

- **Subject:** Effect — a single calibrated shot tracked to its target, crosshair and impact marker aligned
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a single calibrated shot tracked to its target, crosshair and impact marker aligned in action.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, clean professional composition.
- **Prompt props (≤3 cap):** crosshair reticle; impact marker; calibrated shot trail.
- **Rarity cue:** refined / specialist / reinforced

#### Tempered Steel (`card_frti_036`)

FRTI · Rare · Ability · cost 2 | keywords: Fast

**Ability:** Fast: Give an allied active unit +3 ATK this turn and grant it 2 Shield.

**Design intent:** Fast combat trick with shield. Offensive and defensive.

**Art direction:**

- **Subject:** Effect — a blade quenched and hardened with a glowing edge in one fast pass
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a blade quenched and hardened with a glowing edge in one fast pass in action.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, clean professional composition.
- **Prompt props (≤3 cap):** quenched blade; glowing edge; quench steam.
- **Rarity cue:** refined / specialist / reinforced

#### Shrapnel Burst (`card_frti_037`)

FRTI · Rare · Ability · cost 4 | keywords: AoE

**Ability:** Deal 3 damage to all enemy active units.

**Design intent:** Strong AoE clear. Devastates wide boards.

**Art direction:**

- **Subject:** Effect — an explosion scattering fragments in all directions across every position
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — an explosion scattering fragments in all directions across every position in action.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, clean professional composition.
- **Prompt props (≤3 cap):** fragment burst; radial shrapnel; blast core.
- **Rarity cue:** refined / specialist / reinforced

#### Munitions Factory (`card_frti_038`)

FRTI · Rare · Relay · cost 4 | keywords: Activate

**Ability:** Activate: Give all allied active units +2 ATK this turn.

**Design intent:** High-value relay. Board-wide ATK boost per turn.

**Art direction:**

- **Subject:** Mecha — a plant churning out ammunition, belts of shells feeding a firepower boost
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, clean professional composition.
- **Prompt props (≤3 cap):** shell belts; press machines; factory stacks.
- **Rarity cue:** refined / specialist / reinforced

#### Arms Race (`card_frti_039`)

FRTI · Rare · Protocol · cost 4 | keywords: Persistent

**Ability:** Persistent: Give all allied active units +1 ATK permanently at the start of each turn.

**Design intent:** Snowball protocol. Permanent stacking ATK buffs.

**Art direction:**

- **Subject:** Effect — two production lines escalating, output ratcheting upward each cycle
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — two production lines escalating, output ratcheting upward each cycle in action.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, clean professional composition.
- **Prompt props (≤3 cap):** twin production lines; ratcheting output gauge.
- **Rarity cue:** refined / specialist / reinforced

#### Thermal Runaway (`card_frti_040`)

FRTI · Rare · Protocol · cost 4 | keywords: Persistent, AoE

**Ability:** Persistent: Deal 1 damage to all enemy active units at the start of each turn.

**Design intent:** Ongoing AoE ping. Grinds down enemy boards.

**Art direction:**

- **Subject:** Effect — a reactor overheating, heat bleeding outward each turn
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a reactor overheating, heat bleeding outward each turn in action.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, clean professional composition.
- **Prompt props (≤3 cap):** overheating reactor; bleeding heat waves; warning lights.
- **Rarity cue:** refined / specialist / reinforced

### Epic

#### Siege Engine (`card_frti_041`)

FRTI · Epic · Unit · cost 7 | atk 7 | hp 5 | shield 0 | keywords: Piercing

**Ability:** Piercing.

**Design intent:** Top-end piercing threat. Ignores shields for massive damage.

**Art direction:**

- **Subject:** Mecha — structure-centred machine subject, no humans
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, premium materials, commanding presence.
- **Prompt props (≤3 cap):** colossal tracked chassis; armor-piercing main gun.
- **Rarity cue:** authoritative / premium / commanding

#### Lifter Mech (`card_frti_042`)

FRTI · Epic · Unit · cost 6 | atk 5 | hp 6 | shield 0 | keywords: Deploy, AoE

**Ability:** Deploy: Deal 3 damage to all enemy active units.

**Design intent:** AoE clear on a premium body. Board wipe + threat.

**Art direction:**

- **Subject:** Mecha — structure-centred machine subject, no humans
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, premium materials, commanding presence.
- **Prompt props (≤3 cap):** heavy lifter mech; slamming load-arms; area blast dust.
- **Rarity cue:** authoritative / premium / commanding

#### Chief Machinist (`card_frti_043`)

FRTI · Epic · Unit · cost 6 | atk 5 | hp 5 | shield 0 | keywords: Deploy

**Ability:** Deploy: Give all allied active units +2 ATK permanently.

**Design intent:** Permanent board-wide buff. Rally the whole team.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy reinforced work apron, heat-resistant gloves, soot-streaked face, braced posture.
- **Pose:** aggressive combat stance, weight forward, ready to strike.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, premium materials, commanding presence.
- **Prompt props (≤3 cap):** hard hat; exo-frame; crew upgrade banner.
- **Rarity cue:** authoritative / premium / commanding

#### Crushing Press (`card_frti_044`)

FRTI · Epic · Unit · cost 5 | atk 6 | hp 3 | shield 0 | keywords: FirstStrike, CannotBlock

**Ability:** FirstStrike. CannotBlock.

**Design intent:** All-in attacker. Massive first strike but no blocking.

**Art direction:**

- **Subject:** Mecha — structure-centred machine subject, no humans
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, premium materials, commanding presence.
- **Prompt props (≤3 cap):** towering press; downward devastation; shockwave ring.
- **Rarity cue:** authoritative / premium / commanding

#### Factory Reset (`card_frti_045`)

FRTI · Epic · Ability · cost 5 | keywords: AoE

**Ability:** Deal 4 damage to all enemy active units.

**Design intent:** Premium board clear. Kills most units outright.

**Art direction:**

- **Subject:** Effect — every machine rebooting at once in a cleansing surge
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — every machine rebooting at once in a cleansing surge in action.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, premium materials, commanding presence.
- **Prompt props (≤3 cap):** reboot glow wave; machine rows; cleansing surge front.
- **Rarity cue:** authoritative / premium / commanding

#### Meltdown Protocol (`card_frti_046`)

FRTI · Epic · Ability · cost 6 | keywords: -

**Ability:** Destroy an enemy active unit.

**Design intent:** Hard removal. Kills any unit regardless of HP.

**Art direction:**

- **Subject:** Effect — a reactor pushed to critical, warning lights blazing before the annihilating blast
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a reactor pushed to critical, warning lights blazing before the annihilating blast in action.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, premium materials, commanding presence.
- **Prompt props (≤3 cap):** critical reactor; warning light array; annihilation blast.
- **Rarity cue:** authoritative / premium / commanding

#### War Foundry (`card_frti_047`)

FRTI · Epic · Relay · cost 5 | keywords: Activate

**Ability:** Activate: Give all allied active units +2 ATK permanently.

**Design intent:** Premium relay. Stacking permanent buffs each turn.

**Art direction:**

- **Subject:** Mecha — a fortress-forged production hub arming every unit that passes through
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, premium materials, commanding presence.
- **Prompt props (≤3 cap):** fortress walls; arming conveyor; forge stacks.
- **Rarity cue:** authoritative / premium / commanding

### Legendary

#### Director Kaelin (`card_frti_048`)

FRTI · Legendary · Unit · cost 8 | atk 7 | hp 7 | shield 0 | keywords: Piercing, FirstStrike, Deploy, AoE

**Ability:** Piercing. FirstStrike. Deploy: Deal 3 damage to all enemy active units.

**Design intent:** FRTI legendary unit. Dominant combat stats plus board clear.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** heavy reinforced work apron, heat-resistant gloves, soot-streaked face, braced posture.
- **Pose:** aggressive combat stance, weight forward, ready to strike.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, dramatic rim light, gilded edges, monumental scale.
- **Prompt props (≤3 cap):** reinforced command exo-suit; piercing gaze; foundry throne hall.
- **Rarity cue:** gilded / iconic / monumental

#### Grand Forge (`card_frti_049`)

FRTI · Legendary · Relay · cost 8 | keywords: Activate, AoE

**Ability:** Activate: Give all allied active units +3 ATK permanently and deal 2 damage to all enemy active units.

**Design intent:** FRTI legendary relay. Ultimate factory. Buffs and damages each turn.

**Art direction:**

- **Subject:** Mecha — a legendary mega-forge, a cathedral of industry glowing from every bay
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, dramatic rim light, gilded edges, monumental scale.
- **Prompt props (≤3 cap):** cathedral mega-forge; molten bay glow; towering smokestacks.
- **Rarity cue:** gilded / iconic / monumental

#### Total Collapse (`card_frti_050`)

FRTI · Legendary · Ability · cost 10 | keywords: AoE

**Ability:** Deal 6 damage to all enemy active units.

**Design intent:** FRTI legendary ability. Near-total board wipe.

**Art direction:**

- **Subject:** Effect — a full structural failure unleashed - the entire factory coming down at maximum force
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a full structural failure unleashed - the entire factory coming down at maximum force in action.
- **Lighting / background:** forge glow, orange rim light on iron, sparks in the air, dramatic rim light, gilded edges, monumental scale.
- **Prompt props (≤3 cap):** collapsing factory; falling girders; dust shockwave.
- **Rarity cue:** gilded / iconic / monumental

## FPB — Frontier Planning Bureau

*(drafting studios, blueprints, conservation and restoration, galleries — warm paper tones, ink, gilt, protective coatings)*

### Common

#### Junior Drafter (`card_fpb_001`)

FPB · Common · Unit · cost 1 | atk 1 | hp 2 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** Cheap defensive body. Holds early positions.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets.
- **Pose:** steady working stance, alert.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** drafting table; T-square and pencils; first careful layout.
- **Rarity cue:** plain / workwear / utilitarian

#### Blueprint Archivist (`card_fpb_002`)

FPB · Common · Unit · cost 2 | atk 1 | hp 3 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** Durable 2-drop. Classic FPB defensive profile.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets.
- **Pose:** steady working stance, alert.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** rolled blueprints; flat file cabinets; catalog stamps.
- **Rarity cue:** plain / workwear / utilitarian

#### Sketch Apprentice (`card_fpb_003`)

FPB · Common · Unit · cost 1 | atk 1 | hp 1 | shield 1 | keywords: Shield

**Ability:** Shield 1.

**Design intent:** Cheap shielded unit. Soaks one extra hit.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets, shield-ready stance.
- **Pose:** steady working stance, alert.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** sketch pad; light protective wash; pencil case.
- **Rarity cue:** plain / workwear / utilitarian

#### Layout Planner (`card_fpb_004`)

FPB · Common · Unit · cost 2 | atk 1 | hp 3 | shield 1 | keywords: Shield

**Ability:** Shield 1.

**Design intent:** Defensive 2-drop with shield. Very sticky.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets, shield-ready stance.
- **Pose:** steady working stance, alert.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** drafting board; arranged layout blocks; faint shield grid.
- **Rarity cue:** plain / workwear / utilitarian

#### Site Surveyor (`card_fpb_005`)

FPB · Common · Unit · cost 2 | atk 2 | hp 2 | shield 0 | keywords: Deploy, Heal

**Ability:** Deploy: Heal an allied active unit for 2 HP.

**Design intent:** Body with healing. Sustains board presence.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets.
- **Pose:** steady working stance, alert.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** theodolite tripod; site readings; mending structure glow.
- **Rarity cue:** plain / workwear / utilitarian

#### Zoning Inspector (`card_fpb_006`)

FPB · Common · Unit · cost 3 | atk 2 | hp 4 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** High HP 3-drop. Hard to remove cheaply.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets.
- **Pose:** steady working stance, alert.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** clipboard and code book; stamp of approval; checked plan.
- **Rarity cue:** plain / workwear / utilitarian

#### Perspective Artist (`card_fpb_007`)

FPB · Common · Unit · cost 3 | atk 2 | hp 3 | shield 1 | keywords: Shield

**Ability:** Shield 1.

**Design intent:** Solid mid-cost shielded unit.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets, shield-ready stance.
- **Pose:** steady working stance, alert.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** stretched canvas; vanishing-point lines; fine varnish sheen.
- **Rarity cue:** plain / workwear / utilitarian

#### Foundation Layer (`card_fpb_008`)

FPB · Common · Unit · cost 4 | atk 2 | hp 5 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** High HP wall at 4 cost. Very durable.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets, braced posture.
- **Pose:** braced, defensive, immovable presence.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** solid base layer; trowel; sturdy stance.
- **Rarity cue:** plain / workwear / utilitarian

#### Model Maker (`card_fpb_009`)

FPB · Common · Unit · cost 3 | atk 2 | hp 3 | shield 0 | keywords: Deploy, Repair

**Ability:** Deploy: Repair self for 2 Shield.

**Design intent:** Self-repair on deploy. Gains shield when it enters.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets.
- **Pose:** steady working stance, alert.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** scale model pieces; repair adhesive; cracked piece restored.
- **Rarity cue:** plain / workwear / utilitarian

#### Color Theorist (`card_fpb_010`)

FPB · Common · Unit · cost 4 | atk 3 | hp 3 | shield 0 | keywords: Deploy

**Ability:** Deploy: Grant an allied active unit 2 Shield.

**Design intent:** Body plus shield grant. Defensive utility.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets.
- **Pose:** steady working stance, alert.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** mixed pigments; palette knife; protective color wash.
- **Rarity cue:** plain / workwear / utilitarian

#### Render Technician (`card_fpb_011`)

FPB · Common · Unit · cost 4 | atk 2 | hp 4 | shield 1 | keywords: Shield

**Ability:** Shield 1.

**Design intent:** Durable mid-range with shield. Hard to shift.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets, shield-ready stance.
- **Pose:** steady working stance, alert.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** render station; coated finish glaze.
- **Rarity cue:** plain / workwear / utilitarian

#### Structural Engineer (`card_fpb_012`)

FPB · Common · Unit · cost 5 | atk 3 | hp 5 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** Big defensive body. Late common that holds board.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets, braced posture.
- **Pose:** braced, defensive, immovable presence.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** load diagrams; engineering ruler; backbone beam sketch.
- **Rarity cue:** plain / workwear / utilitarian

#### Eraser (`card_fpb_013`)

FPB · Common · Unit · cost 0 | atk 0 | hp 2 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** Free 0/2 blocker. Absorbs an attack for free.

**Art direction:**

- **Subject:** Mecha — structure-centred machine subject, no humans
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** simple rubber eraser; lifting eraser crumbs; humble blocker stance.
- **Rarity cue:** plain / workwear / utilitarian

#### Detail Renderer (`card_fpb_014`)

FPB · Common · Unit · cost 5 | atk 2 | hp 6 | shield 0 | keywords: Activate, Heal

**Ability:** Activate: Heal an allied active unit for 2 HP.

**Design intent:** Repeatable healer. Sustains the board each turn.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets, braced posture.
- **Pose:** braced, defensive, immovable presence.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** fine detail brush; finishing touches; restored adjacent piece.
- **Rarity cue:** plain / workwear / utilitarian

#### Scale Modeller (`card_fpb_015`)

FPB · Common · Unit · cost 3 | atk 3 | hp 2 | shield 0 | keywords: -

**Ability:** — (stats body only, no activated ability)

**Design intent:** Offensive common for FPB. Trades well.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets.
- **Pose:** steady working stance, alert.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** precise scaled replica; measuring calipers.
- **Rarity cue:** plain / workwear / utilitarian

#### Quick Sketch (`card_fpb_016`)

FPB · Common · Ability · cost 1 | keywords: -

**Ability:** Grant an allied active unit 2 Shield.

**Design intent:** Cheap shield grant. Defensive trick.

**Art direction:**

- **Subject:** Effect — a rapid loose sketch throwing a quick protective shield over an ally
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a rapid loose sketch throwing a quick protective shield over an ally in action.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** rapid loose sketch; quick shield shimmer.
- **Rarity cue:** plain / workwear / utilitarian

#### Patch Work (`card_fpb_017`)

FPB · Common · Ability · cost 1 | keywords: Heal

**Ability:** Heal an allied active unit for 3 HP.

**Design intent:** Basic heal spell. Keeps units alive.

**Art direction:**

- **Subject:** Effect — a torn drawing mended with careful patches, health restored
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a torn drawing mended with careful patches, health restored in action.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** torn drawing; careful patches; mending glow.
- **Rarity cue:** plain / workwear / utilitarian

#### Revised Draft (`card_fpb_018`)

FPB · Common · Ability · cost 2 | keywords: Heal

**Ability:** Heal all allied active units for 2 HP.

**Design intent:** Board-wide heal. Keeps the whole team healthy.

**Art direction:**

- **Subject:** Effect — a clean second draft laid over the first, mending flaws across the team
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a clean second draft laid over the first, mending flaws across the team in action.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** layered drafts; correction marks; team mend glow.
- **Rarity cue:** plain / workwear / utilitarian

#### Protective Coating (`card_fpb_019`)

FPB · Common · Ability · cost 2 | keywords: Fast

**Ability:** Fast: Grant an allied active unit 3 Shield.

**Design intent:** Fast-speed shield grant. Defensive combat trick.

**Art direction:**

- **Subject:** Effect — a clear protective layer brushed quickly over a surface, sealing it fast
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a clear protective layer brushed quickly over a surface, sealing it fast in action.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** brushed coating; sealed surface sheen.
- **Rarity cue:** plain / workwear / utilitarian

#### Foundation Check (`card_fpb_020`)

FPB · Common · Ability · cost 0 | keywords: Heal

**Ability:** Heal an allied active unit for 1 HP.

**Design intent:** Free heal. Tiny but free sustain.

**Art direction:**

- **Subject:** Effect — a quick inspection tick patching a small fault
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a quick inspection tick patching a small fault in action.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** inspection tick mark; patched fault glow.
- **Rarity cue:** plain / workwear / utilitarian

#### Clear Coat (`card_fpb_021`)

FPB · Common · Ability · cost 2 | keywords: -

**Ability:** Grant all allied active units 1 Shield.

**Design intent:** Board-wide shield. Small but affects everything.

**Art direction:**

- **Subject:** Effect — a fine lacquer sprayed evenly, a light shield settling over all allied works
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a fine lacquer sprayed evenly, a light shield settling over all allied works in action.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** lacquer spray; settling shield film.
- **Rarity cue:** plain / workwear / utilitarian

#### Restoration Studio (`card_fpb_022`)

FPB · Common · Relay · cost 2 | keywords: Activate, Heal

**Ability:** Activate: Heal an allied active unit for 2 HP.

**Design intent:** Healing relay. Sustains board each turn.

**Art direction:**

- **Subject:** Mecha — a workbench of brushes and solvents continually restoring a piece on demand
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** restoration workbench; brushes and solvents; piece under care.
- **Rarity cue:** plain / workwear / utilitarian

#### Material Stockpile (`card_fpb_023`)

FPB · Common · Relay · cost 2 | keywords: Activate

**Ability:** Activate: Grant an allied active unit 1 Shield.

**Design intent:** Shield relay. Slow shield generation.

**Art direction:**

- **Subject:** Mecha — shelves of conservation materials ready to supply a protective layer when called
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** material shelves; conservation supplies; supply glow.
- **Rarity cue:** plain / workwear / utilitarian

#### Maintenance Schedule (`card_fpb_024`)

FPB · Common · Protocol · cost 3 | keywords: Persistent, Heal

**Ability:** Persistent: Heal all allied active units for 1 HP at the start of each turn.

**Design intent:** Ongoing board heal. Grinds out attrition.

**Art direction:**

- **Subject:** Effect — a pinned timetable of upkeep tasks steadily healing the team every turn
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a pinned timetable of upkeep tasks steadily healing the team every turn in action.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** pinned timetable; upkeep task ticks; steady heal glow.
- **Rarity cue:** plain / workwear / utilitarian

#### Iterative Design (`card_fpb_025`)

FPB · Common · Protocol · cost 3 | keywords: Persistent

**Ability:** Persistent: Grant all allied active units 1 Shield at the start of each turn.

**Design intent:** Ongoing shield protocol. Stacks shields over time.

**Art direction:**

- **Subject:** Effect — a sequence of evolving drafts stacked, each cycle adding a protective refinement
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a sequence of evolving drafts stacked, each cycle adding a protective refinement in action.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, flat utilitarian composition.
- **Prompt props (≤3 cap):** stacked evolving drafts; refinement cycle arcs.
- **Rarity cue:** plain / workwear / utilitarian

### Rare

#### Gallery Curator (`card_fpb_026`)

FPB · Rare · Unit · cost 3 | atk 2 | hp 4 | shield 1 | keywords: Shield, Deploy, Heal

**Ability:** Shield 1. Deploy: Heal an allied active unit for 2 HP.

**Design intent:** Shielded healer. Core defensive piece.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets, shield-ready stance.
- **Pose:** steady working stance, alert.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, clean professional composition.
- **Prompt props (≤3 cap):** gallery display cases; practiced curatorial eye; shielding gesture.
- **Rarity cue:** refined / specialist / reinforced

#### Lead Architect (`card_fpb_027`)

FPB · Rare · Unit · cost 5 | atk 3 | hp 5 | shield 2 | keywords: Shield

**Ability:** Shield 2.

**Design intent:** Premium shielded body. Very hard to take down.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets, braced posture, shield-ready stance.
- **Pose:** braced, defensive, immovable presence.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, clean professional composition.
- **Prompt props (≤3 cap):** grand drafting table; heavy protective shield over plan; architect roll.
- **Rarity cue:** refined / specialist / reinforced

#### Conservation Specialist (`card_fpb_028`)

FPB · Rare · Unit · cost 4 | atk 2 | hp 4 | shield 0 | keywords: Activate, Heal

**Ability:** Activate: Heal all allied active units for 2 HP.

**Design intent:** Repeatable board-wide heal. Key sustain engine.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets.
- **Pose:** steady working stance, alert.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, clean professional composition.
- **Prompt props (≤3 cap):** conservation bench; fine restoration tools; healed set of works.
- **Rarity cue:** refined / specialist / reinforced

#### City Planner (`card_fpb_029`)

FPB · Rare · Unit · cost 4 | atk 2 | hp 5 | shield 0 | keywords: Deploy

**Ability:** Deploy: Grant all allied active units 2 Shield.

**Design intent:** Board-wide shield on deploy. Defensive powerhouse.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets, braced posture.
- **Pose:** braced, defensive, immovable presence.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, clean professional composition.
- **Prompt props (≤3 cap):** large city map; zoning shield overlay; planner markers.
- **Rarity cue:** refined / specialist / reinforced

#### Composition Instructor (`card_fpb_030`)

FPB · Rare · Unit · cost 5 | atk 3 | hp 6 | shield 0 | keywords: Deploy, Repair

**Ability:** Deploy: Repair self for 3 Shield.

**Design intent:** Self-repair for max shield. Very tanky on arrival.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets, braced posture.
- **Pose:** braced, defensive, immovable presence.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, clean professional composition.
- **Prompt props (≤3 cap):** teaching easel; structure demonstration; mended standing pose.
- **Rarity cue:** refined / specialist / reinforced

#### Mural Painter (`card_fpb_031`)

FPB · Rare · Unit · cost 6 | atk 3 | hp 6 | shield 2 | keywords: Shield, Deploy, Heal

**Ability:** Shield 2. Deploy: Heal all allied active units for 3 HP.

**Design intent:** Big heal on a shielded body. Full team sustain.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, command roll-neck, drafter's apron pockets, braced posture, shield-ready stance.
- **Pose:** braced, defensive, immovable presence.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, clean professional composition.
- **Prompt props (≤3 cap):** scaffolding; vast mural wall; broad healing wash.
- **Rarity cue:** refined / specialist / reinforced

#### Framing Specialist (`card_fpb_032`)

FPB · Rare · Unit · cost 3 | atk 2 | hp 3 | shield 0 | keywords: Deploy

**Ability:** Deploy: Deal 2 damage to an enemy active unit.

**Design intent:** FPB removal option. Defensive deck needs answers too.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets.
- **Pose:** steady working stance, alert.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, clean professional composition.
- **Prompt props (≤3 cap):** rigid mounting frame; strike against fault; mounted work.
- **Rarity cue:** refined / specialist / reinforced

#### Master Calligrapher (`card_fpb_033`)

FPB · Rare · Unit · cost 5 | atk 2 | hp 5 | shield 0 | keywords: Activate, Repair

**Ability:** Activate: Repair self for 2 Shield.

**Design intent:** Self-repairing tank. Keeps regenerating shield.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets, braced posture.
- **Pose:** braced, defensive, immovable presence.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, clean professional composition.
- **Prompt props (≤3 cap):** calligraphy brush; ink stone; reinforcing precise strokes.
- **Rarity cue:** refined / specialist / reinforced

#### Design Reviewer (`card_fpb_034`)

FPB · Rare · Unit · cost 4 | atk 3 | hp 4 | shield 0 | keywords: Deploy, Draw

**Ability:** Deploy: Draw 1 card and grant an allied active unit 1 Shield.

**Design intent:** Card draw plus shield. Versatile FPB rare.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets.
- **Pose:** steady working stance, alert.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, clean professional composition.
- **Prompt props (≤3 cap):** marked-up design sheet; review annotations; found resource note.
- **Rarity cue:** refined / specialist / reinforced

#### Layered Varnish (`card_fpb_035`)

FPB · Rare · Ability · cost 3 | keywords: -

**Ability:** Grant all allied active units 3 Shield.

**Design intent:** Premium board-wide shield. Massive defensive swing.

**Art direction:**

- **Subject:** Effect — multiple varnish coats building into a thick shield glazing over all allied works
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — multiple varnish coats building into a thick shield glazing over all allied works in action.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, clean professional composition.
- **Prompt props (≤3 cap):** layered varnish coats; thick glaze shield.
- **Rarity cue:** refined / specialist / reinforced

#### Full Restoration (`card_fpb_036`)

FPB · Rare · Ability · cost 3 | keywords: Heal

**Ability:** Heal all allied active units for 4 HP.

**Design intent:** Deep board-wide heal. Major stabilizer.

**Art direction:**

- **Subject:** Effect — a piece returned to pristine condition, a strong heal across the whole collection
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a piece returned to pristine condition, a strong heal across the whole collection in action.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, clean professional composition.
- **Prompt props (≤3 cap):** pristine restored piece; collection heal glow.
- **Rarity cue:** refined / specialist / reinforced

#### Structural Critique (`card_fpb_037`)

FPB · Rare · Ability · cost 3 | keywords: -

**Ability:** Deal 4 damage to an enemy active unit.

**Design intent:** FPB removal spell. Precise damage.

**Art direction:**

- **Subject:** Effect — a red-lined critique dismantling an enemy argument with a heavy blow
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a red-lined critique dismantling an enemy argument with a heavy blow in action.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, clean professional composition.
- **Prompt props (≤3 cap):** red-lined critique; dismantled argument fragments.
- **Rarity cue:** refined / specialist / reinforced

#### Gilding Workshop (`card_fpb_038`)

FPB · Rare · Relay · cost 3 | keywords: Activate

**Ability:** Activate: Grant all allied active units 2 Shield.

**Design intent:** Shield relay. Board-wide shield per turn.

**Art direction:**

- **Subject:** Mecha — gold leaf applied by a gilding rig as a rich permanent shield settles over the team
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** structure-centred, object fills the frame, no humans.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, clean professional composition.
- **Prompt props (≤3 cap):** gold leaf sheets; gilding tools; settling gilt shield.
- **Rarity cue:** refined / specialist / reinforced

#### Preservation Order (`card_fpb_039`)

FPB · Rare · Protocol · cost 4 | keywords: Persistent, Heal

**Ability:** Persistent: Heal all allied active units for 2 HP at the start of each turn.

**Design intent:** Premium ongoing heal. Attrition engine.

**Art direction:**

- **Subject:** Effect — a formal directive sealed and posted, ongoing healing enacted every turn
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a formal directive sealed and posted, ongoing healing enacted every turn in action.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, clean professional composition.
- **Prompt props (≤3 cap):** sealed directive scroll; posted notice; enacted heal seal.
- **Rarity cue:** refined / specialist / reinforced

#### Hardening Process (`card_fpb_040`)

FPB · Rare · Protocol · cost 4 | keywords: Persistent

**Ability:** Persistent: Grant all allied active units 2 Shield at the start of each turn.

**Design intent:** Ongoing shield protocol. Stacking shield over time.

**Art direction:**

- **Subject:** Effect — a treatment hardening surfaces, a durable shield building turn after turn
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a treatment hardening surfaces, a durable shield building turn after turn in action.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, clean professional composition.
- **Prompt props (≤3 cap):** hardening treatment bath; building durable shield.
- **Rarity cue:** refined / specialist / reinforced

### Epic

#### Exhibition Guard (`card_fpb_041`)

FPB · Epic · Unit · cost 6 | atk 3 | hp 7 | shield 3 | keywords: Shield, Deploy, Heal

**Ability:** Shield 3. Deploy: Heal all allied active units for 3 HP.

**Design intent:** Premium defensive unit. Huge HP, shield, and team heal.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, command roll-neck, drafter's apron pockets, braced posture, shield-ready stance.
- **Pose:** braced, defensive, immovable presence.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, premium materials, commanding presence.
- **Prompt props (≤3 cap):** grand exhibition hall; watchful guard post; shielding and mending aura.
- **Rarity cue:** authoritative / premium / commanding

#### Conceptual Designer (`card_fpb_042`)

FPB · Epic · Unit · cost 5 | atk 3 | hp 5 | shield 0 | keywords: Deploy

**Ability:** Deploy: Grant all allied active units 3 Shield.

**Design intent:** Massive board-wide shield on deploy. Fortress builder.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, roll-neck sweater, drafter's apron pockets, braced posture.
- **Pose:** braced, defensive, immovable presence.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, premium materials, commanding presence.
- **Prompt props (≤3 cap):** bold concept sketches; wide protective shield projection.
- **Rarity cue:** authoritative / premium / commanding

#### Restoration Director (`card_fpb_043`)

FPB · Epic · Unit · cost 7 | atk 3 | hp 7 | shield 0 | keywords: Activate, Heal, Repair

**Ability:** Activate: Heal all allied active units for 3 HP. Repair self for 3 Shield.

**Design intent:** Ultimate sustain unit. Heals the board and repairs itself each turn.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, command roll-neck, drafter's apron pockets, braced posture.
- **Pose:** braced, defensive, immovable presence.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, premium materials, commanding presence.
- **Prompt props (≤3 cap):** full restoration direction; healed team glow; repaired lead piece.
- **Rarity cue:** authoritative / premium / commanding

#### Chief Inspector (`card_fpb_044`)

FPB · Epic · Unit · cost 6 | atk 4 | hp 6 | shield 0 | keywords: Deploy

**Ability:** Deploy: Destroy an enemy active unit.

**Design intent:** Hard removal on a body. FPB answers any threat.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, command roll-neck, drafter's apron pockets, braced posture.
- **Pose:** aggressive combat stance, weight forward, ready to strike.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, premium materials, commanding presence.
- **Prompt props (≤3 cap):** final authority stamp; condemned faulty work; inspector regalia.
- **Rarity cue:** authoritative / premium / commanding

#### Master Restoration (`card_fpb_045`)

FPB · Epic · Ability · cost 5 | keywords: Heal

**Ability:** Heal all allied active units for 5 HP. Grant all allied active units 2 Shield.

**Design intent:** Massive defensive swing. Full team heal and shield.

**Art direction:**

- **Subject:** Effect — a masterpiece fully restored under glass, great heal and shield in one sweeping pass
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a masterpiece fully restored under glass, great heal and shield in one sweeping pass in action.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, premium materials, commanding presence.
- **Prompt props (≤3 cap):** restored masterpiece; protective glass case; sweeping heal arc.
- **Rarity cue:** authoritative / premium / commanding

#### Decommission Order (`card_fpb_046`)

FPB · Epic · Ability · cost 5 | keywords: -

**Ability:** Destroy an enemy active unit. Grant all allied active units 1 Shield.

**Design intent:** Hard removal with defensive bonus. FPB finesse.

**Art direction:**

- **Subject:** Effect — an official order retiring a piece, striking down an enemy while the team is guarded
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — an official order retiring a piece, striking down an enemy while the team is guarded in action.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, premium materials, commanding presence.
- **Prompt props (≤3 cap):** official retirement order; guarded team seal; striking gavel.
- **Rarity cue:** authoritative / premium / commanding

#### Eternal Gallery (`card_fpb_047`)

FPB · Epic · Protocol · cost 5 | keywords: Persistent, Heal

**Ability:** Persistent: Heal all allied active units for 3 HP and grant them 2 Shield at the start of each turn.

**Design intent:** Ultimate sustain protocol. Near-invincible board if not removed.

**Art direction:**

- **Subject:** Effect — a timeless gallery hall whose works are endlessly preserved each turn
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a timeless gallery hall whose works are endlessly preserved each turn in action.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, premium materials, commanding presence.
- **Prompt props (≤3 cap):** timeless gallery columns; preserved works glow; enduring seal.
- **Rarity cue:** authoritative / premium / commanding

### Legendary

#### Director Vasari (`card_fpb_048`)

FPB · Legendary · Unit · cost 9 | atk 4 | hp 9 | shield 3 | keywords: Shield, Deploy, Activate, Heal, Repair

**Ability:** Shield 3. Deploy: Heal all allied active units for 4 HP. Activate: Repair self for 3 Shield.

**Design intent:** FPB legendary unit. Nearly unkillable fortress with team heal.

**Art direction:**

- **Subject:** Human — focus on the person, clear face, stance suited to the card's function.
- **Wardrobe:** ink-stained hands, command roll-neck, drafter's apron pockets, braced posture, shield-ready stance.
- **Pose:** aggressive combat stance, weight forward, ready to strike.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, dramatic rim light, gilded edges, monumental scale.
- **Prompt props (≤3 cap):** great archive hall; director regalia; commanding restoration gesture.
- **Rarity cue:** gilded / iconic / monumental

#### Living Masterpiece (`card_fpb_049`)

FPB · Legendary · Protocol · cost 9 | keywords: Persistent, Heal

**Ability:** Persistent: Heal all allied active units for 4 HP and grant them 3 Shield at the start of each turn.

**Design intent:** FPB legendary protocol. Overwhelming sustain per turn.

**Art direction:**

- **Subject:** Effect — a single work so perfect it seems alive, radiating constant healing and protection
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — a single work so perfect it seems alive, radiating constant healing and protection in action.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, dramatic rim light, gilded edges, monumental scale.
- **Prompt props (≤3 cap):** living masterpiece artwork; radiant heal aura; protective gilt frame.
- **Rarity cue:** gilded / iconic / monumental

#### Magnum Opus (`card_fpb_050`)

FPB · Legendary · Ability · cost 8 | keywords: Heal

**Ability:** Heal all allied active units for 6 HP. Grant all allied active units 4 Shield. Draw 2 cards.

**Design intent:** FPB legendary ability. Full defensive reset and card advantage.

**Art direction:**

- **Subject:** Effect — the crowning monumental composition that restores everything and draws acclaim
- **Wardrobe:** — no wardrobe (subject is a machine or effect; no humans)
- **Pose:** captured mid-effect — the crowning monumental composition that restores everything and draws acclaim in action.
- **Lighting / background:** warm parchment light, gilt accents, drafting table or gallery wall behind, dramatic rim light, gilded edges, monumental scale.
- **Prompt props (≤3 cap):** monumental composition; restoration sweep; acclaim laurels.
- **Rarity cue:** gilded / iconic / monumental
