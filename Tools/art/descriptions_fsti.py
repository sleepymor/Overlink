#!/usr/bin/env python3
"""FSTI per-card art descriptions (50 cards).

Feeds compose_prompts.py. Each entry carries at most three named background
props (the verified cap before SDXL paints an environment and drops the
subject), an optional subject-class override, and - for cards with no
character - the single focal object the effect must be anchored to.

Subject class defaults come from card type in compose_prompts.py:
    Unit -> human, Ability -> effect, Relay -> mecha, Protocol -> effect.
Override with sc= only where the card's own concept demands it.
"""

DESCRIPTIONS = {
    # ------------------------------------------------------------------ Common
    "card_fsti_001": dict(props=["humming lab bench", "data tablet", "safety goggles"]),
    "card_fsti_002": dict(props=["wall of signal relays", "operator headset", "glowing data lines"]),
    "card_fsti_003": dict(props=["dual-screen terminal", "reflected number streams"]),
    "card_fsti_004": dict(props=["handheld scanner", "swept waveform", "oscilloscope glow"]),
    "card_fsti_005": dict(props=["server rack", "pulsing fiber cables"]),
    "card_fsti_006": dict(props=["benchtop laser rig", "glass prism", "split beam"]),
    "card_fsti_007": dict(props=["curved monitor", "cascading code", "green build glyph"]),
    "card_fsti_008": dict(sc="mecha", props=["quad-rotor drone", "sensor pod", "projected scan grid"]),
    "card_fsti_009": dict(sc="mecha", props=["research bench", "robotic arm", "microscope array"]),
    "card_fsti_010": dict(props=["wall of throughput gauges", "climbing green graphs"]),
    "card_fsti_011": dict(props=["courier satchel", "signal device", "campus walkway"]),
    "card_fsti_012": dict(props=["3D holographic wave", "desk console"]),
    "card_fsti_013": dict(sc="mecha", props=["tripod instrument", "fine measuring beam", "calibration target"]),
    "card_fsti_014": dict(props=["panoramic spectrum display", "light band chart"]),
    "card_fsti_015": dict(sc="mecha", props=["large dish antenna", "feed horn", "receiver station"]),
    "card_fsti_016": dict(focal="a stylized analytic readout panel snapping into place over a scanned object",
                          props=["infographic readout", "scanned object", "blue accent lines"]),
    "card_fsti_017": dict(focal="concentric signal rings amplifying outward from a single antenna mast",
                          props=["antenna mast", "concentric signal rings"]),
    "card_fsti_018": dict(focal="one sonar pulse crossing a dark network map, lighting a single node",
                          props=["network map", "sonar pulse ring", "lit node"]),
    "card_fsti_019": dict(focal="a microcontroller chip glowing as new firmware writes into it",
                          props=["microcontroller chip", "progress bar glyph"]),
    "card_fsti_020": dict(focal="a fast scan line racing across a target with cyan speed streaks",
                          props=["scan line", "cyan speed streaks", "marked target"]),
    "card_fsti_021": dict(focal="a radial diagnostic sweep checking a grid of subsystems, one node flagged amber",
                          props=["subsystem grid", "radial sweep arc", "amber flagged node"]),
    "card_fsti_022": dict(sc="mecha", focal="a relay pylon re-broadcasting a beam onward",
                           props=["relay pylon", "re-broadcast beam", "signal arcs"]),
    "card_fsti_023": dict(sc="mecha", focal="a tiny circuit-board transmitter with a hairline antenna and a glowing core",
                           props=["circuit-board transmitter", "hairline antenna", "glowing core"]),
    "card_fsti_024": dict(focal="stacked data blocks streaming down into a storage buffer as progress ticks fill",
                           props=["streaming data blocks", "storage buffer", "progress ticks"]),
    "card_fsti_025": dict(focal="two network nodes exchanging a linked glow at the moment connection establishes",
                           props=["two linked nodes", "connection glow"]),
    # -------------------------------------------------------------------- Rare
    "card_fsti_026": dict(props=["laser bench", "aligned lens array", "bright light filaments"]),
    "card_fsti_027": dict(props=["floating decision-tree hologram", "re-weighting branches"]),
    "card_fsti_028": dict(props=["particle accelerator ring", "charged beam", "magnet housings"]),
    "card_fsti_029": dict(props=["probability glyph cloud", "entangled-particle diagram"]),
    "card_fsti_030": dict(props=["calibration wand", "distorted field grid", "alignment markers"]),
    "card_fsti_031": dict(props=["cryo-unit", "venting cold vapor", "frost creep"]),
    "card_fsti_032": dict(props=["full-room projection", "blue wireframe terrain"]),
    "card_fsti_033": dict(sc="mecha", props=["jammer device", "scrambled noise field", "flickering reticle"]),
    "card_fsti_034": dict(props=["thermal visor", "false-color heat signatures", "outlined hot target"]),
    "card_fsti_035": dict(focal="a rapid cascade of glowing process steps compressing into one bright output burst",
                           props=["cascading process steps", "compressed output burst"]),
    "card_fsti_036": dict(focal="a single unit half-shifted out of phase, translucent edges and a doubled afterimage",
                           props=["translucent edges", "doubled afterimage", "cyan shimmer"]),
    "card_fsti_037": dict(focal="a red-highlighted crack in a defensive grid with a slim probe slipping through the gap",
                           props=["defense grid", "red crack highlight", "slim probe"]),
    "card_fsti_038": dict(sc="mecha", focal="a retrieval arm pulling one bright data shard from a stack of glowing memory cores",
                           props=["stacked memory cores", "retrieval arm", "bright data shard"]),
    "card_fsti_039": dict(focal="a self-referential spiral of repeating glyphs feeding back into itself",
                           props=["glyph spiral", "feedback loop arcs"]),
    "card_fsti_040": dict(focal="a live control-loop diagram whose output arcs back to reinforce its input",
                           props=["control-loop diagram", "reinforcing output arc"]),
    # -------------------------------------------------------------------- Epic
    "card_fsti_041": dict(props=["two linked particles", "correlation lines", "lab gap"]),
    "card_fsti_042": dict(props=["towering mainframe", "rolling dampening field", "custodian armor"]),
    "card_fsti_043": dict(sc="mecha", props=["multi-core cluster", "parallel light lanes"]),
    "card_fsti_044": dict(props=["holo-table", "broadcast upgrade ring"]),
    "card_fsti_045": dict(sc="mecha", props=["dissonant wave emitter", "stalled machinery", "wavefront distortion"]),
    "card_fsti_046": dict(focal="a neural network diagram brightening as it trains, weights shifting and output sharpening",
                           props=["neural network diagram", "shifting weight links", "sharpened output node"]),
    "card_fsti_047": dict(focal="a red override glyph seizing control while locked-out units freeze mid-action",
                           props=["red override glyph", "frozen units", "lockout rings"]),
    "card_fsti_048": dict(focal="a fractal corridor of ever-repeating data, each level spawning the next",
                           props=["fractal data corridor", "nested repeating layers"]),
    # --------------------------------------------------------------- Legendary
    "card_fsti_049": dict(focus="full-body iconic presence, subject centred",
                           props=["orbiting data halo", "high-collared coat", "instrumented observatory hall"]),
    "card_fsti_050": dict(sc="mecha", focus="full-body iconic presence, structure centred edge to edge",
                           focal="a vast crystalline processing core radiating light as the hub of every FSTI network",
                           props=["crystalline processing core", "radiating network conduits", "command ring"]),
}
