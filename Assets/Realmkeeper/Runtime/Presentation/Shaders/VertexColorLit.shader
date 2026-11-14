// Opaque URP shader for procedurally built Realmkeeper meshes (terrain, roads, buildings,
// trees). Albedo comes from vertex colours, lighting is Lambert from the main light with
// soft shadows, plus spherical-harmonic ambient and scene fog. Supports GPU instancing
// (forests use Graphics.RenderMeshInstanced) and is SRP Batcher compatible.
Shader "Realmkeeper/VertexColorLit"
{
    Properties
    {
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _AmbientStrength ("Ambient Strength", Range(0, 2)) = 1
        // Phase 23: ground textures in world space (grass, dirt, rock), weighted by uv1 (dirt, rock). Off (0) for every other mesh.
        _TexStrength ("Ground Texture Strength", Range(0, 1)) = 0
        _TexScale ("Ground Texture Tiles per Metre", Float) = 0.25
        _GrassTex ("Grass", 2D) = "grey" {}
        _DirtTex ("Dirt", 2D) = "grey" {}
        _RockTex ("Rock", 2D) = "grey" {}
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _AmbientStrength;
            half _TexStrength;
            float _TexScale;
        CBUFFER_END

        TEXTURE2D(_GrassTex); SAMPLER(sampler_GrassTex);
        TEXTURE2D(_DirtTex); SAMPLER(sampler_DirtTex);
        TEXTURE2D(_RockTex); SAMPLER(sampler_RockTex);

        // Linear fog by view depth. URP's ComputeFogFactor assumes a perspective
        // projection; Realmkeeper's camera is orthographic, so fog is computed here.
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
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                float2 weights : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                half4 color : COLOR;
                half2 weights : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fogFactor = RkFogFactor(positions.positionWS);
                output.color = input.color * _BaseColor;
                output.weights = input.weights;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half3 normalWS = SafeNormalize(input.normalWS);
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);

                half nDotL = saturate(dot(normalWS, mainLight.direction));
                half3 direct = mainLight.color * (nDotL * mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                // Clamp: a gradient SH probe can evaluate negative, and negative or NaN
                // HDR values are smeared over the whole screen by bloom.
                half3 ambient = max(half3(0, 0, 0), SampleSH(normalWS)) * _AmbientStrength;

                half3 albedo = max(half3(0, 0, 0), input.color.rgb);
                if (_TexStrength > 0)
                {
                    // Each texture divided by its own average (its smallest mip): the vertex colour keeps the biome's hue and
                    // brightness, the texture adds its grain. Two scales break up the tiling.
                    float2 uv = input.positionWS.xz * _TexScale;
                    float2 uvFar = uv * 0.137;
                    half3 g = SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, uv).rgb * 0.7 + SAMPLE_TEXTURE2D(_GrassTex, sampler_GrassTex, uvFar).rgb * 0.3;
                    half3 d = SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, uv).rgb * 0.7 + SAMPLE_TEXTURE2D(_DirtTex, sampler_DirtTex, uvFar).rgb * 0.3;
                    half3 r = SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, uv * 0.5).rgb * 0.7 + SAMPLE_TEXTURE2D(_RockTex, sampler_RockTex, uvFar).rgb * 0.3;
                    g /= max(SAMPLE_TEXTURE2D_LOD(_GrassTex, sampler_GrassTex, float2(0.5, 0.5), 12).rgb, 0.05);
                    d /= max(SAMPLE_TEXTURE2D_LOD(_DirtTex, sampler_DirtTex, float2(0.5, 0.5), 12).rgb, 0.05);
                    r /= max(SAMPLE_TEXTURE2D_LOD(_RockTex, sampler_RockTex, float2(0.5, 0.5), 12).rgb, 0.05);
                    half3 detail = lerp(lerp(g, d, saturate(input.weights.x)), r, saturate(input.weights.y));
                    albedo *= lerp(half3(1, 1, 1), clamp(detail, 0.35, 1.8), _TexStrength);
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
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 ShadowVert(ShadowAttributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
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
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float4 DepthVert(DepthAttributes input) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(input);
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
            #pragma multi_compile_instancing

            struct DepthNormalsAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthNormalsVaryings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
            };

            DepthNormalsVaryings DepthNormalsVert(DepthNormalsAttributes input)
            {
                DepthNormalsVaryings output;
                UNITY_SETUP_INSTANCE_ID(input);
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
