Shader "Overlink/Backdrop"
{
    // The void behind the tabletop: deep gradient, sparse drifting stars, a faint grid horizon.
    // Value-locked by docs/ART-DIRECTION.md: everything stays under 0.2 luminance except the
    // star pin-points, so the board and HUD always out-read the background. Unlit, no depth
    // write concerns: the quad sits far behind the table and renders in the forward pass.
    Properties
    {
        _TopColor ("Top Color", Color) = (0.016, 0.020, 0.031, 1)
        _BottomColor ("Bottom Color", Color) = (0.043, 0.055, 0.075, 1)
        _GridColor ("Horizon Grid Color", Color) = (0.110, 0.439, 0.376, 1)
        _StarDensity ("Star Density", Range(0, 1)) = 0.35
        _Drift ("Drift Speed", Range(0, 1)) = 0.02
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Cull Off

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
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _TopColor;
                float4 _BottomColor;
                float4 _GridColor;
                float _StarDensity;
                float _Drift;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs vpi = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = vpi.positionCS;
                o.uv = input.uv;
                return o;
            }

            float hash12(float2 p)
            {
                float3 p3 = frac(p.xyx * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.uv;

                // Vertical void gradient, slightly brighter toward the horizon (bottom third).
                float3 col = lerp(_BottomColor.rgb, _TopColor.rgb, smoothstep(0.25, 0.95, uv.y));

                // Sparse stars in the upper two thirds, slow twinkle.
                float2 sp = uv * float2(120.0, 70.0);
                sp.x += _Time.y * _Drift * 10.0;
                float2 cell = floor(sp);
                float2 f = frac(sp) - 0.5;
                float h = hash12(cell);
                float star = step(1.0 - _StarDensity * 0.06, h)
                           * (1.0 - smoothstep(0.02, 0.10, length(f - (hash12(cell + 3.3) - 0.5) * 0.6)));
                float twinkle = 0.6 + 0.4 * sin(_Time.y * (1.0 + h * 3.0) + h * 40.0);
                col += star * twinkle * 0.55 * smoothstep(0.30, 0.75, uv.y);

                // Faint perspective grid fading in from the bottom edge (the "floor" of the void).
                float horizon = (1.0 - smoothstep(0.0, 0.42, uv.y));
                float2 gp = float2(uv.x * 24.0, 6.0 / max(uv.y + 0.06, 0.06));
                float gx = abs(frac(gp.x) - 0.5);
                float gy = abs(frac(gp.y) - 0.5);
                float gl = 1.0 - smoothstep(0.02, 0.07, min(gx, gy));
                col += _GridColor.rgb * gl * horizon * 0.10;

                // Corner vignette so screen edges fall into the void.
                float2 vc = uv - 0.5;
                col *= 1.0 - dot(vc, vc) * 0.55;

                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
