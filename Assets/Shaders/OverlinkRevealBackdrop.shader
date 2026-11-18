Shader "Overlink/RevealBackdrop"
{
    Properties
    {
        _TopColor ("Top", Color) = (0.010, 0.020, 0.036, 1)
        _BottomColor ("Bottom", Color) = (0.016, 0.034, 0.058, 1)
        _SignalColor ("Signal", Color) = (0.161, 0.875, 0.949, 1)
        _Speed ("Cycle Speed", Range(0, 1)) = 0.20
        _LineStrength ("Line Strength", Range(0, 1)) = 0.60
        _Vignette ("Vignette", Range(0, 1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Background" }
        Pass
        {
            Name "Unlit"
            Cull Off ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float4 _TopColor;
                float4 _BottomColor;
                float4 _SignalColor;
                float _Speed;
                float _LineStrength;
                float _Vignette;
            CBUFFER_END

            V vert(A i)
            {
                V o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }

            float hash12(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float2 nodeAt(float2 id)
            {
                return id + 0.5 + (float2(hash12(id), hash12(id + 17.3)) - 0.5) * 0.5;
            }

            // One trace per cycle: it grows from a to b, holds, fades out, then is drawn again.
            // The cycle phase is per trace, so the network never draws or clears in unison.
            float drawnTrace(float2 g, float2 a, float2 b, float seed)
            {
                float2 ab = b - a;
                float t = saturate(dot(g - a, ab) / dot(ab, ab));
                float d = length(g - a - ab * t);
                float stroke = 1.0 - smoothstep(0.012, 0.030, d);

                float cycle = frac(_Time.y * _Speed * (0.6 + seed) + seed * 7.31);
                float grow = smoothstep(0.0, 0.5, cycle);
                float fade = 1.0 - smoothstep(0.5, 1.0, cycle);
                float reach = 1.0 - smoothstep(grow - 0.02, grow, t);

                return stroke * reach * fade;
            }

            float4 frag(V i) : SV_Target
            {
                float2 uv = i.uv;
                float2 g = uv * float2(16.0, 9.0);
                float2 cell = floor(g);

                float lines = 0.0;
                float nodes = 0.0;

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 id = cell + float2(x, y);
                        float2 a = nodeAt(id);
                        float seed = hash12(id + 91.0);

                        if (step(0.40, hash12(id + 11.0)) > 0.5)
                            lines = max(lines, drawnTrace(g, a, nodeAt(id + float2(1, 0)), seed));
                        if (step(0.40, hash12(id + 23.0)) > 0.5)
                            lines = max(lines, drawnTrace(g, a, nodeAt(id + float2(0, 1)), seed + 0.33));
                        if (step(0.80, hash12(id + 37.0)) > 0.5)
                            lines = max(lines, drawnTrace(g, a, nodeAt(id + float2(1, 1)), seed + 0.66));

                        float core = 1.0 - smoothstep(0.012, 0.030, length(g - a));
                        nodes = max(nodes, core);
                    }
                }

                float2 q = (uv - 0.5) * float2(1.777, 1.0);
                float edgeWeight = smoothstep(0.12, 0.70, length(q));

                float3 c = lerp(_BottomColor.rgb, _TopColor.rgb, smoothstep(0.0, 1.0, uv.y));
                c += _SignalColor.rgb * lines * edgeWeight * _LineStrength * 0.45;
                c += _SignalColor.rgb * nodes * edgeWeight * _LineStrength * 0.20;

                float vignette = 1.0 - smoothstep(0.25, 0.95, dot(q, q));
                c *= lerp(1.0, vignette, _Vignette);
                return float4(c, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
