Shader "Custom/URP/FullEmission"
{
    Properties
    {
        // 発光色。HDRなので Intensity スライダーで明るさを上げられます。
        [HDR] _EmissionColor ("Emission Color", Color) = (1, 1, 1, 1)
        // 色とは別に明るさを掛ける倍率。Bloom を強めたいとき用。
        _Intensity ("Intensity", Range(0, 20)) = 1
 
        // 半透明にしたい場合のアルファ。通常は 1。
        _Alpha ("Alpha", Range(0, 1)) = 1
 
        // 描画設定（デフォルトは不透明・両面表示OFF）
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2 // 2 = Back
        [Enum(Off, 0, On, 1)] _ZWrite ("ZWrite", Float) = 1
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
            Name "ForwardUnlit"
            Tags { "LightMode" = "UniversalForward" }
 
            Cull  [_Cull]
            ZWrite [_ZWrite]
            Blend One Zero   // 不透明。半透明にしたい場合は下のコメント参照。
 
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
 
            // URP のコアライブラリ
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 
            struct Attributes
            {
                float4 positionOS : POSITION;
            };
 
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
            };
 
            // マテリアルごとに設定される値（SRP Batcher 対応のため CBUFFER にまとめる）
            CBUFFER_START(UnityPerMaterial)
                half4 _EmissionColor;
                half  _Intensity;
                half  _Alpha;
            CBUFFER_END
 
            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }
 
            half4 frag (Varyings IN) : SV_Target
            {
                // ライティングを一切受けず、モデル全面を発光色で塗る
                half3 emission = _EmissionColor.rgb * _Intensity;
                return half4(emission, _Alpha);
            }
            ENDHLSL
        }
    }
 
    // URP 非対応環境やエラー時のフォールバック
    FallBack "Universal Render Pipeline/Unlit"
}
 
