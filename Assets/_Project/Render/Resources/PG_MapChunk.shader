// Map chunks from a Texture2DArray. Alpha encodes surface detail (0 plain,
// .125 sand, .25 grass, .376 rock, .5 water, .627 floodplain, .753 snow, .878 river, 1 lava);
// final output stays opaque.
Shader "PixelGenesis/MapChunk"
{
    Properties
    {
        _MainTex ("Chunks", 2DArray) = "" {}
        _GroundDetail ("Ground detail", 2D) = "white" {}
        _GroundDetailStrength ("Ground detail strength", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Tags { "LightMode" = "Universal2D" }
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma require 2darray
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D_ARRAY(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_GroundDetail);
            SAMPLER(sampler_GroundDetail);
            half _GroundDetailStrength;
            half4 _EraTint; // global, set by the era system (Bölüm 2); white by default
            half4 _DayLight; // global, set by the render-only day/night cycle (Bölüm 11 G4); white by default

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 uv : TEXCOORD0; // xy = uv, z = array layer (chunk index)
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 uv : TEXCOORD0;
                float2 worldPos : TEXCOORD1;
            };

            Varyings Vert(Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(ws);
                o.uv = v.uv;
                o.worldPos = ws.xy;
                return o;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p, float seed)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(cell + seed);
                float b = Hash21(cell + float2(1.0, 0.0) + seed);
                float c = Hash21(cell + float2(0.0, 1.0) + seed);
                float d = Hash21(cell + float2(1.0, 1.0) + seed);
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half4 c = SAMPLE_TEXTURE2D_ARRAY(_MainTex, sampler_MainTex, i.uv.xy, i.uv.z);
                float t = _Time.y;

                float2 px = floor(i.worldPos); // effects stay on the tile grid (pixel art)
                if (c.a > 0.18 && c.a < 0.3)
                {
                    // Eight small pixel cells per tile form irregular, dense turf.
                    // Broad and medium value-noise layers make natural grass clumps;
                    // the fine layer keeps the surface readable at close zoom.
                    float footprint = max(fwidth(i.worldPos.x), fwidth(i.worldPos.y));
                    float detail = 1.0 - smoothstep(0.10, 0.24, footprint);
                    float2 cell = floor(i.worldPos * 8.0);
                    float broad = Hash21(floor(cell / 13.0) + 37.1);
                    float clump = Hash21(floor(cell / 3.0) + 11.7);
                    float grain = Hash21(cell + 19.7);
                    float field = ValueNoise(i.worldPos * 0.20, 61.4);
                    float patch = ValueNoise(i.worldPos * 0.72, 93.2);
                    float turf = (broad - 0.5) * 0.035 + (clump - 0.5) * 0.055
                        + (field - 0.5) * 0.09 + (patch - 0.5) * 0.045 + (grain - 0.5) * 0.022;
                    c.rgb *= 1.0 + turf * detail;
                    float fleck = step(0.94, grain) * step(0.52, clump);
                    c.rgb = lerp(c.rgb, saturate(c.rgb * float3(1.11, 1.14, 1.05)), fleck * detail * 0.32);

                    // A hand-painted, low-contrast material layer breaks up the
                    // procedural turf while keeping the biome's own hue in charge.
                    float2 materialUv = i.worldPos / 64.0;
                    half3 material = SAMPLE_TEXTURE2D(_GroundDetail, sampler_GroundDetail, materialUv).rgb;
                    half materialLight = dot(material, half3(0.299, 0.587, 0.114));
                    half materialVariation = (materialLight - 0.39) * _GroundDetailStrength * detail;
                    c.rgb *= 1.0 + materialVariation;
                    // Let sparse earth patches warm the turf instead of reducing the
                    // hand-painted material to brightness-only noise.
                    half soil = saturate((material.r - material.g * 0.92h) * 5.0h)
                        * saturate((material.g - material.b * 1.12h) * 4.0h);
                    float earthPatch = smoothstep(0.78, 0.93, field * 0.76 + patch * 0.24);
                    soil = max(soil, earthPatch * 0.30h);
                    c.rgb = lerp(c.rgb, c.rgb * half3(1.07h, 0.92h, 0.78h), soil * _GroundDetailStrength * detail * 0.36h);
                }
                else if (c.a > 0.08 && c.a < 0.18)
                {
                    float detail = 1.0 - smoothstep(0.16, 0.32, max(fwidth(i.worldPos.x), fwidth(i.worldPos.y)));
                    float2 cell = floor(i.worldPos * 4.0);
                    float dune = Hash21(floor(cell / 6.0) + 51.7);
                    float grain = Hash21(cell + 5.9);
                    c.rgb *= 1.0 + ((dune - 0.5) * 0.09 + (grain - 0.5) * 0.018) * detail;
                    c.rgb = lerp(c.rgb, saturate(c.rgb * 1.07), step(0.965, grain) * detail * 0.35);
                }
                else if (c.a > 0.34 && c.a < 0.43)
                {
                    // Broken, low-contrast stone seams add scale without turning mountains
                    // into a grid of identical outlines.
                    float detail = 1.0 - smoothstep(0.16, 0.32, max(fwidth(i.worldPos.x), fwidth(i.worldPos.y)));
                    float2 local = frac(i.worldPos);
                    float seed = Hash21(px + 27.4);
                    float2 origin = float2(0.18 + 0.48 * Hash21(px + 9.2), 0.24 + 0.42 * Hash21(px + 4.6));
                    float2 q = local - origin;
                    float seam = (1.0 - smoothstep(0.025, 0.075, abs(q.y - q.x * 0.35)))
                        * step(0.0, q.x) * step(q.x, 0.26) * step(0.35, seed) * detail;
                    c.rgb = lerp(c.rgb, c.rgb * 0.78, seam * 0.55);
                }
                else if (c.a > 0.46 && c.a < 0.56)
                {
                    // Shallow/deep colors come from the map job. Small pixel clusters
                    // add surface motion without a bright checkerboard of glints.
                    float detail = 1.0 - smoothstep(0.16, 0.32, max(fwidth(i.worldPos.x), fwidth(i.worldPos.y)));
                    float2 cell = floor(i.worldPos * 4.0);
                    float patch = Hash21(floor(cell / 5.0) + 73.9);
                    float grain = Hash21(cell + 29.3);
                    c.rgb *= 1.0 + ((patch - 0.5) * 0.075 + (grain - 0.5) * 0.012) * detail;
                    c.rgb *= 1.0 + 0.025 * sin(t * 1.5 + px.x * 0.7 + px.y * 0.4);
                    float2 sparkleCell = floor(px / 8.0);
                    float slot = floor(t * 2.0);
                    float2 pick = floor(float2(Hash21(sparkleCell + slot), Hash21(sparkleCell + slot + 17.1)) * 8.0);
                    if (all(px - sparkleCell * 8.0 == pick) && Hash21(sparkleCell - slot) > 0.6)
                        c.rgb = lerp(c.rgb, 1.0, 0.25);

                    // Bölüm 11 görsel cilası (G3): the bright job-side foam band shimmers so the
                    // coastline visibly breathes. Bright pixels pulse twice a second.
                    float lum = dot(c.rgb, float3(0.299, 0.587, 0.114));
                    if (lum > 0.72)
                        c.rgb *= 1.0 + 0.10 * sin(t * 4.0 + px.x * 1.7 + px.y * 2.3);
                }
                else if (c.a > 0.60 && c.a < 0.68)
                {
                    // Damp, fertile banks get a denser moss pattern and sparse earthy seams.
                    float detail = 1.0 - smoothstep(0.12, 0.28, max(fwidth(i.worldPos.x), fwidth(i.worldPos.y)));
                    float2 cell = floor(i.worldPos * 6.0);
                    float moss = ValueNoise(i.worldPos * 0.34, 41.8);
                    float patch = Hash21(floor(cell / 4.0) + 37.6);
                    float grain = Hash21(cell + 11.3);
                    c.rgb *= 1.0 + ((moss - 0.5) * 0.13 + (patch - 0.5) * 0.055 + (grain - 0.5) * 0.025) * detail;
                    float soil = step(0.91, patch) * step(0.62, grain) * detail;
                    c.rgb = lerp(c.rgb, c.rgb * float3(1.08, 0.88, 0.68), soil * 0.22);
                }
                else if (c.a > 0.84 && c.a < 0.9)
                {
                    // Shallow channels get tiny, low-contrast moving highlights without changing walkability.
                    float detail = 1.0 - smoothstep(0.12, 0.28, max(fwidth(i.worldPos.x), fwidth(i.worldPos.y)));
                    float2 cell = floor(i.worldPos * 3.0);
                    float ripple = Hash21(cell + floor(t * 1.5));
                    c.rgb = lerp(c.rgb, saturate(c.rgb * float3(1.08, 1.14, 1.16)), step(0.79, ripple) * detail * 0.32);
                }
                else if (c.a > 0.72 && c.a < 0.80)
                {
                    // A few pale wind marks rather than animated glitter on snow.
                    float detail = 1.0 - smoothstep(0.16, 0.32, max(fwidth(i.worldPos.x), fwidth(i.worldPos.y)));
                    float2 local = frac(i.worldPos);
                    float2 origin = float2(0.14 + 0.58 * Hash21(px + 14.2), 0.24 + 0.52 * Hash21(px + 25.9));
                    float2 q = local - origin;
                    float drift = (1.0 - smoothstep(0.025, 0.075, abs(q.y + q.x * 0.12)))
                        * step(0.0, q.x) * step(q.x, 0.20) * step(0.64, Hash21(px + 7.7)) * detail;
                    c.rgb = lerp(c.rgb, saturate(c.rgb * 1.10), drift * 0.45);
                }
                else if (c.a > 0.9)
                {
                    // Lava: slow flicker and emission.
                    float f = 0.85 + 0.15 * sin(t * 2.3 + px.x * 0.35 + px.y * 0.5);
                    c.rgb = c.rgb * f * 1.25;
                }

                // Bölüm 11 (G4): the render-only day/night light, blended under the era tint.
                c.rgb *= _EraTint.rgb * _DayLight.rgb;

                c.a = 1.0;
                return c;
            }
            ENDHLSL
        }
    }
}
