// Phase 29 (ADR-0122): the overland ground drawn as a Unity Terrain, with Realmkeeper's own look. The same lighting, fog,
// ground grain and ground weather as Realmkeeper/VertexColorLit, but the biome colour and the dirt and rock weights come from
// two textures over the region (one texel per 5 m, from the game's own terrain) instead of vertex colours, which a Unity
// Terrain does not carry. Drawn with Terrain.drawInstanced off, so the terrain hands it ordinary meshes (position, normal).
Shader "Realmkeeper/TerrainGround"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 1
        _BiomeColour ("Biome Colour (RGB)", 2D) = "grey" {}
        _GroundWeights ("Ground Weights (R dirt, G rock)", 2D) = "black" {}
        _Region ("Region (origin x, z; size x, z)", Vector) = (0, 0, 5120, 5120)
        _TexStrength ("Ground Texture Strength", Range(0, 1)) = 0.85
        _TexScale ("Ground Texture Tiles per Metre", Float) = 0.25
        _GrassTex ("Grass", 2D) = "grey" {}
        _DirtTex ("Dirt", 2D) = "grey" {}
        _RockTex ("Rock", 2D) = "grey" {}
        _Wet ("Ground Wetness", Range(0, 1)) = 0
        _Snow ("Ground Snow", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry-100" "TerrainCompatible" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _AmbientStrength;
            float4 _Region;
            half _TexStrength;
            float _TexScale;
            half _Wet;
            half _Snow;
        CBUFFER_END

        TEXTURE2D(_BiomeColour); SAMPLER(sampler_BiomeColour);
        TEXTURE2D(_GroundWeights); SAMPLER(sampler_GroundWeights);
        TEXTURE2D(_GrassTex); SAMPLER(sampler_GrassTex);
        TEXTURE2D(_DirtTex); SAMPLER(sampler_DirtTex);
        TEXTURE2D(_RockTex); SAMPLER(sampler_RockTex);

        // Linear fog by view depth, as Realmkeeper/VertexColorLit (the camera is orthographic).
        half RkFogFactor(float3 positionWS)
        {
        #if defined(FOG_LINEAR)
            float3 forward = -UNITY_MATRIX_V[2].xyz;
            float depth = dot(positionWS - _WorldSpaceCameraPos, forward);
            return saturate(depth * unity_FogParams.z + unity_FogParams.w);
        #else
            return 1;
        #endif
        }

        half3 RkMixFog(half3 color, half fogFactor)
        {
        #if defined(FOG_LINEAR)
            return lerp(unity_FogColor.rgb, color, fogFactor);
        #else
            return color;
        #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                float2 map : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = RkFogFactor(positions.positionWS);
                // The terrain's own space runs 0..size over the region, wherever the overland is placed in the world.
                output.map = (input.positionOS.xz - _Region.xy) / max(_Region.zw, float2(1, 1));
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half3 normalWS = SafeNormalize(input.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half nDotL = saturate(dot(normalWS, mainLight.direction));
                // The main light's distanceAttenuation is URP's per-renderer culling flag (unity_LightData.z), which a Unity
                // Terrain drawn through a custom shader receives as 0: the ground got no sunlight at all (Phase 29 parity
                // measurement: lit == ambient only). The sun is directional and never culled from the ground, so only its
                // shadow attenuation applies here.
                half3 direct = mainLight.color * (nDotL * mainLight.shadowAttenuation);
                half3 ambient = max(half3(0, 0, 0), SampleSH(normalWS)) * _AmbientStrength;

                float2 map = input.map;
                half3 albedo = max(half3(0, 0, 0), SAMPLE_TEXTURE2D(_BiomeColour, sampler_BiomeColour, map).rgb * _BaseColor.rgb);
                half2 weights = SAMPLE_TEXTURE2D(_GroundWeights, sampler_GroundWeights, map).rg;
                if (_TexStrength > 0)
                {
                    float2 uv = input.positionWS.xz * _TexScale;
                    float2 uvFar = uv * 0.137;
                    half3 g = SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, uv).rgb * 0.7 + SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, uvFar).rgb * 0.3;
                    half3 d = SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, uv).rgb * 0.7 + SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, uvFar).rgb * 0.3;
                    half3 r = SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, uv * 0.5).rgb * 0.7 + SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, uvFar).rgb * 0.3;
                    g /= max(SAMPLE_TEXTURE2D_LOD(_GrassTex, sampler_GrassTex, float2(0.5, 0.5), 12).rgb, 0.05);
                    d /= max(SAMPLE_TEXTURE2D_LOD(_DirtTex, sampler_DirtTex, float2(0.5, 0.5), 12).rgb, 0.05);
                    r /= max(SAMPLE_TEXTURE2D_LOD(_RockTex, sampler_RockTex, float2(0.5, 0.5), 12).rgb, 0.05);
                    half3 detail = lerp(lerp(g, d, saturate(weights.x)), r, saturate(weights.y));
                    albedo *= lerp(half3(1, 1, 1), clamp(detail, 0.35, 1.8), _TexStrength);
                    albedo *= 1.0 - 0.38 * _Wet;
                    half level = saturate((normalWS.y - 0.55) * 3.0);
                    albedo = lerp(albedo, half3(0.86, 0.88, 0.92), _Snow * level * (0.75 + 0.25 * saturate(g.r)));
                }

                half3 color = albedo * (direct + ambient);
                color = RkMixFog(color, input.fogFactor);
                return half4(max(half3(0, 0, 0), color), 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 ShadowVert(ShadowAttributes input) : SV_POSITION
            {
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
                float3 lightDirectionWS = normalize(_LightPosition - positionWS);
            #else
                float3 lightDirectionWS = _LightDirection;
            #endif
                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
            #if UNITY_REVERSED_Z
                positionCS.z = min(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                positionCS.z = max(positionCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                return positionCS;
            }

            half4 ShadowFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVert
            #pragma fragment DepthFrag

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
            };

            float4 DepthVert(DepthAttributes input) : SV_POSITION
            {
                return TransformObjectToHClip(input.positionOS.xyz);
            }

            half DepthFrag() : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVert
            #pragma fragment DepthNormalsFrag

            struct DepthNormalsAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
            };

            DepthNormalsVaryings DepthNormalsVert(DepthNormalsAttributes input)
            {
                DepthNormalsVaryings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 DepthNormalsFrag(DepthNormalsVaryings input) : SV_Target
            {
                return half4(NormalizeNormalPerPixel(input.normalWS), 0);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
