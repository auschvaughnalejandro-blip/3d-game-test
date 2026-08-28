// A surface material that invents its own detail out of mathematics.
//
// Every rock and every patch of ground in this valley is a plain cube. A plain cube with
// a plain colour reads as a programmer's placeholder no matter how good the lighting is,
// because real surfaces are never one flat colour. This shader breaks that flatness up
// using noise generated from the world position of each pixel - no texture image, no
// painting, no unwrapping.
//
// It is triplanar, meaning it projects the pattern down all three axes and blends between
// them based on which way the surface faces. That matters here because these cubes are
// stretched to wildly different sizes, and ordinary texture coordinates would smear the
// pattern into streaks on the stretched faces.
Shader "OneValley/ProceduralRock"
{
    Properties
    {
        _BaseColor ("Base Colour", Color) = (0.35, 0.33, 0.31, 1)
        _SecondColor ("Second Colour", Color) = (0.22, 0.21, 0.20, 1)
        _CrackColor ("Crevice Colour", Color) = (0.10, 0.09, 0.09, 1)

        _NoiseScale ("Pattern Scale", Float) = 0.35
        _NoiseContrast ("Pattern Contrast", Range(0.2, 3)) = 1.2
        _CrackDepth ("Crevice Depth", Range(0, 1)) = 0.55

        _BumpStrength ("Surface Bumpiness", Range(0, 3)) = 1.1
        _Smoothness ("Smoothness", Range(0, 1)) = 0.08
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

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _SecondColor;
                float4 _CrackColor;
                float _NoiseScale;
                float _NoiseContrast;
                float _CrackDepth;
                float _BumpStrength;
                float _Smoothness;
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

            // A cheap repeatable pseudo-random number from a 3D grid point. Not truly
            // random, but it gives the same answer for the same point every frame, which
            // is what stops the pattern crawling as the camera moves.
            float HashToRandom(float3 gridPoint)
            {
                gridPoint = frac(gridPoint * 0.1031);
                gridPoint += dot(gridPoint, gridPoint.yzx + 33.33);
                return frac((gridPoint.x + gridPoint.y) * gridPoint.z);
            }

            // Smooth noise: pick random values at the eight corners of the cell this
            // point falls inside, then blend between them.
            float SmoothNoise(float3 samplePoint)
            {
                float3 cell = floor(samplePoint);
                float3 withinCell = frac(samplePoint);

                // Ease the blend so cell boundaries do not show as straight lines.
                withinCell = withinCell * withinCell * (3.0 - 2.0 * withinCell);

                float corner000 = HashToRandom(cell + float3(0, 0, 0));
                float corner100 = HashToRandom(cell + float3(1, 0, 0));
                float corner010 = HashToRandom(cell + float3(0, 1, 0));
                float corner110 = HashToRandom(cell + float3(1, 1, 0));
                float corner001 = HashToRandom(cell + float3(0, 0, 1));
                float corner101 = HashToRandom(cell + float3(1, 0, 1));
                float corner011 = HashToRandom(cell + float3(0, 1, 1));
                float corner111 = HashToRandom(cell + float3(1, 1, 1));

                float bottomFront = lerp(corner000, corner100, withinCell.x);
                float bottomBack = lerp(corner001, corner101, withinCell.x);
                float topFront = lerp(corner010, corner110, withinCell.x);
                float topBack = lerp(corner011, corner111, withinCell.x);

                float bottom = lerp(bottomFront, bottomBack, withinCell.z);
                float top = lerp(topFront, topBack, withinCell.z);

                return lerp(bottom, top, withinCell.y);
            }

            // Layered noise. Each layer is half the size and half the strength of the one
            // before, which is what gives a natural surface its mix of broad shapes and
            // fine grain.
            float LayeredNoise(float3 samplePoint)
            {
                float total = 0.0;
                float strength = 0.5;

                int layer = 0;
                while (layer < 4)
                {
                    total += SmoothNoise(samplePoint) * strength;
                    samplePoint = samplePoint * 2.03;
                    strength = strength * 0.5;
                    layer = layer + 1;
                }

                return total;
            }

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

            half4 FragmentStage(FragmentInput input) : SV_Target
            {
                float3 worldPosition = input.positionWS * _NoiseScale;

                float pattern = LayeredNoise(worldPosition);
                pattern = saturate((pattern - 0.5) * _NoiseContrast + 0.5);

                // A second, larger pattern picks out crevices. Where it dips near zero the
                // surface darkens, which reads as a crack or a shadowed hollow.
                float crevice = LayeredNoise(worldPosition * 0.45 + 31.7);
                float creviceMask = smoothstep(0.42, 0.52, crevice);

                float3 albedo = lerp(_SecondColor.rgb, _BaseColor.rgb, pattern);
                albedo = lerp(_CrackColor.rgb, albedo, lerp(1.0, creviceMask, _CrackDepth));

                // Bumpiness is faked by measuring how fast the pattern changes just to the
                // side of this pixel, and tilting the surface normal against that slope.
                // It costs three extra noise lookups and buys the illusion of relief.
                float stepSize = 0.35;
                float slopeAlongX = LayeredNoise(worldPosition + float3(stepSize, 0, 0)) - pattern;
                float slopeAlongY = LayeredNoise(worldPosition + float3(0, stepSize, 0)) - pattern;
                float slopeAlongZ = LayeredNoise(worldPosition + float3(0, 0, stepSize)) - pattern;

                float3 slope = float3(slopeAlongX, slopeAlongY, slopeAlongZ);
                float3 bumpedNormal = normalize(input.normalWS - slope * _BumpStrength);

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
                surface.smoothness = _Smoothness;
                surface.occlusion = lerp(0.65, 1.0, pattern);
                surface.alpha = 1.0;

                half4 finalColour = UniversalFragmentPBR(lightingInput, surface);
                finalColour.rgb = MixFog(finalColour.rgb, input.fogCoord);

                return finalColour;
            }
            ENDHLSL
        }

        // Shadow casting and depth are borrowed straight from URP's own Lit shader, so
        // this material still throws shadows and takes part in depth-based effects.
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }

    FallBack "Universal Render Pipeline/Lit"
}
