// Wind sway for the World 1 cornfield strips (2026-11-09). An unlit sprite shader that bends the TOP of the art
// sideways in a travelling wave while the bottom stays planted: the texture lookup is shifted along x by
//   wind * sway(world x, time) * (height in the sprite)^2
// The shift is computed in WORLD units and converted to UV with the screen-space derivative of uv.x, so a tile
// drawn mirrored (SpriteRenderer.flipX, used by EnvironmentDepthSystem's tiled strips) sways the same way as its
// neighbour and the seams between tiles stay closed. _FFWind is a global set every frame by CornfieldAmbience
// (gusts, stronger in rain); _SwayAmount is per material (how far the tops travel, world units at full wind).
Shader "FarmFury/WindSway"
{
    Properties
    {
        [PerRendererData] _MainTex ("Texture", 2D) = "white" {}
        _SwayAmount ("Sway (world units at full wind)", Float) = 0.12
        _SwayFrequency ("Wave length factor (per world unit)", Float) = 0.55
        _SwaySpeed ("Wave speed", Float) = 1.6
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
                float  worldX      : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float _SwayAmount;
                float _SwayFrequency;
                float _SwaySpeed;
            CBUFFER_END

            float _FFWind;   // global, 0 = still air, 1 = normal breeze, 2 = storm

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                float3 world = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.positionHCS = TransformWorldToHClip(world);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                OUT.worldX = world.x;
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                float t = _Time.y * _SwaySpeed;
                float x = IN.worldX * _SwayFrequency;
                // Two waves for an irregular, gusty look; the slow one leans the whole field, the fast one ripples.
                float wave = 0.65 * sin(x - t) + 0.35 * sin(x * 2.7 - t * 2.3 + 1.3);
                float lean = 0.5;   // the wind blows one way: the tops lean downwind, then wave around that
                float bend = IN.uv.y * IN.uv.y;
                float shiftWorld = _FFWind * _SwayAmount * (lean + wave) * bend;

                // world -> uv along x (sign follows flipX), from screen-space derivatives
                float duvdx = ddx(IN.uv.x), dwdx = ddx(IN.worldX);
                float uvPerWorld = abs(dwdx) > 1e-6 ? duvdx / dwdx : 0;
                float2 uv = IN.uv;
                uv.x -= shiftWorld * uvPerWorld;

                // Pulled past the tile's edge: the neighbouring tile is this one mirrored (strips alternate flipX), so
                // mirror the lookup — that IS the neighbour's art and the seam stays continuous.
                uv.x = abs(uv.x);
                uv.x = uv.x > 1.0 ? 2.0 - uv.x : uv.x;
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv) * IN.color;
            }
            ENDHLSL
        }
    }
}
