#!/usr/bin/env python3
"""Overlink card art bible - the cohesion mechanism (axis 7).

Seven-axis profile, visual pillars, faction color scripts, rarity escalation,
type conventions, and the ComfyUI technical spec derived from the real frame
geometry in Assets/Scripts/Cards/CardFaceView.cs and the verified landscape
workflow reference.

Everything a per-card prompt needs is composed from this file plus the card's
unique description, so the 150 outputs stay coherent instead of drifting.
"""

ART_BIBLE = {
    # ------------------------------------------------------------------ axes
    "axes": {
        "pipeline": (
            "hand-painted 2D digital illustration, gacha/TCG splash-art tier, "
            "crisp edges, cinematic key light, semi-realistic anatomy and hard-surface detail"
        ),
        "line": (
            "no visible ink outline; form defined by value edges (painterly), "
            "with thin confident edge accents on machinery and glass"
        ),
        "shading": (
            "soft painterly shading with cel-shaded read at distance, cool-tinted shadows, "
            "single strong key light from upper left plus a colored rim light from the faction accent"
        ),
        "palette": (
            "shared deep slate-navy neutral background so all three factions sit in one world; "
            "each faction owns a key color and an emissive accent; rarity raises glow and ornament, never hue"
        ),
        "silhouette": (
            "faction shape language: FSTI slim verticals, rounded glass, fine antennae, hexagon motifs; "
            "FRTI heavy angular blocks, wide stances, riveted plates; "
            "FPB soft organic curves, arched frames, paper planes, protective shells"
        ),
        "material": (
            "FSTI glass, hologram, chrome, matte composite; "
            "FRTI brushed steel, oiled iron, ceramic heat shielding, soot; "
            "FPB paper, canvas, gilt leaf, varnish, pale stone"
        ),
        "cohesion": (
            "single art bible (this file) + consistent camera (3/4 hero angle at eye level) + "
            "consistent lighting direction + rarity escalation table; one named style owner: Themis"
        ),
    },

    # ---------------------------------------------------------------- pillars
    "pillars": [
        "A frontier institute, not a battlefield - technology and craft shown at work in a real "
        "facility; danger is implied by energy and scale, never gore.",
        "Light tells you the faction - the emissive accent color is the primary read at thumbnail "
        "size, before any detail resolves.",
        "Every card is a portrait of competence - the subject is mid-task, capable and purposeful, "
        "never a passive pose against an empty background.",
    ],

    # -------------------------------------------------------- faction scripts
    "factions": {
        "fsti": {
            "name": "FSTI - Frontier Science and Technology Institute",
            "identity": "signal and data control - draw, NP generation, Protocols, effect chaining",
            "key_color": "cool cyan and steel blue",
            "accent": "white glass highlights with magenta data accents",
            "environment": "clean research labs, server halls, observatories, instrumented campuses",
            "materials": "glass, holographic displays, chrome, matte white composite, sensor arrays",
            "shape_language": "slim verticals, rounded glass, fine antennae, hexagon motifs",
            "light_note": "cold even ambient with a bright localized screen/holo key",
            "style_tokens": (
                "clean futuristic laboratory, holographic displays, cyan glow, chrome and glass surfaces, "
                "scientific instruments, cool blue color scheme, white composite paneling, data streams"
            ),
        },
        "frti": {
            "name": "FRTI - Frontier Research and Technology Institute",
            "identity": "industrial combat - high ATK, durable frames, Relays as structures",
            "key_color": "hot amber and iron grey",
            "accent": "ember orange with red heat glow",
            "environment": "foundries, assembly floors, gantry cranes, reactor bays, rail yards",
            "materials": "brushed steel, oiled iron, ceramic heat shielding, soot, riveted plate",
            "shape_language": "heavy angular blocks, wide stances, riveted plates, chunky hydraulics",
            "light_note": "low warm ambient with a hard molten key from below or from a furnace mouth",
            "style_tokens": (
                "heavy industrial foundry, molten metal glow, ember sparks, riveted steel plating, "
                "hydraulic machinery, soot and grime, warm amber color scheme, iron grey metal"
            ),
        },
        "fpb": {
            "name": "FPB - Frontier Planning Bureau",
            "identity": "attrition and defence - Shield, HP, healing, Repair, Node holding",
            "key_color": "warm parchment and ochre",
            "accent": "gilt gold with ink teal and sage green",
            "environment": "drafting studios, blueprint archives, conservation benches, galleries",
            "materials": "paper, canvas, gilt leaf, varnish, pale stone, brass drafting tools",
            "shape_language": "soft organic curves, arched frames, paper planes, protective shells",
            "light_note": "soft warm daylight from a high window, gentle protective bloom on coated surfaces",
            "style_tokens": (
                "architectural drafting studio, blueprint paper, gilt gold leaf, warm parchment tones, "
                "ink and varnish, conservation workshop, arched gallery architecture, soft daylight"
            ),
        },
    },

    # -------------------------------------------------------- rarity escalation
    "rarities": {
        "Common": {
            "treatment": (
                "single subject, plain readable environment, naturalistic lighting, minimal glow, "
                "functional clothing or machinery, no ornament"
            ),
            "style_tokens": "simple composition, naturalistic lighting, muted colors, understated detail",
        },
        "Rare": {
            "treatment": (
                "richer environment detail, one strong accent light, mild atmospheric FX, "
                "visible specialization in costume or hardware"
            ),
            "style_tokens": "detailed environment background, accent lighting, mild atmospheric effects",
        },
        "Epic": {
            "treatment": (
                "dramatic lighting, volumetric FX, ornate costume or advanced tech, "
                "strong silhouette, moment of peak action"
            ),
            "style_tokens": (
                "dramatic lighting, volumetric lighting, ornate detail, intense atmosphere, "
                "strong silhouette, dynamic moment"
            ),
        },
        "Legendary": {
            "treatment": (
                "iconic full-body presence, heavy colored rim light, particle and glow FX, "
                "maximum ornament, the defining figure or artifact of the faction; "
                "full-art composition that runs edge to edge beneath a translucent frame"
            ),
            "style_tokens": (
                "iconic heroic composition, heavy rim lighting, particle effects, glowing accents, "
                "maximum ornament, legendary key art, full body visible"
            ),
        },
    },

    # --------------------------------------------------------- type conventions
    "types": {
        "Unit": (
            "character or machine portrait with agency - shown mid-task, 3/4 hero angle, "
            "reading as a person or vehicle that acts"
        ),
        "Ability": (
            "a single decisive effect moment - energy, impact, or process; usually no character, "
            "one contained focal object with a sharp defined boundary"
        ),
        "Relay": (
            "an installation or structure - static infrastructure in its environment, "
            "no character required, solid readable silhouette"
        ),
        "Protocol": (
            "a systemic or procedural motif - fields, diagrams, and ongoing processes; "
            "semi-abstract but anchored to one designed object, never formless glow"
        ),
    },

    # ------------------------------------------------- ComfyUI technical spec
    "comfyui": {
        # Derived from CardFaceView.cs: Standard art window 700x385 (ratio 1.818)
        # at (66,66) in an 832x1216 frame; Legendary is FullArt 832x1216.
        "dims": {
            "Standard": {
                "applies_to": ["Common", "Rare", "Epic"],
                "art_window": "700x385 at offset (66,66) inside an 832x1216 frame",
                "window_ratio": 1.818,
                "generate": [1400, 768],
                "generate_note": (
                    "1400x768 = ratio 1.823, short side 768 meets the SDXL >=768 heuristic; "
                    "crops ~0.3% horizontally into the 1.818 window. Exact-ratio alternative "
                    "1280x704 falls below the heuristic."
                ),
                "focus": "ArtFocusY 0.4 - weight the subject slightly above centre",
            },
            "Legendary": {
                "applies_to": ["Legendary"],
                "art_window": "full art, edge to edge, 832x1216 (frame is a translucent overlay)",
                "window_ratio": 0.684,
                "generate": [832, 1216],
                "generate_note": "matches the verified portrait splash-art canvas exactly, no crop",
                "focus": "ArtFocusY 0.5 - centre the subject",
            },
        },
        "sampler": {
            "name": "euler_ancestral",
            "steps": 35,
            "cfg": 7,
            "note": "epsilon-pred SDXL (Animagine/Illustrious/NoobAI) - plain euler gives garbage; CFG 7 for prompt fidelity",
        },
        "checkpoints": {
            "human": {"model": "animagine-xl-3.1.safetensors", "lora_strength_model": 1.5, "lora_strength_clip": 1.0},
            "mecha": {"model": "illustrious-xl-v0.1.safetensors", "lora_strength_model": 0.0, "lora_strength_clip": 0.0},
            "effect": {"model": "illustrious-xl-v0.1.safetensors", "lora_strength_model": 0.0, "lora_strength_clip": 0.0},
        },
        # Landscape style node = rendering language only. These tokens are
        # verified-harmful at wide aspect and are asserted absent by the verifier.
        "banned_style_tokens": [
            "dutch angle",
            "maximalist composition",
            "hard surface lighting",
            "flat vector style",
            "environment-prominent composition",
            "subject offset to side",
        ],
        "required_style_tokens_landscape": [
            "masterpiece", "best quality", "newest", "absurdres", "highres", "ultra-detailed",
            "gacha splash art", "official art", "soft painterly shading", "cel shading",
            "detailed lineart", "crisp linework", "vibrant colors", "high saturation",
            "colorful rim lighting", "volumetric lighting", "edge light emphasis",
            "dramatic lighting", "detailed environment background", "wide landscape composition",
        ],
        "required_style_tokens_portrait": [
            "masterpiece", "best quality", "newest", "absurdres", "highres", "ultra-detailed",
            "gacha splash art", "official art", "soft painterly shading", "cel shading",
            "detailed lineart", "crisp linework", "vibrant colors", "high saturation",
            "colorful rim lighting", "volumetric lighting", "edge light emphasis",
            "dramatic lighting", "detailed environment background",
        ],
        "negative_mandatory": (
            "nsfw, worst quality, old, early, low quality, lowres, watermark, signature, username, artist name, "
            "deformed, bad anatomy, jpeg artifacts, (blurry:1.3), (haze:1.2), "
            "low contrast, muddy colors, washed out colors, empty space, simple background, plain background, "
            "white background, monochrome, centered symmetrical composition, straight pose, static pose, stiff pose, "
            "dark shadows, flat lighting, no lighting, "
            "border, frame, card border, card frame, card art border, game ui, hud, interface, "
            "thick border, ornate border, inner border, outer frame, card overlay, framed image, panel border, "
            "text, letters, numbers, logo, wordmark"
        ),
        "negative_landscape_collapse": (
            "flat vector, glitch art, abstract, no character, no humans, empty scene, "
            "landscape only, light trails, neon streaks, motion blur streaks, "
            "diagonal light bands, portrait crop, extreme close-up, still pose, tiny character"
        ),
        "negative_effect_drift": (
            "random neon streaks, abstract wallpaper, formless glow, light trails only, "
            "motion blur streaks, no focal point, multiple disconnected effects"
        ),
        "negative_human": (
            "bad hands, mutated hands, extra fingers, missing fingers, fused fingers, "
            "crystal armor, gemstone armor, round face, open mouth, teeth"
        ),
        "negative_mecha": (
            "organic body, skin, hair, human face, girl, boy, flesh, animal, soft body, rounded armor, "
            "toy mecha, super deformed, chibi, smooth featureless plates, missing panel lines, "
            "melting metal, fused joints, extra limbs, crystal clothing, gemstone dress"
        ),
        "effect_structure_tokens": (
            "single contained energy form, one clear focal object, sharp defined boundary, "
            "readable technical effect, single clear focal point, defined silhouette"
        ),
        "subject_front_load": {
            "human": "(1girl:1.2), (solo:1.2), full character visible, character large in frame",
            "male_human": "(1boy:1.2), (solo:1.2), full character visible, character large in frame",
            "mecha": "(mecha:1.2), (no humans:1.1), (solo:1.2), machine large in frame",
            "effect": "(no humans:1.2), single contained energy form, one focal object at frame center, sharp defined boundary",
        },
        "max_named_props": 3,
    },
}
