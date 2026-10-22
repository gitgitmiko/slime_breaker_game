// Arena floor: world-space PBR with anti-tiling (second rotated sample blended by height and macro noise),
// macro colour variation, and optional pulsing emission (lava, crystal veins). SB_QUALITY_LOW drops the second sample.
Shader "SlimeBreaker/Ground"
{
    Properties
    {
        _BaseMap ("Albedo (A = height)", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal strength", Float) = 1
        _MaskMap ("Mask (R AO, G smoothness, B metallic)", 2D) = "white" {}
        _SmoothnessScale ("Smoothness scale", Range(0, 2)) = 1
        _OcclusionStrength ("Occlusion", Range(0, 1)) = 1
        _EmissionMap ("Emission", 2D) = "black" {}
        [HDR] _EmissionColor ("Emission colour", Color) = (0, 0, 0, 0)
        _EmissionPulse ("Emission pulse", Range(0, 1)) = 0
        _MacroMap ("Macro noise", 2D) = "gray" {}
        _TileMeters ("Tile size (m)", Float) = 6
        _MacroMeters ("Macro size (m)", Float) = 23
        _MacroStrength ("Macro variation", Range(0, 0.6)) = 0.22
        _AntiTile ("Anti-tiling blend", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Lit" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half _BumpScale;
            half _SmoothnessScale;
            half _OcclusionStrength;
            half4 _EmissionColor;
            half _EmissionPulse;
            float _TileMeters;
            float _MacroMeters;
            half _MacroStrength;
            half _AntiTile;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local_fragment _EMISSION
            #pragma multi_compile_fragment _ SB_QUALITY_LOW
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            TEXTURE2D(_MaskMap);
            TEXTURE2D(_EmissionMap);
            TEXTURE2D(_MacroMap);     SAMPLER(sampler_MacroMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                half fogFactor : TEXCOORD2;
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half3 vertexLight : TEXCOORD3;
            #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs n = GetVertexNormalInputs(input.normalOS);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = n.normalWS;
                output.fogFactor = ComputeFogFactor(p.positionCS.z);
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                output.vertexLight = VertexLighting(p.positionWS, n.normalWS);
            #endif
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 uv = input.positionWS.xz / _TileMeters;
                half3 macro = SAMPLE_TEXTURE2D(_MacroMap, sampler_MacroMap, input.positionWS.xz / _MacroMeters).rgb;

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv);
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, uv);
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BaseMap, uv), _BumpScale);
            #if defined(_EMISSION)
                half3 emit = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, uv).rgb;
            #endif

            #if !defined(SB_QUALITY_LOW)
                // Second, rotated (37°) and rescaled sample; blended by macro noise and relative height so seams follow the texture.
                const float2x2 rot = float2x2(0.7986, -0.6018, 0.6018, 0.7986);
                float2 uv2 = mul(rot, uv * 0.71) + float2(0.37, 0.61);
                half4 albedo2 = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv2);
                half4 mask2 = SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, uv2);
                half3 normal2 = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BaseMap, uv2), _BumpScale);
                normal2.xy = mul(transpose(rot), normal2.xy);
                half blend = saturate((macro.g - 0.5) * 3.0 + (albedo2.a - albedo.a) * 2.0 + 0.5) * _AntiTile;
                albedo = lerp(albedo, albedo2, blend);
                mask = lerp(mask, mask2, blend);
                normalTS = normalize(lerp(normalTS, normal2, blend));
                #if defined(_EMISSION)
                    emit = lerp(emit, SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, uv2).rgb, blend);
                #endif
            #endif

                half variation = lerp(1.0 - _MacroStrength, 1.0 + _MacroStrength, macro.r);
                half3 baseColor = albedo.rgb * _BaseColor.rgb * variation;
                baseColor *= lerp(half3(1.04, 1.0, 0.94), half3(0.95, 1.0, 1.05), macro.b);

                // World XZ mapping: tangent along +X, bitangent along +Z, orthonormalised against the mesh normal.
                float3 N = SafeNormalize(float3(input.normalWS));
                float3 T = SafeNormalize(float3(1, 0, 0) - N * N.x);
                float3 B = cross(T, N);
                float3 normalWS = SafeNormalize(T * normalTS.x + B * normalTS.y + N * normalTS.z);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = baseColor;
                surfaceData.alpha = 1;
                surfaceData.metallic = mask.b;
                surfaceData.smoothness = saturate(mask.g * _SmoothnessScale);
                surfaceData.occlusion = lerp(1.0, mask.r, _OcclusionStrength);
                surfaceData.normalTS = normalTS;
            #if defined(_EMISSION)
                half pulse = 1.0 + _EmissionPulse * sin(_Time.y * 1.7 + macro.b * 6.2831);
                surfaceData.emission = emit * _EmissionColor.rgb * pulse;
            #endif

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                inputData.vertexLighting = input.vertexLight;
            #endif
                inputData.bakedGI = SampleSH(normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
                // Last line of defence: no NaN/Inf/negative may reach bloom (max() drops NaN on GPUs).
                color.rgb = min(max(color.rgb, 0.0), 32.0);
                return color;
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

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "SBCommon.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionCS = SB_ShadowPositionCS(TransformObjectToWorld(input.positionOS.xyz), TransformObjectToWorldNormal(input.normalOS));
                return output;
            }

            half4 frag(Varyings input) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; };

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half frag(Varyings input) : SV_Target { return input.positionCS.z; }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
