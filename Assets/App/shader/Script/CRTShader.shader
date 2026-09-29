Shader "Custom/CRTMonitor"
{
    Properties
    {
        [Header(Base)]
        [MainColor] _BaseColor("Color", Color) = (1,1,1,1)
        [MainTexture] _BaseMap("Albedo", 2D) = "white"{}
        _TintColor("Tint", Color) = (1,1,1,1)
        _TintStrength("TintStrength", Range(0,1)) = 0.5

        // -------------------------------------------------------
        // スキャンライン
        // -------------------------------------------------------
        [Header(Scanline)]
        _Lines("Scanline Count", Float) = 200
        _ScrollSpeed("Scanline ScrollSpeed", Range(-50.0, 50.0)) = 2

        // -------------------------------------------------------
        // 視差オフセット
        // -------------------------------------------------------
        [Header(Parallax)]
        _ZOffset("ZOffset", Range(0.0, 0.03)) = 0.005

        // -------------------------------------------------------
        // ブロックグリッチ
        // -------------------------------------------------------
        [Header(Block Glitch)]
        _BlockSize("Block Size", Float) = 32
        _GlitchAmount("Glitch Amount", Range(0, 1)) = 0.1

        // GlitchFrequency : グリッチが起きる「おおよその頻度」（秒）
        // ただし実際の発生タイミングは乱数でずれる
        _GlitchFrequency("Glitch Frequency (avg sec)", Range(0.1, 10.0)) = 1.5

        // 発生間隔のバラつき（0=規則的, 5=かなりランダム）
        _GlitchIntervalVariance("Glitch Interval Variance", Range(0, 5)) = 2.0

        // グリッチの継続時間（秒）
        _GlitchDuration("Glitch Duration", Range(0, 2)) = 0.3

        // -------------------------------------------------------
        // 色収差
        // -------------------------------------------------------
        [Header(Chromatic Aberration)]
        _ChromaticAberration("Chromatic Aberration", Range(0, 0.05)) = 0.01

        // -------------------------------------------------------
        // フラッシュグリッチ
        // -------------------------------------------------------
        [Header(Flash Glitch)]
        _FlashIntensity("Flash Intensity", Range(0, 1)) = 0.0
        _FlashFrequency("Flash Frequency", Range(0.1, 10.0)) = 2.0
        _FlashColor("Flash Color", Color) = (1,1,1,1)

        // -------------------------------------------------------
        // ラインノイズ
        // -------------------------------------------------------
        [Header(Line Noise)]
        _LineNoiseIntensity("Line Noise Intensity", Range(0, 1)) = 0.0
        _LineNoiseSpeed("Line Noise Speed", Range(0.1, 20.0)) = 3.0
        _LineNoiseThreshold("Line Noise Threshold", Range(0, 1)) = 0.85

        // -------------------------------------------------------
        // フリッカー（明滅）
        // -------------------------------------------------------
        [Header(Flicker)]
        _FlickerSpeed("Flicker Speed", Range(0, 20)) = 0.0
        _FlickerIntensity("Flicker Intensity", Range(0, 1)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 tangentOS  : TANGENT;
                float3 normalOS   : NORMAL;
                float2 texcoord   : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 positionWS  : TEXCOORD1;
                float3 normalWS    : TEXCOORD2;
                half4  tangentWS   : TEXCOORD3;
                half3  bitangentWS : TEXCOORD4;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4  _BaseColor;
                float4 _TintColor;
                float4 _FlashColor;
                float  _TintStrength;
                float  _Lines;
                float  _ScrollSpeed;
                float  _ZOffset;
                float  _BlockSize;
                float  _GlitchAmount;
                float  _GlitchFrequency;
                float  _GlitchIntervalVariance;
                float  _GlitchDuration;
                float  _ChromaticAberration;
                float  _FlashIntensity;
                float  _FlashFrequency;
                float  _LineNoiseIntensity;
                float  _LineNoiseSpeed;
                float  _LineNoiseThreshold;
                float  _FlickerSpeed;
                float  _FlickerIntensity;
            CBUFFER_END

            // -------------------------------------------------------
            // ユーティリティ
            // -------------------------------------------------------
            float Random(float2 pt)
            {
                const float a = 12.9898, b = 78.233, c = 43758.543123;
                return frac(sin(dot(pt, float2(a, b))) * c);
            }

            float Noise2D(float2 st)
            {
                float2 i = floor(st);
                float2 f = frac(st);
                float a = Random(i);
                float b = Random(i + float2(1.0, 0.0));
                float c = Random(i + float2(0.0, 1.0));
                float d = Random(i + float2(1.0, 1.0));
                float2 u = f * f * (3.0 - 2.0 * f);
                return lerp(a, b, u.x) + (c - a) * u.y * (1.0 - u.x) + (d - b) * u.x * u.y;
            }

            // -------------------------------------------------------
            // ランダムタイミングのグリッチ制御
            //
            // 仕組み:
            //   時間を「イベントスロット」に分割し、
            //   各スロットの長さ自体をランダムにすることで
            //   発生タイミングが不規則になる。
            //
            //   slot_duration = _GlitchFrequency
            //                   + Random(slot_index) * _GlitchIntervalVariance
            //
            //   スロット内の経過時間が _GlitchDuration 以内なら ON。
            // -------------------------------------------------------
            float2 GlitchTimingInfo()
            {
                // 粗い時間スロットインデックスを求める
                // （_GlitchFrequency を基準に整数スロットへ）
                float rawSlot = _Time.y / max(_GlitchFrequency, 0.01);
                float slotIdx = floor(rawSlot);

                // このスロットの実際の長さ（ランダムにずらす）
                float variance = Random(float2(slotIdx, 3.77)) * _GlitchIntervalVariance;
                float slotDuration = _GlitchFrequency + variance;

                // スロット開始時刻を積み上げで求める（近似：直前スロットの長さで補正）
                float prevVariance = Random(float2(slotIdx - 1.0, 3.77)) * _GlitchIntervalVariance;
                float prevDuration = _GlitchFrequency + prevVariance;

                // スロット内の経過時間
                float localTime = fmod(_Time.y, slotDuration);

                // グリッチがアクティブか（スロット内の最初の _GlitchDuration 秒間）
                float active = step(localTime, _GlitchDuration);

                // スロットインデックス（ブロックパターン生成に使う）
                return float2(active, slotIdx);
            }

            // -------------------------------------------------------
            // ブロックグリッチオフセット
            // -------------------------------------------------------
            float BlockGlitchOffset(float2 uv)
            {
                float2 timing    = GlitchTimingInfo();
                float  active    = timing.x;
                float  slotIdx   = timing.y;

                // スロットごとにブロックサイズをランダムに変える
                // → 毎回違うブロック粒度でグリッチが出る
                float blockScale = lerp(6.0, 80.0, Random(float2(slotIdx, 1.11)));

                // Y をブロック化
                float yBlock = floor(uv.y * blockScale);

                // ブロックごとに「ずれるか」を独立してランダム決定
                // シードにスロットIDを混ぜることで毎回の位置を変える
                float blockSeed   = Random(float2(yBlock, slotIdx + 7.53));
                float blockActive = step(0.55, blockSeed); // 約45%のブロックがずれる

                // ズレ量もスロットとブロックで毎回変わる
                float offsetMag = (Random(float2(yBlock * 1.7, slotIdx + 0.31)) * 2.0 - 1.0);

                // 細かいブロックレイヤーを重ねてパターンを豊かに
                float blockScale2 = blockScale * (2.0 + Random(float2(slotIdx, 5.55)));
                float yBlock2     = floor(uv.y * blockScale2);
                float blockSeed2  = Random(float2(yBlock2, slotIdx + 4.44));
                float offset2     = (Random(float2(yBlock2 + 0.9, slotIdx + 2.22)) * 2.0 - 1.0)
                                    * step(0.70, blockSeed2) * 0.35;

                float total = (offsetMag * blockActive + offset2) * _GlitchAmount * active;
                return total;
            }

            // -------------------------------------------------------
            // 水平ラインノイズ
            // -------------------------------------------------------
            float LineNoiseOffset(float2 uv)
            {
                float t      = floor(_Time.y * _LineNoiseSpeed);
                float lineY  = floor(uv.y * _Lines);
                float rnd    = Random(float2(lineY, t));
                float active = step(_LineNoiseThreshold, rnd);
                return (Random(float2(lineY + 0.3, t)) * 2.0 - 1.0) * active * _LineNoiseIntensity * 0.1;
            }

            // -------------------------------------------------------
            // 色収差サンプリング
            // -------------------------------------------------------
            float3 SampleWithChromaticAberration(float2 uv, float2 dir)
            {
                float r = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv + dir * _ChromaticAberration).r;
                float g = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).g;
                float b = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv - dir * _ChromaticAberration).b;
                return float3(r, g, b);
            }

            // -------------------------------------------------------
            // Vertex
            // -------------------------------------------------------
            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.positionWS  = TransformObjectToWorld(IN.positionOS.xyz);
                OUT.uv          = IN.texcoord;

                VertexNormalInputs tbn = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                OUT.normalWS    = tbn.normalWS;
                OUT.tangentWS   = float4(tbn.tangentWS, IN.tangentOS.w);
                OUT.bitangentWS = tbn.bitangentWS;
                return OUT;
            }

            // -------------------------------------------------------
            // Fragment
            // -------------------------------------------------------
            half4 frag(Varyings IN) : SV_Target
            {
                // 視差オフセット
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(IN.positionWS));
                float3x3 tbn = float3x3(IN.tangentWS.xyz, IN.bitangentWS, IN.normalWS);
                float3 viewDirTS = mul(tbn, viewDirWS);
                float2 baseUV = IN.uv - viewDirTS.xy * _ZOffset;

                // グリッチUVオフセット
                float blockOffset = BlockGlitchOffset(baseUV);
                float lineOffset  = LineNoiseOffset(baseUV);
                float totalOffsetX = blockOffset + lineOffset;
                float2 glitchedUV  = baseUV + float2(totalOffsetX, 0.0);

                // 色収差（グリッチ量に連動）
                float2 aberrationDir = float2(1.0, 0.0) + float2(totalOffsetX, 0.0) * 5.0;
                float3 sampledColor = SampleWithChromaticAberration(glitchedUV, aberrationDir);

                // RGBチャンネルずれ
                float glitchR = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, glitchedUV + float2(blockOffset * 0.5, 0)).r;
                float glitchG = sampledColor.g;
                float glitchB = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, glitchedUV - float2(blockOffset * 0.5, 0)).b;
                float3 rgbShiftColor = float3(glitchR, glitchG, glitchB);
                float3 finalSample = lerp(sampledColor, rgbShiftColor, saturate(abs(blockOffset) * 10.0));

                // ノイズムラ
                float n = Noise2D(glitchedUV * 8.0);
                float3 srcColor = lerp(finalSample * 2.0, finalSample, n);

                // グレースケール＋ティント
                float grayScale = (srcColor.r + srcColor.g + srcColor.b) / 3.0;
                float3 tinted = grayScale * _TintColor.rgb;
                float3 tintedColor = lerp(srcColor, tinted, _TintStrength);

                // スキャンライン
                float scanline = saturate(
                    smoothstep(0.1, 0.2, frac(glitchedUV.y * _Lines + _Time.y * _ScrollSpeed)) - 0.3
                );
                float3 scannedColor = lerp(srcColor * 0.5, tintedColor, scanline);

                // スウィープ光
                float sweep = smoothstep(0.0, 0.6, sin(glitchedUV.y * 4 + _Time.y * 3) * 0.5 + 0.5);
                scannedColor *= lerp(0.5, 1.0, sweep);

                // -------------------------------------------------------
                // RGBサブピクセル縦縞（モアレ対策つき）
                // -------------------------------------------------------
                // カメラ距離を取得
                float camDist = length(GetWorldSpaceViewDir(IN.positionWS));

                // UV の変化量（ddx/ddy）でピクセルあたりのUV密度を計算
                // → これがそのまま「どれだけエイリアシングが起きるか」の指標
                float2 uvDdx = ddx(glitchedUV);
                float2 uvDdy = ddy(glitchedUV);
                float uvDensity = max(length(uvDdx), length(uvDdy));

                float freq = 640.0 + Noise2D(glitchedUV * 8.0) * 8.0;

                // サブピクセル1本分のUV幅
                float subpixelWidth = 1.0 / (freq * 3.0);

                // uvDensity が subpixelWidth を超え始めたらパターンをフェードアウト
                // smoothstep: subpixelWidth*0.5 以下=フル表示、subpixelWidth*2.0 以上=完全消滅
                float moareFade = 1.0 - smoothstep(subpixelWidth * 0.5, subpixelWidth * 2.0, uvDensity);

                float pixelLine = floor(frac(glitchedUV.x * freq) * 3.0);
                float3 pixelColor =
                    pixelLine < 1.0 ? float3(0.8, 0.0, 0.0) :
                    pixelLine < 2.0 ? float3(0.0, 0.8, 0.0) :
                                      float3(0.0, 0.0, 0.8);

                // 遠距離ではpixelColorを中間値(0.267 = (0.8+0+0)/3)に近づけてフラット化
                float3 pixelColorFaded = lerp(float3(0.267, 0.267, 0.267), pixelColor, moareFade);

                // スキャンラインも同様に距離フェード
                float scanlineFade = 1.0 - smoothstep(0.005, 0.02, uvDensity);
                float3 scannedColorFaded = lerp(tintedColor, scannedColor, scanlineFade);

                float3 finalColor = scannedColorFaded + rgbShiftColor - pixelColorFaded;

                // フラッシュグリッチ
                float flashTime  = floor(_Time.y * _FlashFrequency);
                float flashRnd   = Random(float2(flashTime, 9.99));
                float flashLocal = frac(_Time.y * _FlashFrequency);
                float flash      = step(0.95, flashRnd) * step(flashLocal, 0.05) * _FlashIntensity;
                finalColor       = lerp(finalColor, _FlashColor.rgb, flash);

                // フリッカー（明滅）
                // sin波の高速変動で蛍光灯・CRTのちらつきを再現
                float flicker = 1.0 - _FlickerIntensity *
                    saturate(0.5 + 0.5 * sin(_Time.y * _FlickerSpeed * 6.2831));
                finalColor *= flicker;

                return half4(finalColor, 1.0);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
