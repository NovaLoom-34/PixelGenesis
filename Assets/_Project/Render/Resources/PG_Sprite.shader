// Atlas sprites on world-space quads (features, clouds). Vertex color tints; alpha below 0.5 is cut unless _Soft = 1.
// Bölüm 11 görsel cilası: _Sway=1 materials (features) bend with the wind; _SeasonTint colors foliage seasons.
Shader "PixelGenesis/Sprite"
{
    Properties
    {
        _MainTex ("Atlas", 2D) = "white" {}
        [Toggle] _ZWrite ("Depth write (depth-sorted sprites)", Float) = 0
        _Soft ("Soft alpha (clouds)", Float) = 0
        _Sway ("Wind sway", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            ZWrite [_ZWrite]
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            half4 _EraTint;   // global (Bölüm 2.12)
            half4 _DayLight;  // global (Bölüm 11 G4): terrain light, softened for readable silhouettes
            half4 _SeasonTint; // global (Bölüm 11 G5): seasonal foliage tint; white when seasons are off
            float2 _WindDir;   // global (Bölüm 11 G6): wind direction * speed from the sim

            CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float _Soft;
            float _Sway;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float3 featureData : TEXCOORD1;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                float2 worldPos : TEXCOORD1;
                float2 foliage : TEXCOORD2;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                if (_Sway > 0.5)
                {
                    // Crown vertices (uv.y above the roots) lean with the wind; the base stays planted.
                    float crown = saturate((v.featureData.z - 0.35f) / 0.65f);
                    float wave = sin(_Time.y * 1.6f + ws.x * 0.8f + ws.y * 0.5f) + 0.4f * sin(_Time.y * 3.1f + ws.x);
                    float amp = crown * crown * 0.16f;
                    ws.x += wave * amp * _WindDir.x * v.featureData.x;
                    ws.y += wave * amp * _WindDir.y * 0.3f * v.featureData.x;
                }
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = v.uv;
                o.color = v.color;
                o.worldPos = ws.xy;
                o.foliage = v.featureData.xy;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * i.color;
                if (_Soft < 0.5)
                {
                    clip(c.a - 0.5);
                    c.a = 1.0;
                }
                c.rgb *= _EraTint.rgb;
                if (_Sway > 0.5)
                {
                    c.rgb *= lerp(half3(1, 1, 1), _SeasonTint.rgb, i.foliage.y);
                    c.rgb *= lerp(half3(1, 1, 1), _DayLight.rgb, 0.68);
                }
                return c;
            }
            ENDHLSL
        }
    }
}
