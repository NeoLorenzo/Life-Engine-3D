Shader "LifeEngine/Foliage/TreeCutoutLit"
{
    Properties
    {
        // Specular vs Metallic workflow
        _WorkflowMode("WorkflowMode", Float) = 1.0

        [MainTexture] _BaseMap("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)

        _Cutoff("Alpha Cutoff", Range(0.0, 1.0)) = 0.5

        _Smoothness("Smoothness", Range(0.0, 1.0)) = 0.5
        _GlossMapScale("Smoothness Scale", Range(0.0, 1.0)) = 1.0
        _SmoothnessTextureChannel("Smoothness texture channel", Float) = 0

        _Metallic("Metallic", Range(0.0, 1.0)) = 0.0
        _MetallicGlossMap("Metallic", 2D) = "white" {}

        _SpecColor("Specular", Color) = (0.2, 0.2, 0.2)
        _SpecGlossMap("Specular", 2D) = "white" {}

        [ToggleOff] _SpecularHighlights("Specular Highlights", Float) = 1.0
        [ToggleOff] _EnvironmentReflections("Environment Reflections", Float) = 1.0

        _BumpScale("Scale", Float) = 1.0
        _BumpMap("Normal Map", 2D) = "bump" {}

        _Parallax("Scale", Range(0.005, 0.08)) = 0.005
        _ParallaxMap("Height Map", 2D) = "black" {}

        _OcclusionStrength("Strength", Range(0.0, 1.0)) = 1.0
        _OcclusionMap("Occlusion", 2D) = "white" {}

        [HDR] _EmissionColor("Color", Color) = (0,0,0)
        _EmissionMap("Emission", 2D) = "white" {}

        _DetailMask("Detail Mask", 2D) = "white" {}
        _DetailAlbedoMapScale("Scale", Range(0.0, 2.0)) = 1.0
        _DetailAlbedoMap("Detail Albedo x2", 2D) = "linearGrey" {}
        _DetailNormalMapScale("Scale", Range(0.0, 2.0)) = 1.0
        _DetailNormalMap("Normal Map", 2D) = "bump" {}

        // Blending state
        _Surface("__surface", Float) = 0.0
        _Blend("__blend", Float) = 0.0
        _Cull("__cull", Float) = 2.0
        [ToggleUI] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _BlendOp("__blendop", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _SrcBlendAlpha("__srcA", Float) = 1.0
        [HideInInspector] _DstBlendAlpha("__dstA", Float) = 0.0
        [HideInInspector] _ZWrite("__zwrite", Float) = 1.0
        [HideInInspector] _AlphaToMask("__alphaToMask", Float) = 0.0

        [ToggleUI] _ReceiveShadows("Receive Shadows", Float) = 1.0
        _QueueOffset("Queue offset", Float) = 0.0

        [HideInInspector] _MainTex("BaseMap", 2D) = "white" {}
        [HideInInspector] _Color("BaseColor", Color) = (1,1,1,1)
        [HideInInspector] _Glossiness("Smoothness", Float) = 0.5
        [HideInInspector] _GlossMapScale("Smoothness Scale", Float) = 1.0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "IgnoreProjector" = "True"
        }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend [_SrcBlend] [_DstBlend], [_SrcBlendAlpha] [_DstBlendAlpha]
            ZWrite [_ZWrite]
            Cull [_Cull]
            AlphaToMask [_AlphaToMask]

            HLSLPROGRAM
            #pragma target 3.0

            #define REQUIRES_WORLD_SPACE_POS_INTERPOLATOR

            // -------------------------------------
            // Material Keywords
            #pragma shader_feature_fragment _SURFACE_TYPE_TRANSPARENT
            #pragma shader_feature_fragment _ALPHATEST_ON
            #pragma shader_feature_fragment _ALPHAPREMULTIPLY_ON
            #pragma shader_feature_fragment _EMISSION
            #pragma shader_feature_fragment _METALLICSPECGLOSSMAP
            #pragma shader_feature_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_fragment _OCCLUSIONMAP
            #pragma shader_feature_fragment _SPECULARHIGHLIGHTS_OFF
            #pragma shader_feature_fragment _ENVIRONMENTREFLECTIONS_OFF
            #pragma shader_feature_fragment _SPECULAR_SETUP
            #pragma shader_feature_fragment _NORMALMAP

            // -------------------------------------
            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _LIGHT_COOKIES
            #pragma multi_compile _ _CLUSTERED_RENDERING

            // -------------------------------------
            // Unity defined keywords
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ DYNAMICLIGHTMAP_ON
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ DEBUG_DISPLAY

            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragmentCustom

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"

            // -------------------------------------
            // Sky Reveal Global Buffers (set via Shader.SetGlobal*)
            float _SkyRevealEnabled;
            int _SkyRevealCount;
            float4 _SkyRevealCameraPosition;
            float4 _SkyRevealTargets[64];

            void ApplySkyRevealCutout(float3 positionWS)
            {
                if (_SkyRevealEnabled > 0.5)
                {
                    int count = min(_SkyRevealCount, 64);
                    float3 camPos = _SkyRevealCameraPosition.xyz;
                    for (int i = 0; i < count; i++)
                    {
                        float3 targetPos = _SkyRevealTargets[i].xyz;
                        float radius = _SkyRevealTargets[i].w;
                        float3 axis = camPos - targetPos;
                        float axisLengthSq = dot(axis, axis);
                        if (axisLengthSq > 0.0001)
                        {
                            float along = dot(positionWS - targetPos, axis) / axisLengthSq;
                            if (along >= 0.0 && along <= 1.0)
                            {
                                float3 closestPoint = targetPos + axis * along;
                                float3 delta = positionWS - closestPoint;
                                if (dot(delta, delta) < radius * radius)
                                {
                                    clip(-1.0);
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            void LitPassFragmentCustom(
                Varyings input
                , out half4 outColor : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                ApplySkyRevealCutout(input.positionWS);

            #ifdef _WRITE_RENDERING_LAYERS
                LitPassFragment(input, outColor, outRenderingLayers);
            #else
                LitPassFragment(input, outColor);
            #endif
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0

            #pragma shader_feature_fragment _ALPHATEST_ON
            #pragma shader_feature_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #pragma vertex ShadowPassVertexCustom
            #pragma fragment ShadowPassFragmentCustom

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"

            // -------------------------------------
            // Sky Reveal Global Buffers (set via Shader.SetGlobal*)
            float _SkyRevealEnabled;
            int _SkyRevealCount;
            float4 _SkyRevealCameraPosition;
            float4 _SkyRevealTargets[64];

            void ApplySkyRevealCutout(float3 positionWS)
            {
                if (_SkyRevealEnabled > 0.5)
                {
                    int count = min(_SkyRevealCount, 64);
                    float3 camPos = _SkyRevealCameraPosition.xyz;
                    for (int i = 0; i < count; i++)
                    {
                        float3 targetPos = _SkyRevealTargets[i].xyz;
                        float radius = _SkyRevealTargets[i].w;
                        float3 axis = camPos - targetPos;
                        float axisLengthSq = dot(axis, axis);
                        if (axisLengthSq > 0.0001)
                        {
                            float along = dot(positionWS - targetPos, axis) / axisLengthSq;
                            if (along >= 0.0 && along <= 1.0)
                            {
                                float3 closestPoint = targetPos + axis * along;
                                float3 delta = positionWS - closestPoint;
                                if (dot(delta, delta) < radius * radius)
                                {
                                    clip(-1.0);
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            struct VaryingsShadowCustom
            {
                #if defined(_ALPHATEST_ON)
                    float2 uv       : TEXCOORD0;
                #endif
                float3 positionWS   : TEXCOORD1;
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            VaryingsShadowCustom ShadowPassVertexCustom(Attributes input)
            {
                VaryingsShadowCustom output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                #if defined(_ALPHATEST_ON)
                    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                #endif

                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = GetShadowPositionHClip(input);
                return output;
            }

            half4 ShadowPassFragmentCustom(VaryingsShadowCustom input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);

                ApplySkyRevealCutout(input.positionWS);

                #if defined(_ALPHATEST_ON)
                    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
                #endif

                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(input.positionCS);
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
            #pragma target 3.0

            #pragma shader_feature_fragment _ALPHATEST_ON
            #pragma shader_feature_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A

            #pragma multi_compile_instancing

            #pragma vertex DepthOnlyVertexCustom
            #pragma fragment DepthOnlyFragmentCustom

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"

            // -------------------------------------
            // Sky Reveal Global Buffers (set via Shader.SetGlobal*)
            float _SkyRevealEnabled;
            int _SkyRevealCount;
            float4 _SkyRevealCameraPosition;
            float4 _SkyRevealTargets[64];

            void ApplySkyRevealCutout(float3 positionWS)
            {
                if (_SkyRevealEnabled > 0.5)
                {
                    int count = min(_SkyRevealCount, 64);
                    float3 camPos = _SkyRevealCameraPosition.xyz;
                    for (int i = 0; i < count; i++)
                    {
                        float3 targetPos = _SkyRevealTargets[i].xyz;
                        float radius = _SkyRevealTargets[i].w;
                        float3 axis = camPos - targetPos;
                        float axisLengthSq = dot(axis, axis);
                        if (axisLengthSq > 0.0001)
                        {
                            float along = dot(positionWS - targetPos, axis) / axisLengthSq;
                            if (along >= 0.0 && along <= 1.0)
                            {
                                float3 closestPoint = targetPos + axis * along;
                                float3 delta = positionWS - closestPoint;
                                if (dot(delta, delta) < radius * radius)
                                {
                                    clip(-1.0);
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            struct VaryingsDepthCustom
            {
                #if defined(_ALPHATEST_ON)
                    float2 uv       : TEXCOORD0;
                #endif
                float3 positionWS   : TEXCOORD1;
                float4 positionCS   : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsDepthCustom DepthOnlyVertexCustom(Attributes input)
            {
                VaryingsDepthCustom output = (VaryingsDepthCustom)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                #if defined(_ALPHATEST_ON)
                    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                #endif
                output.positionWS = TransformObjectToWorld(input.position.xyz);
                output.positionCS = TransformObjectToHClip(input.position.xyz);
                return output;
            }

            half DepthOnlyFragmentCustom(VaryingsDepthCustom input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                ApplySkyRevealCutout(input.positionWS);

                #if defined(_ALPHATEST_ON)
                    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
                #endif

                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(input.positionCS);
                #endif

                return input.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0

            #pragma shader_feature_fragment _ALPHATEST_ON
            #pragma shader_feature_fragment _SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A
            #pragma shader_feature_fragment _NORMALMAP

            #pragma multi_compile_instancing

            #pragma vertex DepthNormalsVertexCustom
            #pragma fragment DepthNormalsFragmentCustom

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthNormalsPass.hlsl"

            // -------------------------------------
            // Sky Reveal Global Buffers (set via Shader.SetGlobal*)
            float _SkyRevealEnabled;
            int _SkyRevealCount;
            float4 _SkyRevealCameraPosition;
            float4 _SkyRevealTargets[64];

            void ApplySkyRevealCutout(float3 positionWS)
            {
                if (_SkyRevealEnabled > 0.5)
                {
                    int count = min(_SkyRevealCount, 64);
                    float3 camPos = _SkyRevealCameraPosition.xyz;
                    for (int i = 0; i < count; i++)
                    {
                        float3 targetPos = _SkyRevealTargets[i].xyz;
                        float radius = _SkyRevealTargets[i].w;
                        float3 axis = camPos - targetPos;
                        float axisLengthSq = dot(axis, axis);
                        if (axisLengthSq > 0.0001)
                        {
                            float along = dot(positionWS - targetPos, axis) / axisLengthSq;
                            if (along >= 0.0 && along <= 1.0)
                            {
                                float3 closestPoint = targetPos + axis * along;
                                float3 delta = positionWS - closestPoint;
                                if (dot(delta, delta) < radius * radius)
                                {
                                    clip(-1.0);
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            struct VaryingsDepthNormalsCustom
            {
                float4 positionCS   : SV_POSITION;
                #if defined(_ALPHATEST_ON)
                    float2 uv       : TEXCOORD1;
                #endif
                float3 normalWS     : TEXCOORD2;
                float3 positionWS   : TEXCOORD3;

                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            VaryingsDepthNormalsCustom DepthNormalsVertexCustom(Attributes input)
            {
                VaryingsDepthNormalsCustom output = (VaryingsDepthNormalsCustom)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                #if defined(_ALPHATEST_ON)
                    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
                #endif
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);

                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normal, input.tangentOS);
                output.normalWS = NormalizeNormalPerVertex(normalInput.normalWS);

                return output;
            }

            void DepthNormalsFragmentCustom(
                VaryingsDepthNormalsCustom input
                , out half4 outNormalWS : SV_Target0
            #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
            #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                ApplySkyRevealCutout(input.positionWS);

                #if defined(_ALPHATEST_ON)
                    Alpha(SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a, _BaseColor, _Cutoff);
                #endif

                #if defined(LOD_FADE_CROSSFADE)
                    LODFadeCrossFade(input.positionCS);
                #endif

                #if defined(_NORMALMAP)
                    outNormalWS = half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
                #else
                    outNormalWS = half4(NormalizeNormalPerPixel(input.normalWS), 0.0);
                #endif

                #ifdef _WRITE_RENDERING_LAYERS
                    outRenderingLayers = EncodeMeshRenderingLayer();
                #endif
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
    CustomEditor "UnityEditor.Rendering.Universal.ShaderGUI.LitShader"
}
