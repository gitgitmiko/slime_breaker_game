// General PBR for props and foliage: albedo, normal, packed mask (R AO, G smoothness, B metallic), emission,
// optional alpha clip (+ alpha-to-coverage), wind sway weighted by vertex-colour alpha, two-sided lighting,
// leaf translucency, and vertex-colour red as baked ambient occlusion.
Shader "SlimeBreaker/Lit"
{
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Colour", Color) = (1, 1, 1, 1)
        _BumpMap ("Normal", 2D) = "bump" {}
        _BumpScale ("Normal strength", Float) = 1
        _MaskMap ("Mask (R AO, G smoothness, B metallic)", 2D) = "white" {}
        _Smoothness ("Smoothness scale", Range(0, 2)) = 1
        _Metallic ("Metallic scale", Range(0, 1)) = 1
        _OcclusionStrength ("Occlusion", Range(0, 1)) = 1
        _EmissionMap ("Emission", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission colour", Color) = (0, 0, 0, 0)
        _Cutoff ("Alpha cutoff", Range(0, 1)) = 0.5
        _WindStrength ("Wind strength", Float) = 0
        _WindSpeed ("Wind speed", Float) = 1.3
        _Flutter ("Leaf flutter", Float) = 0
        _Translucency ("Translucency", Range(0, 2)) = 0
        _TranslucencyColor ("Translucency colour", Color) = (0.55, 0.8, 0.25, 1)
        _VertexAO ("Vertex colour AO", Range(0, 1)) = 0
        _RimStrength ("Rim light", Range(0, 2)) = 0
        _RimColor ("Rim colour", Color) = (1, 1, 1, 1)
        _CoverWeight ("Biome cover weight (moss/snow/sand on top faces)", Range(0, 1)) = 0
        _CoverSharpness ("Cover sharpness", Float) = 6
        _FlipBackNormals ("Flip normals on back faces", Float) = 1
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
        [Toggle] _AlphaToMask ("Alpha to coverage", Float) = 0
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
            half _Smoothness;
            half _Metallic;
            half _OcclusionStrength;
            half4 _EmissionColor;
            half _Cutoff;
            half _WindStrength;
            half _WindSpeed;
            half _Flutter;
            half _Translucency;
            half4 _TranslucencyColor;
            half _VertexAO;
            half _RimStrength;
            half4 _RimColor;
            half _CoverWeight;
            half _CoverSharpness;
            half _FlipBackNormals;
        CBUFFER_END

        // Biome cover, set globally by ArenaGenerator: RGB tint, A = cover smoothness.
        TEXTURE2D(_SB_CoverMap); SAMPLER(sampler_SB_CoverMap);
        half4 _SB_CoverColor;
        half _SB_CoverAmount;
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Cull [_Cull]
            AlphaToMask [_AlphaToMask]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _WIND
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _TRANSLUCENT
            #pragma shader_feature_local_fragment _COVER
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "SBCommon.hlsl"

            TEXTURE2D(_BaseMap);      SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);
            TEXTURE2D(_MaskMap);
            TEXTURE2D(_EmissionMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
                half4 tangentWS : TEXCOORD3;
                half4 color : TEXCOORD4;
                half fogFactor : TEXCOORD5;
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                half3 vertexLight : TEXCOORD6;
            #endif
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexNormalInputs n = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            #ifdef _WIND
                positionWS = SB_ApplyWind(positionWS, n.normalWS, input.color.a, _WindStrength, _WindSpeed, _Flutter);
            #endif
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.normalWS = n.normalWS;
                output.tangentWS = half4(n.tangentWS, input.tangentOS.w * GetOddNegativeScale());
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
            #ifdef _ADDITIONAL_LIGHTS_VERTEX
                output.vertexLight = VertexLighting(positionWS, n.normalWS);
            #endif
                return output;
            }

            half4 frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half4 albedoTex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                half4 albedo = albedoTex * _BaseColor;
            #if defined(_ALPHATEST_ON)
                clip(albedo.a - _Cutoff);
            #endif
                half4 mask = SAMPLE_TEXTURE2D(_MaskMap, sampler_BaseMap, input.uv);
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BaseMap, input.uv), _BumpScale);

                // Foliage cards carry authored up/outward normals; flipping them on the back face would light them from below.
                half sign = _FlipBackNormals > 0.5 ? IS_FRONT_VFACE(face, 1.0, -1.0) : 1.0;
            #if defined(_COVER)
                // Moss / snow / sand settles on upward faces; the albedo alpha (height) lets it fill crevices first.
                half amount = _SB_CoverAmount * _CoverWeight;
                half up = SafeNormalize(float3(input.normalWS)).y * sign;
                half cover = saturate((up - (1.0 - amount * 1.3)) * _CoverSharpness + (0.5 - albedoTex.a) * amount * 1.5);
                cover *= step(0.001, amount);
                half3 coverTex = SAMPLE_TEXTURE2D(_SB_CoverMap, sampler_SB_CoverMap, input.uv * 2.0).rgb;
                albedo.rgb = lerp(albedo.rgb, coverTex * _SB_CoverColor.rgb, cover);
                mask.g = lerp(mask.g, _SB_CoverColor.a, cover);
                mask.r = lerp(mask.r, 1.0, cover * 0.5);
                normalTS = normalize(lerp(normalTS, half3(0, 0, 1), cover * 0.6));
            #endif
                // Robust TBN: collapsed UVs (e.g. at the closed tip of a cactus arm) give zero tangents, and
                // normalize(0) = NaN, which bloom then smears into a big white/black disc on screen.
                float3 N = SafeNormalize(float3(input.normalWS)) * sign;
                float3 T = float3(input.tangentWS.xyz) - N * dot(N, float3(input.tangentWS.xyz));
                float tangentLength = dot(T, T);
                float3 fallbackT = abs(N.y) < 0.99 ? normalize(cross(float3(0, 1, 0), N)) : float3(1, 0, 0);
                T = tangentLength > 1e-6 ? T * rsqrt(tangentLength) : fallbackT;
                float3 B = cross(N, T) * (input.tangentWS.w < 0 ? -1.0 : 1.0) * sign;
                float3 normalWS = SafeNormalize(TransformTangentToWorld(normalTS, half3x3(T, B, N)));

                half vertexAO = lerp(1.0, input.color.r, _VertexAO);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo.rgb;
                surfaceData.alpha = 1;
                surfaceData.metallic = mask.b * _Metallic;
                surfaceData.smoothness = saturate(mask.g * _Smoothness);
                surfaceData.occlusion = lerp(1.0, mask.r, _OcclusionStrength) * vertexAO;
                surfaceData.normalTS = normalTS;
            #if defined(_EMISSION)
                surfaceData.emission = SAMPLE_TEXTURE2D(_EmissionMap, sampler_BaseMap, input.uv).rgb * _EmissionColor.rgb;
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
                inputData.bakedGI = SampleSH(normalWS) * vertexAO;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

            #if defined(_TRANSLUCENT)
                Light mainLight = GetMainLight(inputData.shadowCoord);
                half through = saturate(dot(-normalWS, mainLight.direction)) * 0.5
                    + pow(saturate(dot(inputData.viewDirectionWS, -mainLight.direction)), 3.0) * 0.7;
                surfaceData.emission += albedo.rgb * _TranslucencyColor.rgb * mainLight.color
                    * mainLight.shadowAttenuation * through * _Translucency;
            #endif
                if (_RimStrength > 0)
                {
                    half rim = pow(1.0 - saturate(dot(normalWS, inputData.viewDirectionWS)), 4.0);
                    surfaceData.emission += _RimColor.rgb * rim * _RimStrength;
                }

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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _WIND
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "SBCommon.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
            #ifdef _WIND
                positionWS = SB_ApplyWind(positionWS, normalWS, input.color.a, _WindStrength, _WindSpeed, _Flutter);
            #endif
                output.positionCS = SB_ShadowPositionCS(positionWS, normalWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
            #if defined(_ALPHATEST_ON)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
            #endif
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
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _ALPHATEST_ON
            #pragma shader_feature_local _WIND
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            #include "SBCommon.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            Varyings vert(Attributes input)
            {
                UNITY_SETUP_INSTANCE_ID(input);
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
            #ifdef _WIND
                positionWS = SB_ApplyWind(positionWS, TransformObjectToWorldNormal(input.normalOS), input.color.a, _WindStrength, _WindSpeed, _Flutter);
            #endif
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half frag(Varyings input) : SV_Target
            {
            #if defined(_ALPHATEST_ON)
                clip(SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).a * _BaseColor.a - _Cutoff);
            #endif
                return input.positionCS.z;
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
