Shader "MotionVecDebug"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline"}
        LOD 100
        ZTest Always ZWrite Off Cull Off
        Pass
        {
            Name "MotionVectorDebugPass"

            HLSLPROGRAM
            #pragma multi_compile _ _USE_DRAW_PROCEDURAL
            #pragma vertex FullscreenVert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/Shaders/Utils/Fullscreen.hlsl"



            TEXTURE2D_X(_SourceTex);
            SAMPLER(sampler_SourceTex);
            TEXTURE2D(_MotionVectorTexture);
            SAMPLER(sampler_MotionVectorTexture);

            float4 _SourceTex_TexelSize;
            float _Intensity;


            half4 frag (Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 col = SAMPLE_TEXTURE2D_X(_SourceTex, sampler_SourceTex, input.uv);
                float4 vel = SAMPLE_TEXTURE2D_X(_MotionVectorTexture, sampler_MotionVectorTexture, input.uv);

                if (vel.x == 0 && vel.y == 0)
                    discard;

                col.xy = vel.xy * _Intensity;
                return col;
            }
            ENDHLSL
        }


    }
}
