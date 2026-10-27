Shader "Overlink/CardTier/Rare"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [PerRendererData] _Color ("Sprite Color", Color) = (1,1,1,1)
        _AccentColor ("Accent Color", Color) = (0.88,0.90,0.95,1)
        _Intensity ("Intensity", Range(0,2)) = 0.55
        _Speed ("Speed", Range(0,4)) = 0.35
        _EmissionStrength ("Rail Glow", Range(0,2)) = 0.2
        _EffectEnabled ("Effect Enabled", Float) = 1
        _GlowThreshold ("Glow Threshold", Range(0,1)) = 0.4
        _FacetWidth ("Foil Width", Range(0.01,0.5)) = 0.12
        _FacetStrength ("Foil Strength", Range(0,2)) = 0.3
        _FoilOpacity ("Foil Opacity", Range(0,1)) = 0.14
        _Iridescence ("Iridescence", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off ZWrite Off ZTest Always

        // RARE = FOIL only. A restrained, iridescent stamped band that never recolours the card.
        // Common has no treatment; Rare uses this foil band; Epic uses the prismatic crackle holo
        // (CardTierEpic.shader); Legendary keeps the strongest foil sweep plus dust.
        //
        // FILE HISTORY: this file was CardTierEpic.shader while declaring "Overlink/CardTier/Epic",
        // yet it is Rare.mat that references it - the two tier shaders were cross-named until
        // 2026-10-26. Renamed to CardTierRare.shader and re-declared to match, with the .meta (and
        // therefore guid 4950968b847b94745b48169a9e5922d1) moved alongside so Rare.mat's reference
        // survived untouched. See docs/CARD-FRAME-SPEC.md section 13.
        //
        // Neutral by construction: _Iridescence defaults to 0, so the band is white light. Raising it
        // pulls hue from the thin-film palette, never from a pigment, so the card's own colours stay
        // the image. The band MODULATES the base where it overlaps frame ink and adds light only at
        // its core; alpha lifts only where that light lands. See docs/CARD-FRAME-SPEC.md section 13.
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
            float _EmissionStrength;
            float _EffectEnabled;
            float _GlowThreshold;
            float _FacetWidth;
            float _FacetStrength;
            float _FoilOpacity;
            float _Iridescence;
            CBUFFER_END

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            static half3 ThinFilm(float t)
            {
                const half3 a = half3(0.62, 0.64, 0.68);
                const half3 b = half3(0.38, 0.34, 0.42);
                const half3 c = half3(1.00, 1.00, 1.00);
                const half3 d = half3(0.00, 0.30, 0.62);
                return a + b * cos(6.2831853 * (c * t + d));
            }

            half4 frag(Varyings i) : SV_Target
            {
                half4 base = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;
                float a = base.a;
                float on = saturate(_EffectEnabled) * saturate(_Intensity);
                if (on <= 0.0) return base;

                float lum = dot(base.rgb, float3(0.2126, 0.7152, 0.0722));
                float t = _Time.y;
                float width = max(_FacetWidth, 0.001);

                // Primary band: soft shoulder, hot narrow core.
                float sweep = i.uv.x * 0.62 + i.uv.y * 1.14 - t * _Speed * 0.18;
                float d1 = abs(frac(sweep) - 0.5);
                float shoulder = 1.0 - smoothstep(0.0, width, d1);
                float core = 1.0 - smoothstep(0.0, width * 0.2, d1);

                // Secondary band on a crossing angle, much fainter, so the stamp reads as faceted.
                float sweepB = i.uv.x * 1.05 - i.uv.y * 0.48 - t * _Speed * 0.11;
                float crossing = (1.0 - smoothstep(0.0, width * 0.4, abs(frac(sweepB) - 0.5))) * 0.25;

                float band = saturate(shoulder * 0.45 + core * 0.55 + crossing);

                // The foil is stamped on the frame, so it mostly follows frame ink; a tenth of it
                // carries over the art window so the band does not visibly stop at the plate edges.
                float gate = 0.10 + 0.90 * a;
                float foilAmt = band * _FacetStrength * gate * on;

                // Rail glow: only the frame's own bright ink blooms.
                float glow = smoothstep(_GlowThreshold, 1.0, lum) * a;
                float glowAmt = glow * _EmissionStrength * on;

                half3 film = lerp(half3(1.0, 1.0, 1.0), ThinFilm(frac(sweep + core * 0.15)),
                    saturate(_Iridescence));
                half3 accent = _AccentColor.rgb * film;

                // The band brightens what is already there rather than replacing it: a multiplicative
                // lift under the shoulder, additive only at the hot core.
                float3 screen = base.rgb * a * (1.0 + 0.20 * shoulder * gate * on);
                screen += accent * (core * 0.6 + crossing) * _FacetStrength * on;
                screen += accent * glowAmt;
                screen += base.rgb * (0.14 * glowAmt) * a;

                float outA = saturate(a + foilAmt * _FoilOpacity + glowAmt * 0.2);
                float3 col = screen / max(outA, 0.0035);

                return half4(min(col, half3(4.0, 4.0, 4.0)), outA);
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
