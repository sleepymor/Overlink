Shader "Overlink/BoardXLink"
{
    // Node-to-Core X-link on the battle board. A thin unlit filament in the owner colour: _BaseColor is
    // set per renderer through the MaterialPropertyBlock in SetBodyColor, and _Emission lifts it above 1
    // so bloom picks it up. Soft across its width, so it reads as a filament rather than a bar.
    Properties
    {
        _BaseColor ("Owner Colour", Color) = (1, 1, 1, 1)
        _Emission ("Emission", Range(0, 6)) = 2.0
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
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Emission;
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
                float across = 1.0 - abs(input.uv.y * 2.0 - 1.0);
                float core = smoothstep(0.0, 0.6, across);
                return float4(_BaseColor.rgb * _Emission * core, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
