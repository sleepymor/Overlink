// Additive diagonal sheen sweep for UGUI overlays (button hover gloss, bar shimmer, panel wake).
// Authored for a child Image that covers its parent rect: the band travels on _Time so every
// instance sweeps in lockstep without per-object material properties, and the output is premade
// for Blend One One — rgb carries the energy, so no alpha math and no UGUI premultiplied trap.
// Branch-light by construction: two smoothsteps and a frac, nothing that diverges per pixel.
Shader "Overlink/UI/Sheen"
{
    Properties
    {
        // CanvasRenderer supplies a texture even for an untextured procedural Image.
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0.16, 0.87, 0.95, 1)
        _Speed ("Sweep Speed", Float) = 0.45
        _Width ("Band Width", Range(0.04, 0.6)) = 0.16
        _Intensity ("Intensity", Range(0, 2)) = 0.7
        _Angle ("Band Angle", Range(-1, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend One One
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            Name "UiSheen"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Speed;
                float _Width;
                float _Intensity;
                float _Angle;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                output.color = (half4)input.color;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Diagonal coordinate so the band reads as a gloss raked across the widget.
                float travel = input.uv.x + input.uv.y * _Angle - _Time.y * _Speed;
                float band = abs(frac(travel) - 0.5f);
                float shape = 1.0f - smoothstep(0.0f, _Width, band);

                // Fade at the rect edges so the sweep enters and leaves instead of popping.
                float edge = smoothstep(0.0f, 0.12f, input.uv.x) * (1.0f - smoothstep(0.88f, 1.0f, input.uv.x));

                // The Image's vertex alpha is the per-instance fade channel: the shared material
                // never changes, so UiFxButton can ramp one button's sheen without breaking batching.
                half energy = (half)(shape * edge * _Intensity) * input.color.a;
                return half4(_Color.rgb * energy, energy);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
