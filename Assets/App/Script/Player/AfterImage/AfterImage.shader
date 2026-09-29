Shader "Custom/AfterImage"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.2, 0.8, 1.0, 0.15)
        _EdgeColor ("Edge Color", Color) = (0.7, 1.0, 1.0, 0.75)
        _Blur ("Blur", Range(1, 8)) = 2
        _Fade ("Fade", Range(0, 1)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back

        Pass
        {
            Name "RugbyAfterimage"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD1;
                half3 normalWS : TEXCOORD2;
            };

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EdgeColor;
                float _Blur;
                float _Fade;
            CBUFFER_END

            Varyings vert(Attributes input)
            {
                Varyings output;

                VertexPositionInputs positions = GetVertexPositionInputs(input.positionOS.xyz);

                output.positionCS = positions.positionCS;
                output.positionWS = positions.positionWS;
                output.uv = input.uv;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);

                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 normalWS = normalize(input.normalWS);
                half3 cameraDirection = normalize(_WorldSpaceCameraPos.xyz - input.positionWS);

                half rimBase = saturate(dot(normalWS, cameraDirection));
                half rim = pow(rimBase, _Blur);

                half4 color = lerp(_EdgeColor, _BaseColor, rim);
                color.a *= saturate(_Fade);

                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}