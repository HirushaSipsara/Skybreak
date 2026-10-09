Shader "Skybreak/Final Teleport Field"
{
    Properties
    {
        _Tint("Cyan energy", Color) = (0.08, 0.68, 1, 1)
        _Accent("Violet strands", Color) = (0.48, 0.15, 0.88, 1)
        _Energy("Energy", Range(0, 1.5)) = 0.8
        _Opacity("Transparency strength", Range(0, 1)) = 0.6
        _PulseOrigin("Crystal origin in field UV", Vector) = (0.5, 0.60, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent+5" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "TeleportEnergy"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float4 _Accent;
                float4 _PulseOrigin;
                float _Energy;
                float _Opacity;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                o.uv=input.uv;
                return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 aperture=(input.uv-.5)*2;
                float edge=length(aperture);
                float mask=1-smoothstep(.84,1,edge);
                float2 p=(input.uv-_PulseOrigin.xy)*2;
                float radius=length(p);
                float angle=atan2(p.y,p.x);
                float time=_Time.y;
                float drift=.016*sin(angle*5+time*.55);
                float phase=frac((radius+drift)*2.8-time*.22);
                float ripple=1-smoothstep(.012,.09,abs(phase-.5));
                float strand=pow(saturate(.5+.5*sin(angle*4+radius*10-time*.75)),15);
                float wisps=pow(saturate(.5+.5*sin(p.x*9-p.y*6+time*.5)),8);
                float rim=pow(saturate(edge),7);
                float breathing=.92+.08*sin(time*1.7);
                float3 tint=lerp(_Tint.rgb,_Accent.rgb,.22+.17*sin(angle*2+time*.3));
                float3 color=tint*(.6+1.5*ripple+.8*strand)+_Tint.rgb*rim;
                float alpha=(.026+.32*ripple+.10*strand+.028*wisps+.16*rim)*mask;
                alpha*=saturate(_Energy)*_Opacity*breathing;
                return half4(color,alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
