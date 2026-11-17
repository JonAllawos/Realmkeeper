// Transparent URP water for the lake plane and river ribbon. Flat colour with alpha,
// gently animated normals for a soft sun glint, main-light shadows and scene fog.
// Vertex colour alpha scales opacity (the river ribbon fades at its banks).
Shader "Realmkeeper/Water"
{
    Properties
    {
        _ShallowColor ("Colour", Color) = (0.20, 0.34, 0.42, 0.78)
        _GlintColor ("Glint", Color) = (1.0, 0.94, 0.82, 1)
        _GlintPower ("Glint Power", Range(8, 512)) = 160
        _RippleScale ("Ripple Scale", Float) = 0.35
        _RippleStrength ("Ripple Strength", Range(0, 0.3)) = 0.06
        _SkyReflection ("Sky Reflection", Range(0, 1)) = 0.75
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _ShallowColor;
            half4 _GlintColor;
            half _GlintPower;
            float _RippleScale;
            half _RippleStrength;
            half _SkyReflection;
        CBUFFER_END

        // Orthographic-safe linear fog (see VertexColorLit.shader).
        // Visual pass (2026-11-16): exponential fog too (towns and the country); see TerrainGround.shader.
        float RkFogFactor(float3 positionWS)
        {
        #if defined(FOG_LINEAR) || defined(FOG_EXP) || defined(FOG_EXP2)
            float3 forward = -UNITY_MATRIX_V[2].xyz;
            float depth = dot(positionWS - _WorldSpaceCameraPos, forward);
            #if defined(FOG_LINEAR)
            return saturate(depth * unity_FogParams.z + unity_FogParams.w);
            #else
            return max(depth, 0.0);
            #endif
        #else
            return 1;
        #endif
        }

        half3 RkMixFog(half3 color, float fogFactor)
        {
        #if defined(FOG_LINEAR)
            return lerp(unity_FogColor.rgb, color, fogFactor);
        #elif defined(FOG_EXP)
            return lerp(unity_FogColor.rgb, color, saturate(exp2(-fogFactor * unity_FogParams.y)));
        #elif defined(FOG_EXP2)
            float d = fogFactor * unity_FogParams.x;
            return lerp(unity_FogColor.rgb, color, saturate(exp2(-d * d)));
        #else
            return color;
        #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

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
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float fogFactor : TEXCOORD1;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.fogFactor = RkFogFactor(positions.positionWS);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float t = _Time.y;
                float2 p = input.positionWS.xz * _RippleScale;
                half2 ripple = half2(
                    sin(p.x * 1.7 + t * 0.9) + sin(p.y * 2.3 - t * 0.7) * 0.5,
                    cos(p.y * 1.9 + t * 0.8) + cos(p.x * 2.1 + t * 0.6) * 0.5);
                half3 normalWS = normalize(half3(ripple.x * _RippleStrength, 1, ripple.y * _RippleStrength));

                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                Light mainLight = GetMainLight(shadowCoord);
                half shadow = lerp(0.55, 1.0, mainLight.shadowAttenuation);

                half3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half3 halfDir = normalize(mainLight.direction + viewDirWS);
                half glint = pow(saturate(dot(normalWS, halfDir)), _GlintPower) * shadow;

                half3 ambient = max(half3(0, 0, 0), SampleSH(half3(0, 1, 0)));
                half nDotL = saturate(dot(normalWS, mainLight.direction));
                half3 color = _ShallowColor.rgb * (ambient + mainLight.color * nDotL * shadow);
                // Visual pass (2026-11-16): the sky in the water at grazing angles (the ambient probe holds the sky's colour),
                // so lakes seen across the land read as water rather than flat dark navy. Nothing extra is sampled.
                half fresnel = pow(1.0 - saturate(dot(normalWS, viewDirWS)), 4.0) * _SkyReflection;
                half3 sky = max(half3(0, 0, 0), SampleSH(reflect(-viewDirWS, normalWS))) * 1.15;
                color = lerp(color, sky, fresnel);
                color += _GlintColor.rgb * mainLight.color * glint;
                color = RkMixFog(color, RkFogFactor(input.positionWS)); // per pixel: the water is one quad whose corners can lie behind the camera

                half alpha = saturate(_ShallowColor.a * input.color.a + glint * 0.5 + fresnel * 0.4);
                return half4(max(half3(0, 0, 0), color), alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
