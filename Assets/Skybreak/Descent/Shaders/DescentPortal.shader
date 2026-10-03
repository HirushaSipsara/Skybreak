Shader "Skybreak/Descent Portal"
{
    Properties { _Tint("Portal tint",Color) = (0.3,0.08,0.65,1) }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            float4 _Tint;
            Varyings Vert(Attributes input) { Varyings o; o.positionCS=TransformObjectToHClip(input.positionOS.xyz);o.uv=input.uv;return o; }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=(input.uv-.5)*2;float r=length(p);float a=atan2(p.y,p.x);
                float wave=.5+.5*sin(a*6+r*24-_Time.y*2.4);
                float ring=pow(saturate(r),5);float spark=pow(wave,12)*.6;
                float3 color=_Tint.rgb*(.5+wave*.7)+float3(.3,.18,.8)*(ring*3+spark);
                color+=float3(.05,.5,.7)*pow(1-saturate(r),3)*(.6+.2*sin(_Time.y));
                return half4(color,1-smoothstep(.94,1,r));
            }
            ENDHLSL
        }
    }
}
