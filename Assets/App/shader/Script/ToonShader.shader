Shader "Custom/ToonShader"
{
    Properties
    {
        // ── ベース ──────────────────────────────────────────
        _BaseMap          ("Base Texture", 2D)                = "white" {}
        _BaseColor        ("Base Color", Color)               = (1, 1, 1, 1)

        // ── 影（1段目・2段目） ───────────────────────────────
        _ShadowColor      ("Shadow Color", Color)             = (0.4, 0.4, 0.6, 1)
        _ShadowThreshold  ("Shadow Threshold", Range(-1,1))   = 0.0   // 影の境界位置
        _ShadowSmoothness ("Shadow Smoothness", Range(0,1.0)) = 0.02  // 境界のぼかし幅
        _Shadow2Color     ("Shadow2 Color", Color)            = (0.2, 0.2, 0.4, 1)
        _Shadow2Threshold ("Shadow2 Threshold", Range(-1,1))  = -0.3  // 深い影の境界位置

        // ── スペキュラー（ハイライト） ────────────────────────
        _SpecularColor    ("Specular Color", Color)           = (1, 1, 1, 1)
        _SpecularSize     ("Specular Size", Range(1,200))     = 50    // 値が大きいほど光沢が小さく鋭くなる
        _SpecularThreshold("Specular Threshold", Range(0,1))  = 0.6

        // ── リムライト ──────────────────────────────────────
        _RimColor         ("Rim Color", Color)                = (0.8, 0.8, 1.0, 1)
        _RimPower         ("Rim Power", Range(0.1,8))         = 3.0   // 値が大きいほどリムが細くなる
        _RimThreshold     ("Rim Threshold", Range(0,1))       = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // シャドウマップのサンプリングを有効化
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            // カメラ追従ライトの切り替えキーワード
            #pragma shader_feature USE_CAMERA_DIRECTION

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ── 頂点シェーダー入力 ────────────────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;  // オブジェクト空間の頂点座標
                float3 normalOS   : NORMAL;    // オブジェクト空間の法線
                float2 uv         : TEXCOORD0;
            };

            // ── フラグメントシェーダー入力 ─────────────────────
            struct Varyings
            {
                float4 positionHCS : SV_POSITION; // クリップ空間の頂点座標
                float3 normalWS    : TEXCOORD0;   // ワールド空間の法線
                float3 positionWS  : TEXCOORD1;   // ワールド空間の頂点座標（視線ベクトル計算用）
                float2 uv          : TEXCOORD2;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            // マテリアルごとの定数バッファ
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _ShadowColor;
                float  _ShadowThreshold;
                float  _ShadowSmoothness;
                float4 _Shadow2Color;
                float  _Shadow2Threshold;
                float4 _SpecularColor;
                float  _SpecularSize;
                float  _SpecularThreshold;
                float4 _RimColor;
                float  _RimPower;
                float  _RimThreshold;
                float  _CameraLightOffsetX;
                float  _CameraLightOffsetY;
            CBUFFER_END

            // ── 頂点シェーダー ────────────────────────────────
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            // ── フラグメントシェーダー ─────────────────────────
            half4 frag(Varyings IN) : SV_Target
            {
                float3 normalWS  = normalize(IN.normalWS);
                float3 viewDir   = normalize(GetCameraPositionWS() - IN.positionWS);
                half4  baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                // ライト方向の決定
                // USE_CAMERA_DIRECTION が有効な場合はカメラ基準、無効な場合はシーンのメインライト
                #if defined(USE_CAMERA_DIRECTION)
                    float3 camRight = normalize(UNITY_MATRIX_V[0].xyz); // ビュー行列からカメラ右方向を取得
                    float3 camUp    = normalize(UNITY_MATRIX_V[1].xyz); // ビュー行列からカメラ上方向を取得
                    float3 lightDir = normalize(
                        viewDir
                        + camRight * _CameraLightOffsetX
                        + camUp    * _CameraLightOffsetY
                    );
                #else
                    Light  mainLight = GetMainLight();
                    float3 lightDir  = normalize(mainLight.direction);
                #endif

                // ── 影の計算（2段階トゥーン影） ──────────────────
                float NdotL = dot(normalWS, lightDir); // 法線とライト方向の内積（-1〜1）

                // 深い影（Shadow2）: NdotL が _Shadow2Threshold を超えると明るくなる
                float shadow2 = smoothstep(
                    _Shadow2Threshold - _ShadowSmoothness,
                    _Shadow2Threshold + _ShadowSmoothness,
                    NdotL);

                // 通常の影（Shadow1）: NdotL が _ShadowThreshold を超えるとベースカラーへ遷移
                float shadow1 = smoothstep(
                    _ShadowThreshold - _ShadowSmoothness,
                    _ShadowThreshold + _ShadowSmoothness,
                    NdotL);

                // 影色をブレンド: 深い影 → 通常の影 → ベースカラー
                half4 shadow2Color = _Shadow2Color * baseColor;
                half4 shadow1Color = _ShadowColor  * baseColor;
                half4 color = lerp(shadow2Color, shadow1Color, shadow2);
                      color = lerp(color, baseColor, shadow1);

                // ── スペキュラー（Blinn-Phong） ───────────────────
                float3 halfDir = normalize(lightDir + viewDir); // ハーフベクトル
                float  NdotH   = max(0, dot(normalWS, halfDir));
                // step でトゥーン的なハードエッジなハイライトにする
                // shadow1 を掛けることで影の中にはハイライトを出さない
                float  specular = step(_SpecularThreshold, pow(NdotH, _SpecularSize));
                color += _SpecularColor * specular * shadow1;

                // ── リムライト ────────────────────────────────────
                // 視線と法線が垂直に近い（輪郭付近）ほど rim が 1 に近づく
                float rim     = pow(1.0 - saturate(dot(normalWS, viewDir)), _RimPower);
                // step でトゥーン的なハードエッジにし、影の中では非表示
                float rimMask = step(_RimThreshold, rim) * shadow1;
                color += _RimColor * rimMask;

                return color;
            }
            ENDHLSL
        }
        
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        // デプス・法線パスは URP 標準の Lit シェーダーを流用
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}