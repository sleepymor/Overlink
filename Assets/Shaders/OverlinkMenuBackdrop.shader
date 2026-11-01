Shader "Overlink/MenuBackdrop"
{
    Properties
    {
        // UGUI's CanvasRenderer requires a texture slot on any material assigned to an Image. The
        // same shader is also used by the replaceable world-space backdrop quad, where this defaults white.
        _MainTex ("Texture", 2D) = "white" {}
        _TopColor ("Top", Color) = (0.004, 0.008, 0.016, 1)
        _BottomColor ("Bottom", Color) = (0.012, 0.024, 0.044, 1)
        _SignalColor ("Signal", Color) = (0.035, 0.26, 0.30, 1)
        _GridColor ("Grid", Color) = (0.025, 0.16, 0.20, 1)
        _Speed ("Signal Speed", Range(0, 1)) = 0.12
        _GridStrength ("Grid Strength", Range(0, 1)) = 0.48
        _SignalStrength ("Signal Strength", Range(0, 2)) = 0.82
        _Vignette ("Vignette", Range(0, 1)) = 0.46
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off ZWrite Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _TopColor; float4 _BottomColor; float4 _SignalColor; float4 _GridColor;
                float _Speed; float _GridStrength; float _SignalStrength; float _Vignette;
            CBUFFER_END
            V vert(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; o.color = i.color; return o; }
            float hash12(float2 p) { float3 p3=frac(float3(p.xyx)*0.1031); p3+=dot(p3,p3.yzx+33.33); return frac((p3.x+p3.y)*p3.z); }
            float4 frag(V i) : SV_Target
            {
                float2 uv=i.uv;
                float2 q=uv-0.5;
                q.x *= 1.777;
                float3 c=lerp(_BottomColor.rgb,_TopColor.rgb,smoothstep(0.02,0.98,uv.y));

                // A broad, quiet pool of signal behind the menu gives the void a focal depth cue.
                float pool=exp(-dot(q*float2(0.72,1.08),q*float2(0.72,1.08))*3.0);
                c += _SignalColor.rgb * pool * 0.075 * _SignalStrength;

                // Two scales of circuitry keep the backdrop from reading as a flat colour field.
                float2 p=uv*float2(30,17); p.x += _Time.y*_Speed;
                float2 fineP=uv*float2(74,42); fineP.x -= _Time.y*_Speed*0.35;
                float gridX=1-smoothstep(0.018,0.055,abs(frac(p.x)-0.5));
                float gridY=1-smoothstep(0.018,0.055,abs(frac(p.y)-0.5));
                float fineX=1-smoothstep(0.012,0.030,abs(frac(fineP.x)-0.5));
                float fineY=1-smoothstep(0.012,0.030,abs(frac(fineP.y)-0.5));
                float depthFade=smoothstep(0.06,0.92,uv.y) * (0.70 + pool*0.30);
                c += _GridColor.rgb * (gridX+gridY) * depthFade * _GridStrength;
                c += _SignalColor.rgb * (fineX+fineY) * depthFade * 0.12 * _GridStrength;

                // Moving data traces and small junctions add motion without competing with the UI column.
                float traceA=1-smoothstep(0.006,0.026,abs(frac((uv.x+uv.y*0.22)*13 + _Time.y*_Speed)-0.5));
                float traceB=1-smoothstep(0.006,0.026,abs(frac((uv.x-uv.y*0.18)*9 - _Time.y*_Speed*0.7)-0.5));
                c += _SignalColor.rgb * (traceA+traceB) * 0.035 * _SignalStrength;

                float2 cell=floor(p), f=frac(p)-0.5;
                float h=hash12(cell);
                float node=step(0.975,h)*(1-smoothstep(0.012,0.115,length(f)));
                c += _SignalColor.rgb * node * (0.45+0.40*sin(_Time.y*2+h*20)) * _SignalStrength;

                // Large, low-opacity rings read like distant network relays at the edges of the frame.
                float ringA=1-smoothstep(0.010,0.035,abs(length((uv-float2(0.16,0.72))*float2(1.55,1.0))-0.18));
                float ringB=1-smoothstep(0.010,0.035,abs(length((uv-float2(0.84,0.28))*float2(1.55,1.0))-0.24));
                c += _SignalColor.rgb * (ringA+ringB) * 0.075 * _SignalStrength;

                float edge=1-smoothstep(0.24,0.78,dot(q,q));
                c *= lerp(1.0,edge,_Vignette);
                float4 tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                return float4(c * tex.rgb * i.color.rgb, tex.a * i.color.a);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
