#ifndef SLIMEBREAKER_COMMON_INCLUDED
#define SLIMEBREAKER_COMMON_INCLUDED

// Shared helpers for the Slime Breaker URP shaders (wind sway, shadow-caster positioning).

float3 _LightDirection;
float3 _LightPosition;

// Gentle global wind. `weight` comes from the vertex colour alpha (0 at the base, 1 at tips).
float3 SB_ApplyWind(float3 positionWS, float3 normalWS, float weight, float strength, float speed, float flutter)
{
    float t = _Time.y * speed;
    float phase = dot(positionWS.xz, float2(0.13, 0.21));
    float2 dir = normalize(float2(1.0, 0.35));
    float gust = sin(t + phase) * 0.6 + sin(t * 2.3 + phase * 1.7) * 0.3 + 0.35;
    float leaf = sin(t * 7.0 + dot(positionWS, float3(3.1, 2.7, 4.3))) * flutter;
    positionWS.xz += dir * gust * strength * weight;
    positionWS.y -= abs(gust) * strength * weight * 0.25;
    positionWS += normalWS * leaf * weight * 0.5;
    return positionWS;
}

float4 SB_ShadowPositionCS(float3 positionWS, float3 normalWS)
{
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif
    float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
    return ApplyShadowClamping(positionCS);
}

#endif
