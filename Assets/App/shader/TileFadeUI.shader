Shader "Custom/TileFadeUI_URP"
{
    Properties
    {
        _FadeColor ("Fade Color", Color) = (0,0,0,1)
        _Progress ("Progress", Range(0,1)) = 0
        _Columns ("Columns", Float) = 12
        _Rows ("Rows", Float) = 8
        _Edge ("Tile Edge Width", Range(0,0.2)) = 0.02
        _EdgeColor ("Edge Color", Color) = (0,0,0,1)
        _Randomness ("Randomness", Range(0,1)) = 0.15
        _FadeToBlack ("Fade To Black", Float) = 1
        _RandomSeed ("Random Seed", Float) = 12345
        _OrderMode ("Order Mode", Float) = 0
        _StartCorner ("Start Corner", Float) = 0
        _RandomBatchSize ("Random Batch Size", Float) = 1
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "IgnoreProjector"="True"
            "CanUseSpriteAtlas"="True"
        }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode"="UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            float4 _FadeColor;
            float4 _EdgeColor;
            float _Progress;
            float _Columns;
            float _Rows;
            float _Edge;
            float _Randomness;
            float _FadeToBlack;
            float _RandomSeed;
            float _OrderMode;
            float _StartCorner;
            float _RandomBatchSize;

            float Hash21(float2 p)
            {
                p += _RandomSeed;
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float2 ApplyCornerTransform(float2 cell, float cols, float rows, float startCorner)
            {
                float2 c = cell;

                // 0=LeftTop, 1=RightTop, 2=LeftBottom, 3=RightBottom
                if (startCorner == 1 || startCorner == 3)
                {
                    c.x = (cols - 1.0) - c.x;
                }

                if (startCorner == 0 || startCorner == 1)
                {
                    c.y = (rows - 1.0) - c.y;
                }

                return c;
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                float2 uv = IN.uv;
                float cols = max(_Columns, 1.0);
                float rows = max(_Rows, 1.0);
                float2 grid = float2(cols, rows);

                float2 cell = floor(uv * grid);
                float2 localUV = frac(uv * grid);

                float totalCells = max(1.0, cols * rows);
                float2 transformedCell = ApplyCornerTransform(cell, cols, rows, _StartCorner);

                float orderedIndex = 0.0;
                float visible = 0.0;

                // 通し番号
                float linearIndex = transformedCell.y * cols + transformedCell.x;
                float reverseLinearIndex = (totalCells - 1.0) - linearIndex;

                // 縦方向優先の通し番号
                float verticalLinearIndex = transformedCell.x * rows + transformedCell.y;
                float reverseVerticalLinearIndex = (totalCells - 1.0) - verticalLinearIndex;

                // 0=LeftToRight : 1タイルずつ
                if (_OrderMode < 0.5)
                {
                    orderedIndex = (linearIndex + 0.5) / totalCells;
                    visible = step(orderedIndex, _Progress);
                }
                // 1=RightToLeft : 1タイルずつ
                else if (_OrderMode < 1.5)
                {
                    orderedIndex = (reverseLinearIndex + 0.5) / totalCells;
                    visible = step(orderedIndex, _Progress);
                }
                // 2=TopToBottom : 1タイルずつ
                else if (_OrderMode < 2.5)
                {
                    orderedIndex = (reverseVerticalLinearIndex + 0.5) / totalCells;
                    visible = step(orderedIndex, _Progress);
                }
                // 3=BottomToTop : 1タイルずつ
                else if (_OrderMode < 3.5)
                {
                    orderedIndex = (verticalLinearIndex + 0.5) / totalCells;
                    visible = step(orderedIndex, _Progress);
                }
                // 4=CenterOut
                else if (_OrderMode < 4.5)
                {
                    float2 center = float2((cols - 1.0) * 0.5, (rows - 1.0) * 0.5);
                    float dist = distance(cell, center);
                    float maxDist = distance(float2(0, 0), center);
                    orderedIndex = dist / max(maxDist, 0.0001);
                    visible = step(orderedIndex, _Progress);
                }
                // 5=OutsideIn
                else if (_OrderMode < 5.5)
                {
                    float2 center = float2((cols - 1.0) * 0.5, (rows - 1.0) * 0.5);
                    float dist = distance(cell, center);
                    float maxDist = distance(float2(0, 0), center);
                    orderedIndex = 1.0 - (dist / max(maxDist, 0.0001));
                    visible = step(orderedIndex, _Progress);
                }
                // 6=DiagonalTLtoBR
                else if (_OrderMode < 6.5)
                {
                    orderedIndex = (transformedCell.x + transformedCell.y + 1.0) / (cols + rows);
                    visible = step(orderedIndex, _Progress);
                }
                // 7=DiagonalTRtoBL
                else if (_OrderMode < 7.5)
                {
                    float dx = (cols - 1.0) - transformedCell.x;
                    orderedIndex = (dx + transformedCell.y + 1.0) / (cols + rows);
                    visible = step(orderedIndex, _Progress);
                }
                // 8=ZigZagHorizontal
                else if (_OrderMode < 8.5)
                {
                    float rowParity = fmod(transformedCell.y, 2.0);
                    float zigX = rowParity < 0.5 ? transformedCell.x : ((cols - 1.0) - transformedCell.x);
                    orderedIndex = ((transformedCell.y * cols) + zigX + 0.5) / totalCells;
                    visible = step(orderedIndex, _Progress);
                }
                // 9=ZigZagVertical
                else if (_OrderMode < 9.5)
                {
                    float colParity = fmod(transformedCell.x, 2.0);
                    float zigY = colParity < 0.5 ? transformedCell.y : ((rows - 1.0) - transformedCell.y);
                    orderedIndex = ((transformedCell.x * rows) + zigY + 0.5) / totalCells;
                    visible = step(orderedIndex, _Progress);
                }
                // 10=RowByRow : 1行まとめて
                else if (_OrderMode < 10.5)
                {
                    orderedIndex = (transformedCell.y + 0.5) / rows;
                    visible = step(orderedIndex, _Progress);
                }
                // 11=ColumnByColumn : 1列まとめて
                else if (_OrderMode < 11.5)
                {
                    orderedIndex = (transformedCell.x + 0.5) / cols;
                    visible = step(orderedIndex, _Progress);
                }
                // 12=Checkerboard
                else if (_OrderMode < 12.5)
                {
                    float parity = fmod(transformedCell.x + transformedCell.y, 2.0);
                    float halfProgress = _Progress * 2.0;

                    if (halfProgress < 1.0)
                    {
                        visible = (parity < 0.5) ? step(0.5, halfProgress) : 0.0;
                    }
                    else
                    {
                        visible = (parity < 0.5) ? 1.0 : step(0.5, halfProgress - 1.0);
                    }
                }
                // 13=Random
                else
                {
                    float randomIndex = Hash21(transformedCell);

                    float batchSize = max(_RandomBatchSize, 1.0);
                    float batchCount = ceil(totalCells / batchSize);

                    float batchIndex = floor(randomIndex * batchCount);
                    orderedIndex = batchIndex / max(batchCount - 1.0, 1.0);

                    visible = step(orderedIndex, _Progress);
                }

                float edgeMask = 0.0;
                edgeMask += step(localUV.x, _Edge);
                edgeMask += step(localUV.y, _Edge);
                edgeMask += step(1.0 - localUV.x, _Edge);
                edgeMask += step(1.0 - localUV.y, _Edge);
                edgeMask = saturate(edgeMask);

                half4 tileColor = _FadeColor;
                tileColor.rgb = lerp(tileColor.rgb, _EdgeColor.rgb, edgeMask);

                float alphaMask;
                if (_FadeToBlack > 0.5)
                {
                    alphaMask = visible;
                }
                else
                {
                    alphaMask = 1.0 - visible;
                }

                tileColor.a *= alphaMask;

                return tileColor * IN.color;
            }
            ENDHLSL
        }
    }
}