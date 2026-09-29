Shader "Custom/URP/ScanlineShader"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Texture", 2D) = "white" {}
        [MainColor]   _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        // 走査線の設定
        _ScanlineColor      ("Scanline Color", Color) = (0, 0, 0, 1)
        _ScanlineCount      ("Scanline Count", Float) = 100.0
        _ScanlineIntensity  ("Scanline Intensity", Range(0, 1)) = 0.5
        _ScanlineWidth      ("Scanline Width", Range(0.01, 0.99)) = 0.5

        // スクロール設定
        _ScrollSpeed        ("Scroll Speed", Float) = 0.0

        // スペースの選択 (0=UV空間, 1=スクリーン空間)
        [KeywordEnum(UV, Screen)] _ScanlineSpace ("Scanline Space", Float) = 0

        // フリッカー効果
        _FlickerSpeed       ("Flicker Speed", Float) = 0.0
        _FlickerIntensity   ("Flicker Intensity", Range(0, 1)) = 0.0

        // URP標準設定
        [HideInInspector] _Surface("__surface", Float) = 0.0
        [HideInInspector] _Blend("__blend", Float) = 0.0
        [HideInInspector] _AlphaClip("__clip", Float) = 0.0
        [HideInInspector] _SrcBlend("__src", Float) = 1.0
        [HideInInspector] _DstBlend("__dst", Float) = 0.0
        [HideInInspector] _ZWrite("__zw", Float) = 1.0
        [HideInInspector] _Cull("__cull", Float) = 2.0
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

            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // URP キーワード
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fog
            #pragma multi_compile _ _SCANLINESPACE_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // -------------------------------------------------------
            // Textures & Samplers
            // -------------------------------------------------------
            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // -------------------------------------------------------
            // Constant Buffer
            // -------------------------------------------------------
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                half4  _ScanlineColor;
                float  _ScanlineCount;
                half   _ScanlineIntensity;
                float  _ScanlineWidth;
                float  _ScrollSpeed;
                float  _FlickerSpeed;
                half   _FlickerIntensity;
            CBUFFER_END

            // -------------------------------------------------------
            // Structs
            // -------------------------------------------------------
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS  : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                float4 positionNDC : TEXCOORD3; // スクリーン座標用
                float  fogFactor   : TEXCOORD4;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // -------------------------------------------------------
            // 走査線マスク計算
            // -------------------------------------------------------
            half ScanlineMask(float coord)
            {
                // _ScanlineWidth: 0→線が細い / 1→線が太い
                float scanPos = frac(coord * _ScanlineCount + _Time.y * _ScrollSpeed);
                return step(1.0 - _ScanlineWidth, scanPos);
            }

            // -------------------------------------------------------
            // Vertex
            // -------------------------------------------------------
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   norInputs = GetVertexNormalInputs(IN.normalOS);

                OUT.positionCS  = posInputs.positionCS;
                OUT.positionWS  = posInputs.positionWS;
                OUT.positionNDC = posInputs.positionNDC;
                OUT.normalWS    = norInputs.normalWS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                OUT.fogFactor   = ComputeFogFactor(posInputs.positionCS.z);

                return OUT;
            }

            // -------------------------------------------------------
            // Fragment
            // -------------------------------------------------------
            half4 frag(Varyings IN) : SV_Target
            {
                // ベーステクスチャ & カラー
                half4 baseMap   = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                half4 baseColor = baseMap * _BaseColor;

                // ライティング (Lambertモデル)
                Light mainLight = GetMainLight();
                half3 normalWS  = normalize(IN.normalWS);
                half  NdotL     = saturate(dot(normalWS, mainLight.direction));
                half3 lighting  = mainLight.color * NdotL + half3(0.1, 0.1, 0.1); // + ambient

                half3 litColor = baseColor.rgb * lighting;

                // -------------------------------------------------------
                // 走査線座標の選択
                // -------------------------------------------------------
                float scanCoord;

#if defined(_SCANLINESPACE_SCREEN)
                // スクリーン空間: NDC の y 成分を使用
                float2 screenUV = IN.positionNDC.xy / IN.positionNDC.w;
                scanCoord = screenUV.y;
#else
                // UV空間: テクスチャ UV の v 成分を使用
                scanCoord = IN.uv.y;
#endif

                // 走査線マスク (1=走査線あり, 0=走査線なし)
                half scanMask = ScanlineMask(scanCoord);

                // -------------------------------------------------------
                // フリッカー効果
                // -------------------------------------------------------
                half flicker = 1.0;
                if (_FlickerSpeed > 0.0)
                {
                    // 高周波ノイズで明滅
                    flicker = 1.0 - _FlickerIntensity *
                              (0.5 + 0.5 * sin(_Time.y * _FlickerSpeed * 6.2831));
                }

                // -------------------------------------------------------
                // 走査線を色に合成
                // -------------------------------------------------------
                half3 scanlineContrib = lerp(
                    half3(0, 0, 0),
                    _ScanlineColor.rgb - litColor,   // 走査線カラーとの差分
                    scanMask * _ScanlineIntensity
                );

                half3 finalColor = (litColor + scanlineContrib) * flicker;

                // フォグ
                finalColor = MixFog(finalColor, IN.fogFactor);

                return half4(finalColor, baseColor.a);
            }
            ENDHLSL
        }

        // シャドウキャスト (標準URP流用)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex   ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/ShadowCasterPass.hlsl"
            ENDHLSL
        }

        // デプスオンリーパス
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma vertex   DepthOnlyVertex
            #pragma fragment DepthOnlyFragment

            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/Shaders/DepthOnlyPass.hlsl"
            ENDHLSL
        }
        // OutlineRendererFeature・SSAOなどの深度・法線エッジ検出に必要
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }

    FallBack "Universal Render Pipeline/Lit"
}
