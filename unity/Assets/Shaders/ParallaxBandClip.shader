// Reveals only the bottom band of a full-frame opaque painting (UV.y in [0, _ClipAbove]),
// discarding everything above that line so whatever renders behind (a further-back parallax
// layer, or the true sky) shows through there. Used to stack several full-bleed background
// paintings — each one a complete scene with its own sky — like theatre flats: every layer
// fills the whole camera view, but only its own lower portion is actually visible, so nearer
// layers don't fully hide farther ones. See EnvironmentDepthSystem.cs.
Shader "FarmFury/ParallaxBandClip"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1, 1, 1, 1)
        _ClipAbove ("Visible Below (UV.y, local to this sprite's own texture)", Range(0, 1)) = 1
        _EdgeSoftness ("Edge Softness", Range(0, 0.2)) = 0.03
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
            float4 _MainTex_ST;

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _ClipAbove;
                half _EdgeSoftness;
            CBUFFER_END

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformWorldToHClip(TransformObjectToWorld(IN.positionOS.xyz));
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv) * IN.color;

                // Soft-edged reveal of the bottom [0, _ClipAbove] band of THIS sprite's own local
                // UV space (0 = bottom of the source painting, 1 = top) — deliberately independent
                // of the sprite's current world scale/position, so the same threshold means the
                // same fraction of the painting whether this layer is scaled for L01's 4.5 orthoSize
                // or L18's 8.0. _ClipAbove = 1 shows the whole painting uncropped.
                half fade = 1.0 - smoothstep(_ClipAbove - _EdgeSoftness, _ClipAbove, IN.uv.y);
                col.a *= fade;
                return col;
            }
            ENDHLSL
        }
    }
}
