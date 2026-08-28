// Real photographic surfaces projected onto geometry that has no usable texture
// coordinates.
//
// Two problems make ordinary texturing useless in this valley. The terrain is generated
// in code and was never given UV coordinates at all. The cliffs are cubes stretched to
// 80 metres long, so any UVs they do have would smear the texture into streaks.
//
// Triplanar mapping solves both by ignoring UVs entirely. It projects the texture down
// all three world axes and blends between the three by how much the surface faces each
// one. Nothing needs unwrapping, and a stretched cube looks identical to an unstretched
// one because the projection lives in world space rather than on the model.
//
// Cost: three texture lookups per map instead of one. At this scale that is free.
Shader "OneValley/TriplanarPBR"
{
    Properties
    {
        _AlbedoMap ("Albedo", 2D) = "grey" {}
        _NormalMap ("Normal", 2D) = "bump" {}
        _RoughMap ("Roughness", 2D) = "grey" {}
        _OcclusionMap ("Ambient Occlusion", 2D) = "white" {}

        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Tiling ("World Tiling (metres per tile)", Float) = 0.12
        _BlendSharpness ("Blend Sharpness", Range(1, 12)) = 5
        _NormalStrength ("Normal Strength", Range(0, 3)) = 1.2
        _RoughnessScale ("Roughness Scale", Range(0, 2)) = 1
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 0.85
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex VertexStage
            #pragma fragment FragmentStage

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_AlbedoMap);      SAMPLER(sampler_AlbedoMap);
            TEXTURE2D(_NormalMap);      SAMPLER(sampler_NormalMap);
            TEXTURE2D(_RoughMap);       SAMPLER(sampler_RoughMap);
            TEXTURE2D(_OcclusionMap);   SAMPLER(sampler_OcclusionMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _Tiling;
                float _BlendSharpness;
                float _NormalStrength;
                float _RoughnessScale;
                float _OcclusionStrength;
            CBUFFER_END

            struct VertexInput
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct FragmentInput
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float fogCoord : TEXCOORD2;
            };

            FragmentInput VertexStage(VertexInput input)
            {
                FragmentInput output;

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normals = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.normalWS = normals.normalWS;
                output.fogCoord = ComputeFogFactor(positions.positionCS.z);

                return output;
            }

            // How much each of the three projections contributes, decided by which way
            // the surface faces. Raising to a power sharpens the transition so the three
            // projections do not visibly cross-fade over a wide band.
            float3 ProjectionWeights(float3 normalWS)
            {
                float3 weights = pow(abs(normalWS), _BlendSharpness);
                return weights / (weights.x + weights.y + weights.z);
            }

            float4 SampleTriplanar(TEXTURE2D_PARAM(chosenMap, chosenSampler),
                float3 worldPosition, float3 weights)
            {
                float2 coordsX = worldPosition.zy * _Tiling;
                float2 coordsY = worldPosition.xz * _Tiling;
                float2 coordsZ = worldPosition.xy * _Tiling;

                float4 alongX = SAMPLE_TEXTURE2D(chosenMap, chosenSampler, coordsX);
                float4 alongY = SAMPLE_TEXTURE2D(chosenMap, chosenSampler, coordsY);
                float4 alongZ = SAMPLE_TEXTURE2D(chosenMap, chosenSampler, coordsZ);

                return alongX * weights.x + alongY * weights.y + alongZ * weights.z;
            }

            // Normal maps cannot simply be averaged like colours - each projection stores
            // its bumps relative to a different plane. This is the standard "whiteout"
            // blend, which reorients each one against the real surface normal first.
            float3 SampleTriplanarNormal(float3 worldPosition, float3 surfaceNormal, float3 weights)
            {
                float2 coordsX = worldPosition.zy * _Tiling;
                float2 coordsY = worldPosition.xz * _Tiling;
                float2 coordsZ = worldPosition.xy * _Tiling;

                // These maps are imported as ordinary colour images, so the tangent-space
                // normal is decoded by hand rather than with UnpackNormal.
                float3 tangentX = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, coordsX).rgb * 2.0 - 1.0;
                float3 tangentY = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, coordsY).rgb * 2.0 - 1.0;
                float3 tangentZ = SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, coordsZ).rgb * 2.0 - 1.0;

                tangentX.xy *= _NormalStrength;
                tangentY.xy *= _NormalStrength;
                tangentZ.xy *= _NormalStrength;

                float3 alongX = float3(tangentX.xy + surfaceNormal.zy, abs(tangentX.z) * surfaceNormal.x);
                float3 alongY = float3(tangentY.xy + surfaceNormal.xz, abs(tangentY.z) * surfaceNormal.y);
                float3 alongZ = float3(tangentZ.xy + surfaceNormal.xy, abs(tangentZ.z) * surfaceNormal.z);

                return normalize(
                    alongX.zyx * weights.x +
                    alongY.xzy * weights.y +
                    alongZ.xyz * weights.z);
            }

            half4 FragmentStage(FragmentInput input) : SV_Target
            {
                float3 surfaceNormal = normalize(input.normalWS);
                float3 weights = ProjectionWeights(surfaceNormal);

                float3 albedo = SampleTriplanar(
                    TEXTURE2D_ARGS(_AlbedoMap, sampler_AlbedoMap),
                    input.positionWS, weights).rgb * _Tint.rgb;

                float roughness = SampleTriplanar(
                    TEXTURE2D_ARGS(_RoughMap, sampler_RoughMap),
                    input.positionWS, weights).r * _RoughnessScale;

                float occlusion = SampleTriplanar(
                    TEXTURE2D_ARGS(_OcclusionMap, sampler_OcclusionMap),
                    input.positionWS, weights).r;
                occlusion = lerp(1.0, occlusion, _OcclusionStrength);

                float3 bumpedNormal = SampleTriplanarNormal(input.positionWS, surfaceNormal, weights);

                InputData lightingInput = (InputData)0;
                lightingInput.positionWS = input.positionWS;
                lightingInput.normalWS = bumpedNormal;
                lightingInput.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lightingInput.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                lightingInput.fogCoord = input.fogCoord;
                lightingInput.bakedGI = SampleSH(bumpedNormal);

                SurfaceData surface = (SurfaceData)0;
                surface.albedo = albedo;
                surface.metallic = 0.0;
                // Roughness and smoothness are opposite ends of the same idea.
                surface.smoothness = saturate(1.0 - roughness);
                surface.occlusion = occlusion;
                surface.alpha = 1.0;

                half4 finalColour = UniversalFragmentPBR(lightingInput, surface);
                finalColour.rgb = MixFog(finalColour.rgb, input.fogCoord);

                return finalColour;
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    FallBack "Universal Render Pipeline/Lit"
}
