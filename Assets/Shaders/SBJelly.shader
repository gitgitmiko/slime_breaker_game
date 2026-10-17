// Slime gel: wet PBR specular, wrap-lit subsurface scattering, bright fresnel rim where light passes through
// the thin edge, a denser darker core, and slowly drifting internal noise. _BaseColor / _EmissionColor stay
// compatible with EnemyHealth's tint and hit-flash property blocks.
Shader "SlimeBreaker/Jelly"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (0.3, 0.85, 0.5, 1)
        [HDR] _EmissionColor ("Emission (hit flash / status)", Color) = (0, 0, 0, 0)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.9
        _Wrap ("Subsurface wrap", Range(0, 1)) = 0.55
        _Subsurface ("Subsurface strength", Range(0, 2)) = 0.6
        _RimPower ("Rim power", Range(0.5, 8)) = 2.6
        _RimStrength ("Rim strength", Range(0, 3)) = 0.9
        _CoreDarken ("Core density", Range(0, 1)) = 0.5
        _GlowStrength ("Inner glow", Range(0, 2)) = 0.22
        _NoiseMap ("Inner noise", 2D) = "gray" {}
        _NoiseScale ("Noise scale", Float) = 1.6
        _NoiseSpeed ("Noise drift", Float) = 0.12
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" "UniversalMaterialType" = "Lit" }
        LOD 300

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _EmissionColor;
            half _Smoothness;
            half _Wrap;
            half _Subsurface;
            half _RimPower;
            half _RimStrength;
            half _CoreDarken;
            half _GlowStrength;
            float _NoiseScale;
            float _NoiseSpeed;
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);

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
                float3 positionOS : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionOS = input.positionOS.xyz;
                output.fogFactor = ComputeFogFactor(p.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 N = normalize(input.normalWS);
                float3 V = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half ndv = saturate(dot(N, V));
                half fresnel = pow(1.0 - ndv, _RimPower);

                // Drifting internal gel: two noise samples in object space (moves with the slime, not the world).
                float t = _Time.y * _NoiseSpeed;
                float3 o = input.positionOS * _NoiseScale;
                half n1 = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, o.xz + float2(t, t * 0.7)).g;
                half n2 = SAMPLE_TEXTURE2D(_NoiseMap, sampler_NoiseMap, o.xy * 1.3 - float2(t * 0.6, t)).b;
                half gel = saturate(n1 * 0.6 + n2 * 0.6 - 0.1);

                half3 base = _BaseColor.rgb;
                half3 core = base * lerp(1.0, 0.45, _CoreDarken);
                half3 albedo = lerp(core, base, saturate(1.0 - ndv * 0.8 + fresnel)) * lerp(0.85, 1.15, gel);

                SurfaceData surfaceData = (SurfaceData)0;
                surfaceData.albedo = albedo;
                surfaceData.alpha = 1;
                surfaceData.metallic = 0;
                surfaceData.smoothness = _Smoothness;
                surfaceData.occlusion = 1;
                surfaceData.normalTS = half3(0, 0, 1);

                InputData inputData = (InputData)0;
                inputData.positionWS = input.positionWS;
                inputData.positionCS = input.positionCS;
                inputData.normalWS = N;
                inputData.viewDirectionWS = V;
                inputData.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                inputData.fogCoord = input.fogFactor;
                inputData.bakedGI = SampleSH(N);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                // Subsurface: light wrapped round the terminator plus light shining through toward the viewer.
                Light mainLight = GetMainLight(inputData.shadowCoord);
                half wrap = saturate((dot(N, mainLight.direction) + _Wrap) / (1.0 + _Wrap));
                half through = pow(saturate(dot(V, -mainLight.direction)), 3.0) * (0.4 + fresnel);
                half shadow = lerp(0.35, 1.0, mainLight.shadowAttenuation);
                half3 sss = base * mainLight.color * (wrap * 0.35 + through) * _Subsurface * shadow;
                half3 rim = lerp(base, half3(1, 1, 1), 0.35) * fresnel * _RimStrength * (SampleSH(N) + mainLight.color * 0.3);
                surfaceData.emission = sss + rim + base * _GlowStrength * (0.6 + 0.8 * gel) + _EmissionColor.rgb;

                half4 color = UniversalFragmentPBR(inputData, surfaceData);
                color.rgb = MixFog(color.rgb, inputData.fogCoord);
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
