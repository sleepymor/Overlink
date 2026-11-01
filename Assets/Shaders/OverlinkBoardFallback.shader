Shader "Overlink/BoardFallback"
{
    // Procedural stand-in for the five BattlefieldPresenter skin slots until supplied art lands.
    // CONTRACT (docs/ART-DIRECTION.md): every pattern outputs CHROMA-NEUTRAL grayscale in
    // 0.6-1.0 so the serialized albedo tints multiply cleanly through _BaseColor, exactly like
    // the URP/Lit materials the board ships with (SetBodyColor resolves _BaseColor first).
    // _Pattern is a uniform float (0 Table, 1 Socket, 2 Plate, 3 Pile, 4 Rim); uniform branching
    // is cheap because the whole draw takes one side.
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Pattern ("Pattern (0 Table 1 Socket 2 Plate 3 Pile 4 Rim)", Float) = 0
        _Scale ("Pattern Scale", Float) = 6
        _LineIntensity ("Inlay Brightness", Range(0, 2)) = 1.0
        _Emission ("Rim Emission Strength", Range(0, 8)) = 2.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            // SRPDefaultUnlit is drawn by both URP's 2D renderer and its forward renderer. The previous
            // UniversalForward-only pass was skipped entirely by Renderer2D, leaving only captions visible.
            Tags { "LightMode" = "SRPDefaultUnlit" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Pattern;
                float _Scale;
                float _LineIntensity;
                float _Emission;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs vpi = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = vpi.positionCS;
                o.positionWS = vpi.positionWS;
                o.uv = input.uv;
                return o;
            }

            float hash12(float2 p)
            {
                float3 p3 = frac(p.xyx * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            // Sparse circuit traces on a world-space XZ grid (table slab).
            float patternTable(float3 positionWS)
            {
                float2 p = positionWS.xz * _Scale;
                float2 cell = floor(p);
                float2 f = frac(p);
                float h = hash12(cell);
                float w = 0.035;
                // Half the cells carry a trace; alternate axis per cell hash.
                float axis = step(0.5, hash12(cell + 7.7));
                float lineMask = lerp(step(f.y, w), step(f.x, w), axis);
                float trace = lineMask * step(0.45, h);
                // Solder dots where a trace ends mid-cell.
                float dotm = step(0.86, h) * step(length(f - 0.5), 0.09);
                float g = 0.78 + (0.17 * trace + 0.22 * dotm) * _LineIntensity;
                return clamp(g, 0.0, 1.0);
            }

            // Recessed tile with bright corner brackets (per-face UV 0..1).
            float patternSocket(float2 uv)
            {
                float2 d = min(uv, 1.0 - uv);
                float edge = min(d.x, d.y);
                float recess = 0.86 - 0.16 * (1.0 - smoothstep(0.0, 0.18, edge));
                // Corner region: near two edges at once.
                float cornerRegion = step(d.x, 0.30) * step(d.y, 0.30);
                float bracket = step(edge, 0.045) * cornerRegion;
                float g = recess + bracket * 0.30 * _LineIntensity;
                return clamp(g, 0.0, 1.0);
            }

            // Triangular lattice inlay (reads as machined hex plate at board scale).
            float patternPlate(float2 uv)
            {
                float2 p = (uv - 0.5) * _Scale * 0.6;
                float a1 = abs(frac(p.x) - 0.5);
                float a2 = abs(frac(p.x * 0.5 + p.y * 0.8660254) - 0.5);
                float a3 = abs(frac(p.x * 0.5 - p.y * 0.8660254) - 0.5);
                float m = min(min(a1, a2), a3);
                float lineMask = 1.0 - smoothstep(0.02, 0.06, m);
                float g = 0.80 + lineMask * 0.18 * _LineIntensity;
                return clamp(g, 0.0, 1.0);
            }

            // Card back: diagonal weave plus a centre emblem ring.
            float patternPile(float2 uv)
            {
                float weave = sin((uv.x + uv.y) * 90.0) * sin((uv.x - uv.y) * 90.0);
                float g = 0.80 + weave * 0.04;
                float ring = abs(length(uv - 0.5) - 0.22);
                g += (1.0 - smoothstep(0.008, 0.02, ring)) * 0.20 * _LineIntensity;
                float2 d = min(uv, 1.0 - uv);
                g -= 0.10 * (1.0 - smoothstep(0.0, 0.06, min(d.x, d.y)));
                return clamp(g, 0.0, 1.0);
            }

            float4 frag(Varyings input) : SV_Target
            {
                float g;
                if (_Pattern < 0.5)      g = patternTable(input.positionWS);
                else if (_Pattern < 1.5) g = patternSocket(input.uv);
                else if (_Pattern < 2.5) g = patternPlate(input.uv);
                else if (_Pattern < 3.5) g = patternPile(input.uv);
                else                     return float4(_BaseColor.rgb * _Emission, 1.0);
                return float4(g * _BaseColor.rgb, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
