Shader "Overlink/BoardCrystal"
{
    // Crystal finish for the battle board, on top of the matte tactical base. Same CONTRACT as
    // Reimport marker: current source intentionally uses only positionWS/UV inputs.
    // OverlinkBoardFallback: the base is chroma-neutral grayscale in 0.6-1.0, multiplied by _BaseColor.
    // The crystal read is added on top and stays matte: an emissive edge glow in the owner colour (HDR
    // above 1 so bloom catches it), a fresnel-style sheen on rims, and faceted inlays. Surfaces keep
    // near-zero specular; nothing here is glass. See docs/ART-DIRECTION.md, "Board crystal finish".
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _Pattern ("Pattern (0 Table 1 Socket 2 Plate 3 Pile 4 Rim 5 Core)", Float) = 0
        _AccentColor ("Owner Accent", Color) = (0, 0, 0, 1)
        _ChevronSide ("Chevron Side", Float) = 1
        _Scale ("Pattern Scale", Float) = 6
        _LineIntensity ("Inlay Brightness", Range(0, 2)) = 1.0
        _Emission ("Edge Glow Strength", Range(0, 8)) = 2.0
        _Sheen ("Rim Sheen", Range(0, 1)) = 0.35
        _Chevrons ("Chevrons (0 off, 1 on)", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
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
                float4 _AccentColor;
                float _ChevronSide;
                float _Pattern;
                float _Scale;
                float _LineIntensity;
                float _Emission;
                float _Sheen;
                float _Chevrons;
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

            // Fine tactical grid plus sparse traces on the table slab (world XZ).
            float patternTable(float3 positionWS)
            {
                float2 p = positionWS.xz * _Scale;
                float2 f = frac(p);
                float2 cell = floor(p);
                float h = hash12(cell);
                float grid = step(f.x, 0.02) + step(f.y, 0.02);
                float trace = step(0.5, h) * step(abs(f.y - 0.5), 0.015);
                float g = 0.80 + (0.10 * saturate(grid) + 0.10 * trace) * _LineIntensity;
                return clamp(g, 0.0, 1.0);
            }

            // Recessed socket with corner registration ticks (per-face UV 0..1).
            float patternSocket(float2 uv)
            {
                float2 d = min(uv, 1.0 - uv);
                float edge = min(d.x, d.y);
                float recess = 0.84 - 0.14 * (1.0 - smoothstep(0.0, 0.18, edge));
                float cornerRegion = step(d.x, 0.30) * step(d.y, 0.30);
                float tick = step(edge, 0.04) * cornerRegion;
                float g = recess + tick * 0.28 * _LineIntensity;
                return clamp(g, 0.0, 1.0);
            }

            // Faceted plate: triangular lattice inlay plus a diagonal facet split.
            float patternPlate(float2 uv)
            {
                float2 p = (uv - 0.5) * _Scale * 0.6;
                float a1 = abs(frac(p.x) - 0.5);
                float a2 = abs(frac(p.x * 0.5 + p.y * 0.8660254) - 0.5);
                float a3 = abs(frac(p.x * 0.5 - p.y * 0.8660254) - 0.5);
                float m = min(min(a1, a2), a3);
                float lattice = 1.0 - smoothstep(0.02, 0.06, m);
                float facet = 1.0 - smoothstep(0.0, 0.01, abs(uv.x - uv.y));
                float g = 0.80 + (lattice * 0.14 + facet * 0.06) * _LineIntensity;
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

            // Three decorative slashes in the exposed side gutter, not a count or rank.
            float chevronMask(float2 uv)
            {
                float x = _ChevronSide < 0.0 ? 1.0 - uv.x : uv.x;
                float2 q = float2(x - 0.84, uv.y - 0.28);
                float slashes = abs(frac((q.x - q.y * 0.55) / 0.044 + 0.5) - 0.5);
                float aa = max(fwidth(slashes), 0.015);
                float inside = step(abs(q.x), 0.066) * step(abs(q.y), 0.022);
                return (1.0 - smoothstep(0.20 - aa, 0.20 + aa, slashes)) * inside;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float g;
                float2 d = min(input.uv, 1.0 - input.uv);
                float edge = min(d.x, d.y);

                if (_Pattern > 4.5)
                {
                    // Quiet, unfaceted Core inset with a crisp owner-coloured perimeter.
                    // The body remains real geometry for Core targeting; only its finish changes.
                    float aa = max(fwidth(edge), 0.0005);
                    float outline = 1.0 - smoothstep(0.004, 0.004 + aa, edge);
                    return float4(_BaseColor.rgb * 0.60 + _AccentColor.rgb * outline * _Emission, 1.0);
                }

                if (_Pattern < 0.5)      g = patternTable(input.positionWS);
                else if (_Pattern < 1.5) g = patternSocket(input.uv);
                else if (_Pattern < 2.5) g = patternPlate(input.uv);
                else if (_Pattern < 3.5) g = patternPile(input.uv);
                else
                {
                    // Rim: owner-coloured edge glow plus a fresnel-style sheen on the outer band.
                    float glow = 1.0 - smoothstep(0.0, 0.10, edge);
                    float sheen = (1.0 - smoothstep(0.0, 0.35, edge)) * _Sheen;
                    return float4(_BaseColor.rgb * (glow * _Emission + sheen), 1.0);
                }

                float3 baseCol = g * _BaseColor.rgb;

                // Crystal read on plates and sockets: a thin owner-coloured edge glow above 1.0 for bloom.
                float edgeGlow = (1.0 - smoothstep(0.0, 0.05, edge)) * _Emission * 0.5;
                float sheen = (1.0 - smoothstep(0.0, 0.25, edge)) * _Sheen * 0.25;
                float chev = _Chevrons > 0.5 ? chevronMask(input.uv) : 0.0;

                float3 col = baseCol + _BaseColor.rgb * (edgeGlow + sheen) + _AccentColor.rgb * chev * 1.4;
                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
