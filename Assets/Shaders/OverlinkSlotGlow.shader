Shader "Overlink/SlotGlow"
{
    // Event-driven glow for board slots, Node plates and the Core plate. Additive and black at rest
    // (Intensity 0), so a quad that is not being pulsed adds nothing to the frame. No texture, no noise,
    // no discard: the glow is the distance to the face edge (rim, weighted by _Rim) plus an optional
    // horizontal scan band (_Scan in 0..1, negative disables it).
    Properties
    {
        _GlowColor ("Glow Color", Color) = (0.25, 0.62, 1, 1)
        _Intensity ("Intensity", Range(0, 4)) = 0
        _Rim ("Rim Weight", Range(0, 1)) = 1
        _Scan ("Scan Position (-1 off)", Float) = -1
        _ScanWidth ("Scan Width", Range(0.01, 0.5)) = 0.12
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Blend One One
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
                float4 _GlowColor;
                float _Intensity;
                float _Rim;
                float _Scan;
                float _ScanWidth;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 d = min(input.uv, 1.0 - input.uv);
                float rim = 1.0 - smoothstep(0.0, 0.16, min(d.x, d.y));

                float scan = 0.0;
                if (_Scan >= 0.0)
                {
                    scan = 1.0 - smoothstep(0.0, _ScanWidth, abs(input.uv.y - _Scan));
                }

                float g = (rim * 0.85 * _Rim + scan * 0.6) * _Intensity;
                return float4(_GlowColor.rgb * g, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
