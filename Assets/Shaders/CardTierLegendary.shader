Shader "Overlink/CardTier/Legendary"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _Color ("Sprite Color", Color) = (1,1,1,1)
        _AccentColor ("Accent Color", Color) = (0.86,0.88,0.94,1)
        _Intensity ("Intensity", Range(0,2)) = 1.0
        _Speed ("Speed", Range(0,4)) = 0.5
        _NoiseScale ("Dust Density", Range(0,40)) = 18
        _EmissionStrength ("Rail Glow", Range(0,2)) = 0.7
        _EffectEnabled ("Effect Enabled", Float) = 1
        _GlowThreshold ("Glow Threshold", Range(0,1)) = 0.28
        _FacetWidth ("Facet Width", Range(0.01,0.5)) = 0.2
        _FacetStrength ("Facet Strength", Range(0,2)) = 0.9
        _FoilOpacity ("Foil Opacity", Range(0,1)) = 0.5
        _SparkleStrength ("Dust Strength", Range(0,2)) = 1.4
        _Iridescence ("Iridescence", Range(0,1)) = 0.85
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off ZTest Always

        // Legendary foil: the sprite's own colour stays the image, and the effect only ADDS light.
        //
        // Compositing note (this is what made the first two passes invisible): the frame is a
        // translucent overlay (alpha ~47 on its plates, ~1 on the open canvas), and with
        // SrcAlpha blending the GPU multiplies fragment RGB by fragment alpha before it reaches
        // the screen. Light added to RGB while returning the original alpha is attenuated ~250x
        // over the art. So this shader works in screen space: light is accumulated premultiplied,
        // alpha is raised wherever foil or dust lands (real stamped foil is opaque and sits ON
        // TOP of the print), and the source RGB is folded back out as screen / outA. Pixels the
        // effect never touches come out bit-exact base.
        //
        // Layer map:
        //   broad band + hot core   -> the foil sweep that reads as a moving reflection
        //   two dust layers         -> glinting motes, each with its own hue and twinkle clock
        //   alpha*luminance glow    -> the frame's own rails blooming, gated so plates stay dark
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
            float _NoiseScale;
            float _EmissionStrength;
            float _EffectEnabled;
            float _GlowThreshold;
            float _FacetWidth;
            float _FacetStrength;
            float _FoilOpacity;
            float _SparkleStrength;
            float _Iridescence;
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

            // Cosine palette (IQ). Cool mean: silver -> cyan -> violet -> rose.
            static half3 ThinFilm(float t)
            {
                const half3 a = half3(0.62, 0.64, 0.68);
                const half3 b = half3(0.38, 0.34, 0.42);
                const half3 c = half3(1.00, 1.00, 1.00);
                const half3 d = half3(0.00, 0.30, 0.62);
                return a + b * cos(6.2831853 * (c * t + d));
            }

            static half3 FilmOrSilver(float t, float irid)
            {
                return lerp(half3(1.0, 1.0, 1.0), ThinFilm(t), irid);
            }

            // One dust layer: a mote per cell at a random spot, on its own twinkle clock, with a
            // tight core and a faint four-way flare so a bright mote reads as a glint, not a blob.
            static float DustLayer(float2 uv, float scale, float speed, float time, out float seedOut)
            {
                float2 grid = uv * scale;
                float2 cellId = floor(grid);
                float2 cellUv = frac(grid);
                float seed = Hash(cellId);
                seedOut = seed;
                float2 mote = float2(Hash(cellId + 17.31), Hash(cellId + 41.07));
                mote += 0.2 * sin(float2(time * speed + seed * 6.28,
                                         time * speed * 0.8 + seed * 3.14));
                float2 delta = cellUv - clamp(mote, 0.15, 0.85);
                float core = 1.0 - smoothstep(0.0, 0.13, length(delta));
                float streak = max(0.0, 1.0 - abs(delta.x) * 26.0) * max(0.0, 1.0 - abs(delta.y) * 5.0)
                             + max(0.0, 1.0 - abs(delta.y) * 26.0) * max(0.0, 1.0 - abs(delta.x) * 5.0);
                float twinkle = pow(max(0.0, sin(time * (1.1 + seed * 2.6) + seed * 6.2831)), 9.0);
                return (core + streak * 0.35) * twinkle * step(0.42, seed);
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

                // --- foil sweep: broad band with a hot narrow core, on two crossing angles -------
                float sweep = i.uv.x * 0.62 + i.uv.y * 1.14 - t * _Speed * 0.22;
                float d1 = abs(frac(sweep) - 0.5);
                float width = max(_FacetWidth, 0.001);
                float broad = 1.0 - smoothstep(0.0, width, d1);
                float core = 1.0 - smoothstep(0.0, width * 0.22, d1);
                float sweepB = i.uv.x * 1.05 - i.uv.y * 0.48 - t * _Speed * 0.13;
                float cross = (1.0 - smoothstep(0.0, width * 0.5, abs(frac(sweepB) - 0.5))) * 0.45;
                float facet = saturate(broad * 0.6 + core * 0.9 + cross);

                // --- glowing dust: two layers for parallax depth --------------------------------
                float seedA, seedB;
                float density = max(_NoiseScale, 2.0);
                float dustA = DustLayer(i.uv, density, 0.55, t, seedA);
                float dustB = DustLayer(i.uv, density * 2.3, 0.9, t + 13.7, seedB);
                float dust = min(dustA + dustB * 0.7, 1.6);

                // --- rail glow: the only term the frame's translucency should gate ---------------
                float glow = smoothstep(_GlowThreshold, 1.0, lum) * a;

                half3 foilTint = FilmOrSilver(frac(sweep + lum * 0.35), irid);
                half3 accent = _AccentColor.rgb * foilTint;

                // Each mote carries its own hue, so the dust scatters coloured light rather than
                // flickering white. Kept near-white so it stays neutral overall.
                half3 dustTint = lerp(half3(1.0, 1.0, 1.0),
                    ThinFilm(frac(seedA * 0.83 + sweep * 0.5)), irid * 0.9);
                half3 dustAccent = _AccentColor.rgb *
                    lerp(half3(1.0, 1.0, 1.0),
                        ThinFilm(frac(seedB * 0.71 + sweep * 0.4 + 0.25)), irid * 0.9);

                // Premultiplied screen-space accumulation.
                float3 screen = base.rgb * a;
                screen += accent * (facet * _FacetStrength) * on;
                screen += accent * (glow * _EmissionStrength) * on;
                screen += base.rgb * (0.18 * glow * on) * a;
                screen += dustTint * _AccentColor.rgb * (dustA * _SparkleStrength) * on;
                screen += dustAccent * _AccentColor.rgb * (dustB * 0.7 * _SparkleStrength) * on;

                // Foil and dust buy their own opacity; without this SrcAlpha eats the added light.
                float outA = saturate(a
                    + facet * _FoilOpacity * on
                    + (dustA + dustB * 0.7) * 0.9 * on
                    + glow * 0.25 * on);
                float3 col = screen / max(outA, 0.0035);

                return half4(min(col, half3(6.0, 6.0, 6.0)), outA);
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
