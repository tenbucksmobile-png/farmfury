// Chroma key compositing shader for VideoPlayer output. Keys out a configurable colour
// (default #00B140) with a soft edge derived from _Tolerance, so a green-screen character
// video can be drawn over gameplay via a RawImage with the background invisible.
// Written as a UI-compatible Unlit shader (stencil/clip support) since the intended
// consumer is a full-screen RawImage under a Canvas, not a world-space quad.
Shader "FarmFury/ChromaKeyVideo"
{
    Properties
    {
        [PerRendererData] _MainTex ("Video Texture", 2D) = "white" {}
        _KeyColor ("Chroma Key Colour", Color) = (0, 0.6941177, 0.2509804, 1) // #00B140
        _Tolerance ("Tolerance", Range(0, 1)) = 0.3
        _Color ("Tint (RawImage color)", Color) = (1, 1, 1, 1)

        // Standard Unity UI clipping/stencil boilerplate so this still behaves correctly
        // under a RectMask2D/Mask ancestor — omitting it is the usual way a custom UI
        // shader silently ignores masking.
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        _ClipRect ("Clip Rect", Vector) = (-32767, -32767, 32767, 32767)
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

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

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
                float4 worldPos    : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            float4 _MainTex_ST;

            CBUFFER_START(UnityPerMaterial)
                half4 _KeyColor;
                half _Tolerance;
                half4 _Color;
                float4 _ClipRect;
            CBUFFER_END

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.worldPos = float4(TransformObjectToWorld(IN.positionOS.xyz), 1.0);
                OUT.positionHCS = TransformWorldToHClip(OUT.worldPos.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.color = IN.color * _Color;
                return OUT;
            }

            // Clips outside the RectMask2D-supplied rect (matches Unity's UI/Default behaviour).
            half UIClipRectFade(float2 worldPos, float4 clipRect)
            {
                float2 inside = step(clipRect.xy, worldPos) * step(worldPos, clipRect.zw);
                return inside.x * inside.y;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                // Distance between the sampled pixel and the key colour in RGB space,
                // normalised to [0,1] so it lines up with the exposed 0-1 tolerance range.
                half rgbDist = distance(col.rgb, _KeyColor.rgb) * 0.5773503; // 1 / sqrt(3)

                // Saturation gate: plain RGB-distance keying can't tell a desaturated dark pixel
                // (character outlines, shadow shading) from a mid-tone green — both sit numerically
                // "close" to the key colour in flat RGB space, so without this a character's own
                // dark shading was fading translucent right alongside the real greenscreen (looked
                // like the whole subject was a "ghost", not just the background being removed).
                // Only pixels at least half as saturated as the key colour are even eligible to be
                // keyed out; anything greyer than that is forced to a huge effective distance, i.e.
                // always fully opaque, regardless of how "close" its raw RGB happens to sit.
                half sampleSat = max(col.r, max(col.g, col.b)) - min(col.r, min(col.g, col.b));
                half keySat     = max(_KeyColor.r, max(_KeyColor.g, _KeyColor.b))
                                 - min(_KeyColor.r, min(_KeyColor.g, _KeyColor.b));
                half satRatio   = saturate(sampleSat / max(keySat * 0.5, 0.0001));

                // The saturation gate above protects any desaturated pixel equally, but a
                // desaturated pixel can mean two very different things: genuine dark shadow
                // shading on the character (must stay opaque) or a bright, washed-out anti-
                // aliasing/compression blend right on the character's silhouette edge, where its
                // true colour is partway between the character and the green screen (should still
                // be keyed, not force-protected — this was showing up as a bright "shiny lining"
                // traced around the whole character). Brightness tells them apart: real shadow
                // detail is dark, the edge-blend artifact is bright/near-white. Treating bright
                // pixels as if they were fully saturated (i.e. subject to the real distance check)
                // lets that edge fringe key out normally, while dark desaturated pixels keep full
                // protection exactly as before.
                half value        = max(col.r, max(col.g, col.b));
                half brightWashout = smoothstep(0.75, 0.95, value);
                half effSatRatio   = max(satRatio, brightWashout);
                half dist          = lerp(10.0, rgbDist, effSatRatio);

                // Soft edge proportional to tolerance itself — a hard cutoff at low tolerance
                // reads as jagged, so the transition band scales with how aggressive the key is.
                half edge = max(_Tolerance * 0.5, 0.02);
                half keyAlpha = smoothstep(_Tolerance, _Tolerance + edge, dist);

                half4 result = col * IN.color;
                result.a = col.a * keyAlpha * IN.color.a;
                result.a *= UIClipRectFade(IN.worldPos.xy, _ClipRect);
                return result;
            }
            ENDHLSL
        }
    }
}
