Shader "Universal Render Pipeline/2D/Magical Sparkly Sprite"
{
    Properties
    {
        _MainTex("Diffuse", 2D) = "white" {}
        _MaskTex("Mask", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        [MaterialToggle] _ZWrite("ZWrite", Float) = 0

        _Color("Base Tint", Color) = (0.78, 0.92, 1, 1)
        _AuraColor("Aura Color", Color) = (0.35, 0.95, 1, 1)
        _SparkleColor("Sparkle Color", Color) = (1, 0.92, 0.48, 1)
        _TintStrength("Tint Strength", Range(0, 1)) = 0.35
        _ShimmerStrength("Shimmer Strength", Range(0, 2)) = 0.65
        _ShimmerSpeed("Shimmer Speed", Range(0, 10)) = 2.4
        _SparkleIntensity("Sparkle Intensity", Range(0, 4)) = 2.1
        _SparkleDensity("Sparkle Density", Range(4, 80)) = 34
        _SparkleCoverage("Sparkle Coverage", Range(0, 1)) = 0.42
        _SparkleSize("Sparkle Size", Range(0.005, 0.12)) = 0.035
        _SparkleSpeed("Sparkle Speed", Range(0, 10)) = 3.6

        // Legacy properties. They let sprite renderers and old materials keep their expected data.
        [HideInInspector] _RendererColor("RendererColor", Color) = (1,1,1,1)
        [HideInInspector] _AlphaTex("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }

        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite [_ZWrite]

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            half4 _AuraColor;
            half4 _SparkleColor;
            half _TintStrength;
            half _ShimmerStrength;
            half _ShimmerSpeed;
            half _SparkleIntensity;
            half _SparkleDensity;
            half _SparkleCoverage;
            half _SparkleSize;
            half _SparkleSpeed;
        CBUFFER_END

        float SparklyHash(float2 value)
        {
            return frac(sin(dot(value, float2(127.1, 311.7))) * 43758.5453123);
        }

        float2 SparklyHash2(float2 value)
        {
            return frac(sin(float2(
                dot(value, float2(269.5, 183.3)),
                dot(value, float2(419.2, 371.9)))) * 43758.5453123);
        }

        half3 ApplyMagicalSparkle(half3 albedo, half alpha, float2 uv)
        {
            float time = _Time.y;
            float density = max((float)_SparkleDensity, 1.0);
            float2 sparkleUv = uv * density;
            float2 cell = floor(sparkleUv);
            float2 localUv = frac(sparkleUv);
            float seed = SparklyHash(cell);
            float active = step(1.0 - saturate((float)_SparkleCoverage), seed);
            float2 center = SparklyHash2(cell + 17.0) * 0.7 + 0.15;
            float2 delta = localUv - center;

            float twinkleWave = sin(time * max((float)_SparkleSpeed, 0.001) + seed * 6.2831853);
            float twinkle = pow(saturate(twinkleWave * 0.5 + 0.5), 5.0);
            float sparkleSize = max((float)_SparkleSize, 0.001);
            float core = smoothstep(sparkleSize, 0.0, length(delta));
            float horizontalRay = smoothstep(sparkleSize * 5.5, 0.0, abs(delta.x)) *
                smoothstep(sparkleSize * 0.45, 0.0, abs(delta.y));
            float verticalRay = smoothstep(sparkleSize * 5.5, 0.0, abs(delta.y)) *
                smoothstep(sparkleSize * 0.45, 0.0, abs(delta.x));
            float star = active * twinkle * saturate(core + (horizontalRay + verticalRay) * 0.55);

            float ribbon = sin((uv.x * 17.0 + uv.y * 23.0) + time * (float)_ShimmerSpeed);
            float shimmer = pow(saturate(ribbon * 0.5 + 0.5), 6.0) * (float)_ShimmerStrength;
            float auraPulse = saturate(sin(time * 1.7 + uv.x * 13.0 - uv.y * 7.0) * 0.5 + 0.5);

            half3 tinted = lerp(albedo, albedo * _AuraColor.rgb + _AuraColor.rgb * 0.22, saturate(_TintStrength));
            half3 aura = _AuraColor.rgb * (shimmer * 0.55 + auraPulse * 0.12);
            half3 sparkle = _SparkleColor.rgb * star * _SparkleIntensity;
            return tinted + (aura + sparkle) * alpha;
        }
        ENDHLSL

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex LitVertex
            #pragma fragment LitFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/ShapeLightShared.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_LIT_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Lit2DCommon.hlsl"

            Varyings LitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings output = CommonLitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half4 LitFragment(Varyings input) : SV_Target
            {
                half4 main = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half4 mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, input.uv);
                half3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, input.uv));
                main.rgb = ApplyMagicalSparkle(main.rgb, main.a, input.uv);

                SurfaceData2D surfaceData;
                InputData2D inputData;

                InitializeSurfaceData(main.rgb, main.a, mask, normalTS, surfaceData);
                InitializeInputData(input.uv, input.lightingUV, inputData);

            #if defined(DEBUG_DISPLAY)
                SETUP_DEBUG_TEXTURE_DATA_2D_NO_TS(inputData, input.positionWS, input.positionCS, _MainTex);
                surfaceData.normalWS = input.normalWS;
            #endif

                return CombinedShapeLightShared(surfaceData, inputData);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "NormalsRendering" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex NormalsRenderingVertex
            #pragma fragment NormalsRenderingFragment

            #pragma multi_compile_instancing
            #pragma multi_compile _ SKINNED_SPRITE

            struct Attributes
            {
                COMMON_2D_NORMALS_INPUTS
                float4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_NORMALS_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Normals2DCommon.hlsl"

            Varyings NormalsRenderingVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings output = CommonNormalsVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half4 NormalsRenderingFragment(Varyings input) : SV_Target
            {
                return CommonNormalsFragment(input, input.color);
            }
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" "Queue" = "Transparent" "RenderType" = "Transparent" }

            HLSLPROGRAM
            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"

            #pragma vertex UnlitVertex
            #pragma fragment UnlitFragment

            struct Attributes
            {
                COMMON_2D_INPUTS
                half4 color : COLOR;
                UNITY_SKINNED_VERTEX_INPUTS
            };

            struct Varyings
            {
                COMMON_2D_OUTPUTS
                half4 color : COLOR;
            };

            #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY SKINNED_SPRITE

            Varyings UnlitVertex(Attributes input)
            {
                UNITY_SKINNED_VERTEX_COMPUTE(input);
                SetUpSpriteInstanceProperties();
                input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);

                Varyings output = CommonUnlitVertex(input);
                output.color = input.color * _Color * unity_SpriteColor;
                return output;
            }

            half4 UnlitFragment(Varyings input) : SV_Target
            {
                half4 main = input.color * SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                main.rgb = ApplyMagicalSparkle(main.rgb, main.a, input.uv);

            #if defined(DEBUG_DISPLAY)
                SurfaceData2D surfaceData;
                InputData2D inputData;
                half4 debugColor = 0;

                InitializeSurfaceData(main.rgb, main.a, surfaceData);
                InitializeInputData(input.uv, inputData);
                SETUP_DEBUG_TEXTURE_DATA_2D_NO_TS(inputData, input.positionWS, input.positionCS, _MainTex);

                if (CanDebugOverrideOutputColor(surfaceData, inputData, debugColor))
                {
                    return debugColor;
                }
            #endif

                return main;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/2D/Sprite-Lit-Default"
}
