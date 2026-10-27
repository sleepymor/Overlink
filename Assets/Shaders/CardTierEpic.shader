Shader "Overlink/CardTier/Epic"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _Color ("Sprite Color", Color) = (1,1,1,1)
        _AccentColor ("Accent Color", Color) = (0.90,0.92,0.96,1)
        _Intensity ("Intensity", Range(0,2)) = 0.5
        _Speed ("Speed", Range(0,4)) = 0.3
        _CrackleScale ("Crackle Scale", Range(1,40)) = 7
        _CrackleContrast ("Crackle Contrast", Range(0,1)) = 0.4
        _EdgeGlow ("Edge Glow", Range(0,1)) = 0.5
        _EmissionStrength ("Rail Glow", Range(0,2)) = 0.14
        _EffectEnabled ("Effect Enabled", Float) = 1
        _GlowThreshold ("Glow Threshold", Range(0,1)) = 0.42
        _HoloStrength ("Holo Strength", Range(0,2)) = 0.35
        _HoloOpacity ("Holo Opacity", Range(0,1)) = 0.04
        _Iridescence ("Iridescence", Range(0,1)) = 0
        _PrismStrength ("Prism Strength", Range(0,1)) = 0
        _PrismFreq ("Prism Frequency", Range(0.1,4)) = 1.0
        _PrismSat ("Prism Saturation", Range(0,1)) = 0.6
        _PrismSpread ("Colour Spread", Range(0,1)) = 0
        _PrismWarp ("Hue Warp", Range(0,1)) = 0
        _FacetSharp ("Facet Sharpness", Range(1,12)) = 6
        _MeshJitter ("Mesh Jitter", Range(0,0.6)) = 0.34
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "RenderPipeline"="UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off ZTest Always

        // This is the EPIC HOLO finish. It is a PRISMATIC CRACKLE: an irregular TRIANGLE mesh where
        // every facet is a tiny mirror tilted a different way, so facets switch on and off as one
        // flat unit while light sweeps the card. It replaces an earlier stripe grating (looked like a
        // barcode) and then an earlier jittered-Voronoi crackle (rounded blob cells whose brightness
        // varied per pixel inside each cell, which read as a slow oil-slick shimmer, not prisms).
        // Rare owns the foil-only shader (CardTierRare.shader); Legendary adds the strongest foil
        // sweep plus dust (CardTierLegendary.shader).
        //
        // FILE HISTORY: this file was CardTier.shader while declaring "Overlink/CardTier/Rare", and
        // CardTierRare.shader was CardTierEpic.shader while declaring "Overlink/CardTier/Epic". The
        // two were cross-named, which is what made two tuning passes talk past each other. Renamed
        // 2026-10-26 with each .meta moved alongside, so the guids (this one: 68600cce46102e1498872a78200c4a97)
        // and therefore Epic.mat's reference survived untouched. See docs/CARD-FRAME-SPEC.md section 13.
        //
        // _Iridescence defaults to 0 (structure with no hue), _PrismStrength defaults to 0 (no
        // dispersion) and _FacetSharp 1 is the widest, softest flash. At the zero defaults the finish
        // is the old achromatic crackle. Epic.mat runs dispersion on with a restrained hand.
        // The five invariants that keep the card's own colours intact:
        //   1. The facet pattern MODULATES the base rather than depositing light on it, mean-preserving,
        //      so it redistributes brightness between facets without lifting the card overall - the
        //      earliest additive version greyed the whole frame into a haze.
        //   2. Hue rides on the ADDITIVE glints only, never on the modulation term, so a saturated
        //      facet cannot recolour the artwork underneath it.
        //   3. Only facets and crack rims ADD light, and alpha is lifted only where that light lands,
        //      so SrcAlpha cannot eat the effect and the art window stays clear.
        //   4. Everything derived from the light vector is sampled per FACET, not per pixel. That is
        //      what makes it read as a surface of discrete mirrors; the per-pixel version is the
        //      smooth shimmer that was reported as wrong.
        //   5. Added light is weighted by `key`, the brightness of the pixel underneath, so a prism
        //      flash on dark frame ink stays dim instead of becoming a floating coloured sticker.
        //   6. Facet faces, crack rims and the rail bloom take three separated spectrum phases, so
        //      the dispersion reads as light splitting rather than one rainbow laid over the card.
        //
        // Colour variety comes from four independent terms in the hue, not from one ramp: the
        // travelling phase runs BACKWARDS on half the facets (facet.order), each facet carries its own
        // wheel offset (_PrismSpread), the hue origin wanders across the card (_PrismWarp), and each
        // facet detunes the palettogram's channel spacing so hues land in the yellow-green and cyan
        // gaps a fixed 1/3, 2/3 spacing under-samples. All four are dialable and default to 0, so the
        // finish degrades gracefully back to the single-ramp version rather than needing a revert.
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _Color;
            float4 _AccentColor;
            float _Intensity;
            float _Speed;
            float _CrackleScale;
            float _CrackleContrast;
            float _EdgeGlow;
            float _EmissionStrength;
            float _EffectEnabled;
            float _GlowThreshold;
            float _HoloStrength;
            float _HoloOpacity;
            float _Iridescence;
            float _PrismStrength;
            float _PrismFreq;
            float _PrismSat;
            float _PrismSpread;
            float _PrismWarp;
            float _FacetSharp;
            float _MeshJitter;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            static float Hash(float2 p)
            {
                return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453);
            }

            // Cosine palette (IQ), cool mean. Only reached when _Iridescence is dialled above 0.
            static half3 ThinFilm(float t)
            {
                const half3 a = half3(0.62, 0.64, 0.68);
                const half3 b = half3(0.38, 0.34, 0.42);
                const half3 c = half3(1.00, 1.00, 1.00);
                const half3 d = half3(0.00, 0.30, 0.62);
                return a + b * cos(6.2831853 * (c * t + d));
            }

            static half3 FilmOrWhite(float t, float irid)
            {
                return lerp(half3(1.0, 1.0, 1.0), ThinFilm(t), irid);
            }

            // Full-spectrum dispersion: an IQ palettogram at unit amplitude, so the hue actually
            // travels the wheel instead of hovering around silver like ThinFilm does. This is what
            // makes the crackle read as PRISMATIC rather than as a grey mirror mosaic. _PrismStrength
            // mixes it in over the neutral tint, and at 0 the finish is bit-exact the old achromatic
            // crackle, so this layer is opt-in per material.
            //
            // `detune` shifts the green and blue channels in OPPOSITE directions, which changes the
            // channel spacing away from the textbook 1/3, 2/3. The plain palettogram under-samples
            // yellow-green and cyan - every facet lands in the same handful of hues - so giving each
            // facet its own film thickness spreads the population across the gaps. This is the same
            // trick thin-film interference actually uses: hue and saturation are both functions of
            // thickness, not just hue.
            static half3 Spectrum(float h, float detune)
            {
                return half3(0.5 + 0.5 * cos(6.2831853 * h),
                             0.5 + 0.5 * cos(6.2831853 * (h + 0.3333 + detune)),
                             0.5 + 0.5 * cos(6.2831853 * (h + 0.6667 - detune)));
            }

            // Smooth two-octave value noise, used only to wander the hue origin. Cheap and, unlike a
            // raw hash, continuous: a facet's neighbours stay related instead of decorrelating fully.
            static float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1.0, 0.0)), u.x),
                            lerp(Hash(i + float2(0.0, 1.0)), Hash(i + float2(1.0, 1.0)), u.x), u.y);
            }

            struct Facet
            {
                float tilt;    // 0..1 per-facet orientation, drives flash phase
                float seed;    // 0..1 per-facet random, drives static brightness spread
                float hueOff;  // 0..1 third uncorrelated hash: this facet's place on the wheel
                float detune;  // -1..1 per-facet film-thickness offset for the palettogram
                float order;   // -1 or 1: which way the spectrum runs across this facet
                float edge;    // 1 on a triangle boundary, 0 in its interior
            };

            static float2 Jitter(float2 id)
            {
                return float2(Hash(id), Hash(id + 19.17)) - 0.5;
            }

            static float SegDist(float2 p, float2 a, float2 b)
            {
                float2 ab = b - a;
                float t = clamp(dot(p - a, ab) / max(dot(ab, ab), 1e-6), 0.0, 1.0);
                return length(p - (a + ab * t));
            }

            // Jittered TRIANGLE lattice: a sheared quad grid, each quad split by a per-cell diagonal
            // whose direction alternates randomly, with the shared corners jittered off-grid. That
            // yields irregular triangles of varying size and angle - the crystal-face silhouette a
            // prismatic foil actually has - instead of the rounded blob cells a Voronoi crackle gives.
            //
            // Corner ids are computed from the integer lattice, so adjacent quads agree on every
            // shared vertex and the mesh stays watertight: no cracks, no overlaps, no per-cell seams.
            static Facet SampleFacets(float2 uv, float scale, float jitter)
            {
                float2 grid = uv * max(scale, 0.5);
                // Mild shear so no edge of the mesh is parallel to a card edge.
                grid += float2(grid.y * 0.21, grid.x * 0.08);
                float2 cell = floor(grid);
                float2 f = frac(grid);
                float j = clamp(jitter, 0.0, 0.6);

                float2 p00 = float2(0.0, 0.0) + Jitter(cell) * j;
                float2 p10 = float2(1.0, 0.0) + Jitter(cell + float2(1.0, 0.0)) * j;
                float2 p01 = float2(0.0, 1.0) + Jitter(cell + float2(0.0, 1.0)) * j;
                float2 p11 = float2(1.0, 1.0) + Jitter(cell + float2(1.0, 1.0)) * j;

                // Two triangles per quad, on a randomly chosen diagonal.
                float2 a0, a1, a2;
                float2 off;
                bool anti = Hash(cell + 5.37) < 0.5;
                if (anti)
                {
                    // diagonal p10 -> p01
                    float cross2 = (p01.x - p10.x) * (f.y - p10.y) - (p01.y - p10.y) * (f.x - p10.x);
                    off = cross2 >= 0.0 ? float2(0.0, 0.0) : float2(37.13, 11.79);
                    a0 = p10; a1 = p01; a2 = cross2 >= 0.0 ? p00 : p11;
                }
                else
                {
                    // diagonal p00 -> p11
                    float cross2 = (p11.x - p00.x) * (f.y - p00.y) - (p11.y - p00.y) * (f.x - p00.x);
                    off = cross2 >= 0.0 ? float2(37.13, 11.79) : float2(0.0, 0.0);
                    a0 = p00; a1 = p11; a2 = cross2 >= 0.0 ? p01 : p10;
                }

                float2 id = cell + off;
                Facet facet;
                facet.tilt = Hash(id + 3.71);
                facet.seed = Hash(id + 7.31);
                // Three more uncorrelated salts, so a facet's colour identity is independent of the
                // flash phase that tilt/seed already drive.
                facet.hueOff = Hash(id + 23.19);
                facet.detune = Hash(id + 51.83) * 2.0 - 1.0;
                facet.order = Hash(id + 44.53) < 0.5 ? -1.0 : 1.0;
                float dMin = min(SegDist(f, a0, a1), min(SegDist(f, a1, a2), SegDist(f, a2, a0)));
                facet.edge = 1.0 - smoothstep(0.0, 0.075, dMin);
                return facet;
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 base = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;
                float a = base.a;
                float on = saturate(_EffectEnabled) * saturate(_Intensity);
                if (on <= 0.0) return base;

                float lum = dot(base.rgb, float3(0.2126, 0.7152, 0.0722));
                float t = _Time.y;
                float irid = saturate(_Iridescence);

                // The transparent art window must not acquire shard silhouettes. Only ink in the
                // frame catches this finish; its nearly-zero-alpha gaps remain untouched.
                float gate = smoothstep(0.02, 0.22, a);

                Facet facet = SampleFacets(i.uv, _CrackleScale, _MeshJitter);

                // One slow light vector crosses the card. Every quantity derived from it is sampled
                // at the FACET, not at the pixel: the whole triangle is one flat colour that flashes
                // as a unit. This is the fix for the old smooth swell, where `light` entered the
                // facing term per pixel and painted a gradient inside every shard - an oil slick
                // pretending to be a prismatic surface.
                float light = i.uv.x * 0.85 + i.uv.y * 1.25 - t * _Speed * 0.22;

                // Wrapped distance to the flash peak, 0 at peak and 1 at the trough, then a narrow
                // Gaussian in that distance: a facet sits dark, crosses a threshold and snaps on.
                // _FacetSharp is the inverse width, so higher = fewer facets lit at once and each
                // flash is a harder edge rather than a fade.
                float phase = frac(light * 0.5 + facet.tilt);
                float d = abs(phase - 0.5) * 2.0;
                float sharp = max(_FacetSharp, 1.0);
                float lit = exp(-d * d * sharp);

                // A static per-facet spread so the mesh is legible even where no facet is flashing.
                float facetVar = facet.seed - 0.5;

                // Achromatic, mean-preserving modulation of what is already printed. Hue never
                // enters this term, so a saturated facet cannot recolour the artwork under it - it
                // can only make its own reflected light coloured.
                float modulation = 1.0 + (lit * 0.8 + facetVar * 0.35 - 0.25)
                    * _CrackleContrast * gate * on;

                // Crack rims catch light from whichever facet bounds them, brightest when that facet
                // is itself flashing.
                float crackGlint = facet.edge * (0.3 + 0.7 * lit) * _EdgeGlow;

                // Rail glow: only the frame's own bright ink blooms.
                float glow = smoothstep(_GlowThreshold, 1.0, lum) * a;
                float glowAmt = glow * _EmissionStrength * on;

                // --- prismatic dispersion -------------------------------------------------------
                // One hue per facet, constant across its whole area, so neighbouring triangles are
                // different colours and the rainbow moves by facets switching rather than by a
                // gradient sliding. Four independent contributions, each with its own dial:
                //   phase * _PrismFreq * order - the travelling ramp, run BACKWARDS on half the
                //     facets. This is the big one: two adjacent facets the same distance along the
                //     light vector cycle their hues in opposite directions, so the surface stops
                //     reading as one rainbow sweeping past and starts reading as many prisms.
                //   hueOff * (0.34 + 0.66 * spread) - a static per-facet offset on the wheel. At
                //     _PrismSpread 0 the facets only differ by where they sit on the ramp (a smooth
                //     gradient across the card); at 1 each is anywhere on the wheel.
                //   warp - a slow continuous wander of the hue origin, so regions of the card drift
                //     apart instead of marching in one straight line.
                //   detune - per-facet palettogram spacing, which moves hues between the yellow-green
                //     and cyan gaps the textbook 1/3, 2/3 spacing under-samples.
                float prism = saturate(_PrismStrength);
                float sat = saturate(_PrismSat);
                float spread = saturate(_PrismSpread);
                float warp = (ValueNoise(i.uv * float2(3.1, 2.4)
                    + float2(t * 0.021, -t * 0.014)) - 0.5) * saturate(_PrismWarp);
                float hue = frac(phase * max(_PrismFreq, 0.01) * facet.order
                    + facet.hueOff * (0.34 + 0.66 * spread) + warp);
                // +-0.09 of channel-spacing, scaled by spread so the whole effect stays dialable.
                float detune = facet.detune * 0.09 * (0.35 + 0.65 * spread);
                half3 neutralTint = FilmOrWhite(frac(light * 0.5), irid);
                // Desaturate toward white before anything else: Spectrum() is a full-amplitude
                // palettogram and at full sat it reads as Christmas foil, not as a prism edge.
                // Rim and bloom take their own shifts, now partly per-facet rather than two fixed
                // constants, so the three layers disagree about colour the way a real edge would.
                half3 film = lerp(half3(1.0, 1.0, 1.0), Spectrum(hue, detune), sat);
                float rimShift = 0.14 + 0.26 * facet.seed;
                half3 rimFilm = lerp(half3(1.0, 1.0, 1.0),
                    Spectrum(hue + rimShift, detune * 0.6), sat);
                // The bloom stays closer to neutral than the glints, or the whole frame reads as a
                // colour cast rather than a foil catching light on its edges.
                half3 glowFilm = lerp(half3(1.0, 1.0, 1.0),
                    Spectrum(hue + rimShift * 0.5, detune * 0.3), sat * 0.55);

                // Added light is scaled by the facet's own brightness underneath it, so a prism
                // flash on dark frame ink stays dim instead of becoming a floating coloured sticker.
                float key = 0.35 + 0.65 * saturate(lum * 1.6);
                half3 facetAccent = _AccentColor.rgb * lerp(neutralTint, film, prism) * key;
                half3 rimAccent = _AccentColor.rgb * lerp(neutralTint, rimFilm, prism) * key;
                half3 glowAccent = _AccentColor.rgb * lerp(neutralTint, glowFilm, prism * 0.55);

                float3 screen = base.rgb * a * modulation;
                screen += facetAccent * (lit * _HoloStrength * gate * on * 0.55);
                screen += rimAccent * (crackGlint * _HoloStrength * gate * on * 0.4);
                screen += glowAccent * glowAmt;
                screen += base.rgb * (0.10 * glowAmt) * a;

                // Alpha lifts only where light actually lands, so the film never thickens into a
                // haze over the artwork.
                float lightAmt = lit * gate + crackGlint * gate + glowAmt;
                float outA = saturate(a + lightAmt * _HoloOpacity);
                float3 col = screen / max(outA, 0.0035);

                return half4(min(col, half3(3.0, 3.0, 3.0)), outA);
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
