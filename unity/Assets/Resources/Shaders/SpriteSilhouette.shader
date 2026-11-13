// A sprite drawn as a soft, flat-coloured silhouette (2026-11-13): the golden rim behind an equipped cannon skin
// (CannonSkinGlow draws the cannon's own sprite with this, slightly enlarged, behind the cannon). The colour comes
// from the SpriteRenderer's colour; the texture only supplies the shape, blurred a little over _Blur (UV units) so the
// rim's edge is soft. Lives in Resources/ so Shader.Find works in builds.
Shader "FarmFury/SpriteSilhouette"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Blur ("Edge softness (UV)", Float) = 0.012
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "CanUseSpriteAtlas" = "True"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color       : COLOR;
                float2 uv          : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _Blur;
            CBUFFER_END

            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.uv = v.uv;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                // 13 taps: the centre, a ring at _Blur and a ring at half of it - the max gives a slightly grown,
                // soft-edged shape.
                float a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                [unroll] for (int k = 0; k < 6; k++)
                {
                    float ang = k * 1.0471976;   // 60 degrees
                    float2 d = float2(cos(ang), sin(ang)) * _Blur;
                    a = max(a, 0.85 * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + d).a);
                    a = max(a, SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv + d * 0.5).a);
                }
                return half4(i.color.rgb, a * i.color.a);
            }
            ENDHLSL
        }
    }
}
