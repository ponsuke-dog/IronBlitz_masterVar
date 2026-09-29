Shader "Custom/URP/Outline"
{
    // ─────────────────────────────────────────────────────────────
    // インバーテッドハル方式のアウトライン（Opaque + ディザ・ディゾルブ）
    //   _USE_SMOOTH_NORMAL ON : UV3(TEXCOORD3) のスムース法線で押し出す（静的メッシュ用）
    //   _USE_SMOOTH_NORMAL OFF: NORMAL で押し出す（キャラ用。クローンのNORMALにベイク済み）
    //   Outline Alpha を下げると、モデルと同じディザで溶けて消える。
    //   ※ Opaqueのまま（深度を保つ）なので殻の内側は隠れ、縁だけ残る。
    // ─────────────────────────────────────────────────────────────
    Properties
    {
        _OutlineColor ("Outline Color", Color)                 = (0, 0, 0, 1)
        _OutlineWidth ("Outline Width", Range(0, 10))          = 1.0
        _OutlineAlpha ("Outline Alpha (Dissolve)", Range(0, 1)) = 1.0
        [Toggle(_USE_SMOOTH_NORMAL)] _UseSmoothNormal ("Use Baked Smooth Normal (UV3)", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType"     = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue"          = "Geometry"
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "UniversalForward" }

            Cull   Front   // 裏面のみ描画（インバーテッドハルの肝）
            ZWrite On      // Opaqueのまま深度を書く（殻の内側を隠すため）

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag
            #pragma shader_feature_local _USE_SMOOTH_NORMAL

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
                float  _OutlineAlpha;
                float  _UseSmoothNormal; // SRP Batcher対応のため宣言
            CBUFFER_END

            // 4x4 Bayer ディザ閾値（Halftone側と同じ。画面座標ベース）
            float DitherThreshold(float2 screenPos)
            {
                const float bayer[16] = {
                     0.0,  8.0,  2.0, 10.0,
                    12.0,  4.0, 14.0,  6.0,
                     3.0, 11.0,  1.0,  9.0,
                    15.0,  7.0, 13.0,  5.0 };
                uint idx = (uint(screenPos.y) & 3u) * 4u + (uint(screenPos.x) & 3u);
                return (bayer[idx] + 0.5) * (1.0 / 16.0);
            }

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                #if defined(_USE_SMOOTH_NORMAL)
                    float3 smoothNormalOS : TEXCOORD3;
                #endif
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };

            Varyings vert(Attributes IN)
            {
                Varyings OUT;

                #if defined(_USE_SMOOTH_NORMAL)
                    float3 dir = IN.smoothNormalOS; // 静的: UV3のスムース法線
                #else
                    float3 dir = IN.normalOS;       // キャラ: NORMAL（スキン変形 / クローンに焼き済み）
                #endif

                float3 posOS = IN.positionOS.xyz + dir * (_OutlineWidth * 0.01);

                OUT.positionHCS = TransformObjectToHClip(posOS);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                // ★ディザ・ディゾルブ: Outline Alpha が下がるほど間引いて消す
                clip((_OutlineColor.a * _OutlineAlpha) - DitherThreshold(IN.positionHCS.xy));
                return float4(_OutlineColor.rgb, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
