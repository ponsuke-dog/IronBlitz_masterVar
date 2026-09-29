Shader "Custom/URP/HalftoneLit"
{
    Properties
    {
        [Header(Surface Options)]
        [Enum(Opaque,0,Transparent,1)] _Surface ("Surface Type", Float) = 0
        // Render Face: Both=Cull Off(0) / Back=Cull Front(1) / Front=Cull Back(2)
        [Enum(Both,0,Back,1,Front,2)]  _Cull    ("Render Face", Float)  = 2
        // 以下3つは ShaderGUI(HalftoneShaderGUI.cs) が自動設定するので隠す
        [HideInInspector] _SrcBlend ("__src", Float) = 1
        [HideInInspector] _DstBlend ("__dst", Float) = 0
        [HideInInspector] _ZWrite   ("__zw",  Float) = 1

        [Header(Texture)]
        _MainTex  ("Texture", 2D)     = "white" {}

        [Header(World Tiling)]
        // ワールド座標でタイリング（拡大しても伸びず模様が繰り返す）
        // 床(Plane/Cube)向け。キャラは通常OFFでメッシュUVを使う
        [Toggle(_WORLD_TRIPLANAR)] _WorldTriplanar ("World-Space Tiling (Triplanar)", Float) = 0
        _WorldTexScale      ("World Tiling Scale", Float)                = 1.0
        _TriplanarSharpness ("Triplanar Blend Sharpness", Range(1, 16)) = 4.0

        [Header(Normal Map)]
        _NormalMap      ("Normal Map", 2D)                    = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2))      = 1.0
        _NormalShading  ("Normal Shading Intensity", Range(0, 2)) = 1.0

        [Header(PBR Maps)]
        _MetalnessMap   ("Metalness Map", 2D)              = "black" {}
        _RoughnessMap   ("Roughness Map", 2D)              = "white" {}
        _MetalnessScale ("Metalness Scale", Range(0, 1))   = 1.0
        _RoughnessScale ("Roughness Scale", Range(0, 1))   = 1.0

        [Header(Toon Specular)]
        _SpecColor     ("Specular Color", Color)                       = (1,1,1,1)
        _SpecStrength  ("Specular Strength", Range(0, 2))              = 1.0
        _SpecGloss     ("Specular Size (higher = smaller)", Range(1, 256)) = 40.0
        _SpecThreshold ("Specular Threshold", Range(0, 1))            = 0.5
        _SpecSoftness  ("Specular Edge Softness", Range(0.001, 0.5))  = 0.02

        [Header(Occlusion)]
        _OcclusionMap      ("Occlusion Map", 2D)               = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1.0
        // 環境光(SampleSH)の上限。HDRで環境光が跳ねてBloomが爆発するのを防ぐ
        _AmbientMax        ("Ambient Max (clamp)", Range(0, 4)) = 1.0

        [Header(Emission)]
        _EmissionMap         ("Emission Map", 2D)              = "black" {}
        [HDR]
        _EmissionColor       ("Emission Color", Color)         = (0,0,0,1)
        _EmissionIntensity   ("Emission Intensity", Range(0, 10)) = 5.0

        _EmissionMaskMap     ("Emission Mask Map", 2D)         = "white" {}
        _EmissionMaskScrollX ("Mask Scroll X", Float)          = 0.0
        _EmissionMaskScrollY ("Mask Scroll Y", Float)          = 0.0

        [Header(Rim Light)]
        _RimColor      ("Rim Color", Color)                  = (1,1,1,1)
        _RimThreshold  ("Rim Threshold", Range(0, 1))        = 0.2
        _RimSmoothness ("Rim Smoothness", Range(0.001, 0.2)) = 0.05
        _RimIntensity  ("Rim Intensity", Range(0, 2))        = 1.0

        [Header(Halftone)]
        _DotFreq          ("Dot Frequency (dots per UV)", Float)        = 20.0
        _DotMin           ("Dot Size (shadow areas)", Range(0.00, 5.0))  = 0.01
        _DotMax           ("Dot Size (lit areas)",    Range(0.0, 2.0))   = 1.5
        _Angle            ("Grid Angle (deg)", Range(-90, 90))           = 45.0
        _DotThreshold     ("Dot Threshold",   Range(-1, 1))              = 0.0
        _DotSmoothness    ("Dot Smoothness",  Range(-0.5, 1))            = -0.2
        _ViewDotInfluence ("View Angle Dot Influence", Range(0, 1))     = 0.3
        // 追加ライトの「なめらかな色のせ」の強さ。0=ドット主体(色は控えめ) / 1=色ハッキリ
        _AddLightDiffuse  ("Additional Light Diffuse (0=dots only)", Range(0, 1)) = 1.0
        // 光の強度でドットの大きさを変える。0=強度無視 / 1=強く反映（強い光ほど小さいドット）
        _LightDotScale       ("Light Intensity to Dot Size", Range(0, 1)) = 1.0
        // 光の向きでドットの格子を回転。0=固定(_Angleのみ) / 1=光に追従
        _LightAngleInfluence ("Light Dir to Grid Angle", Range(0, 1))     = 1.0
        // スクリーン空間ドット: ONでUV/曲面に影響されない均一な丸ドット（漫画トーン）。キャラ向け
        [Toggle(_SCREEN_SPACE_DOTS)] _ScreenSpaceDots ("Screen-Space Dots", Float) = 0
        _ScreenDotSize ("Screen Dot Size (px)", Float) = 8.0

        [Header(BaseColor)]
        _BgColor  ("Base Color", Color) = (1,1,1,1)
        _Color    ("Dot Color", Color)  = (0,0,0,1)
        // 全体透明度（ディザ・ディゾルブで消す）
        _Alpha    ("Alpha (Opacity)", Range(0, 1)) = 1.0
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
            Name "HalftoneLit"
            Tags { "LightMode" = "UniversalForward" }

            Blend  [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull   [_Cull]

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _SHADOWS_SOFT
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #pragma multi_compile _ _FORWARD_PLUS
            #pragma shader_feature_local _WORLD_TRIPLANAR
            #pragma shader_feature_local _SCREEN_SPACE_DOTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);          SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalMap);        SAMPLER(sampler_NormalMap);
            TEXTURE2D(_MetalnessMap);     SAMPLER(sampler_MetalnessMap);
            TEXTURE2D(_RoughnessMap);     SAMPLER(sampler_RoughnessMap);
            TEXTURE2D(_OcclusionMap);     SAMPLER(sampler_OcclusionMap);
            TEXTURE2D(_EmissionMap);      SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_EmissionMaskMap);  SAMPLER(sampler_EmissionMaskMap);

            CBUFFER_START(UnityPerMaterial)
                float  _Surface;
                float  _Cull;
                float  _SrcBlend;
                float  _DstBlend;
                float  _ZWrite;

                float4 _MainTex_ST;
                float  _WorldTexScale;
                float  _TriplanarSharpness;
                float4 _Color;
                float4 _NormalMap_ST;
                float  _NormalStrength;
                float  _NormalShading;
                float4 _MetalnessMap_ST;
                float4 _RoughnessMap_ST;
                float  _MetalnessScale;
                float  _RoughnessScale;
                float4 _SpecColor;
                float  _SpecStrength;
                float  _SpecGloss;
                float  _SpecThreshold;
                float  _SpecSoftness;
                float4 _OcclusionMap_ST;
                float  _OcclusionStrength;
                float  _AmbientMax;
                float4 _EmissionMap_ST;
                float4 _EmissionColor;
                float  _EmissionIntensity;
                float4 _EmissionMaskMap_ST;
                float  _EmissionMaskScrollX;
                float  _EmissionMaskScrollY;
                float4 _RimColor;
                float  _RimThreshold;
                float  _RimSmoothness;
                float  _RimIntensity;
                float  _DotFreq;
                float  _DotMin;
                float  _DotMax;
                float  _Angle;
                float  _DotThreshold;
                float  _DotSmoothness;
                float  _ViewDotInfluence;
                float  _AddLightDiffuse;
                float  _LightDotScale;
                float  _LightAngleInfluence;
                float  _ScreenSpaceDots;
                float  _ScreenDotSize;
                float4 _BgColor;
                float  _Alpha;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 tangentWS   : TEXCOORD2;
                float3 bitangentWS : TEXCOORD3;
                float3 positionWS  : TEXCOORD4;
            };

            float2 Rot(float2 p, float deg)
            {
                float r = deg * (PI / 180.0);
                float s = sin(r), c = cos(r);
                return float2(c * p.x - s * p.y, s * p.x + c * p.y);
            }


            // トゥーン用ステップスペキュラー（Blinn-Phongをsmoothstepで硬い縁に切る）
            float ToonSpecShape(float3 normal, float3 lightDir, float3 viewDir)
            {
                float3 h       = normalize(lightDir + viewDir);
                float  specRaw = pow(saturate(dot(normal, h)), _SpecGloss);

                float  w     = max(_SpecSoftness, fwidth(specRaw) * 0.5);
                float  shape = smoothstep(_SpecThreshold - w, _SpecThreshold + w, specRaw);

                shape *= step(0.0, dot(normal, lightDir)); // 裏面には出さない
                return shape;
            }

            // Burleyディフューズ（Disney PBR）
            float BurleyDiffuse(float NdotL, float NdotV, float roughness)
            {
                float FD90 = 0.5 + 2.0 * roughness * NdotL * NdotL;
                float FdV  = 1.0 + (FD90 - 1.0) * pow(1.0 - NdotV, 5.0);
                float FdL  = 1.0 + (FD90 - 1.0) * pow(1.0 - NdotL, 5.0);
                return FdV * FdL;
            }

            //   トライプラナー（ワールド座標）サンプリング
            //   3軸(zy/xz/xy)でサンプルし法線の向きでブレンド。スケール・向き・UVに依存しない。
            float4 SampleTriplanar(TEXTURE2D_PARAM(tex, samp), float3 wpos, float3 blend)
            {
                float4 cx = SAMPLE_TEXTURE2D(tex, samp, wpos.zy);
                float4 cy = SAMPLE_TEXTURE2D(tex, samp, wpos.xz);
                float4 cz = SAMPLE_TEXTURE2D(tex, samp, wpos.xy);
                return cx * blend.x + cy * blend.y + cz * blend.z;
            }

            // ワールド法線を返すトライプラナー法線（whiteoutブレンド）
            float3 SampleTriplanarNormal(TEXTURE2D_PARAM(tex, samp), float3 wpos,
                                         float3 wnormal, float3 blend, float strength)
            {
                float3 nx = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, wpos.zy));
                float3 ny = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, wpos.xz));
                float3 nz = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, wpos.xy));
                nx.xy *= strength; ny.xy *= strength; nz.xy *= strength;
                nx = float3(nx.xy + wnormal.zy, abs(nx.z) * wnormal.x);
                ny = float3(ny.xy + wnormal.xz, abs(ny.z) * wnormal.y);
                nz = float3(nz.xy + wnormal.xy, abs(nz.z) * wnormal.z);
                float3 n = nx.zyx * blend.x + ny.xzy * blend.y + nz.xyz * blend.z;
                return normalize(n);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                VertexNormalInputs   nrmInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);

                OUT.positionHCS  = posInputs.positionCS;
                OUT.positionWS   = posInputs.positionWS;
                OUT.normalWS     = nrmInputs.normalWS;
                OUT.tangentWS    = nrmInputs.tangentWS;
                OUT.bitangentWS  = nrmInputs.bitangentWS;
                OUT.uv           = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float4 dotColor = _Color;

                // 頂点法線（ドットサイズ・リム・トライプラナーのブレンド軸に使用）
                float3 vertexNormalWS = normalize(IN.normalWS);

                // ── テクスチャサンプル（メッシュUV ⇄ ワールドトライプラナー）──
                float4 baseTex;
                float  metalness, roughness, rawOcclusion, emMask;
                float3 emissionTex;
                float3 normalMapWS;

            #if defined(_WORLD_TRIPLANAR)
                // ワールド座標からタイリング（拡大/向きに依存しない）
                float3 wpos  = IN.positionWS * _WorldTexScale;
                float3 blend = pow(abs(vertexNormalWS), _TriplanarSharpness);
                blend /= max(blend.x + blend.y + blend.z, 1e-4);

                baseTex      = SampleTriplanar(TEXTURE2D_ARGS(_MainTex,      sampler_MainTex),      wpos, blend);
                metalness    = SampleTriplanar(TEXTURE2D_ARGS(_MetalnessMap, sampler_MetalnessMap), wpos, blend).r * _MetalnessScale;
                roughness    = SampleTriplanar(TEXTURE2D_ARGS(_RoughnessMap, sampler_RoughnessMap), wpos, blend).r * _RoughnessScale;
                rawOcclusion = SampleTriplanar(TEXTURE2D_ARGS(_OcclusionMap, sampler_OcclusionMap), wpos, blend).r;

                float3 emScroll = float3(_EmissionMaskScrollX, _EmissionMaskScrollY, 0) * _Time.y;
                emMask       = SampleTriplanar(TEXTURE2D_ARGS(_EmissionMaskMap, sampler_EmissionMaskMap), wpos + emScroll, blend).r;
                emissionTex  = SampleTriplanar(TEXTURE2D_ARGS(_EmissionMap,     sampler_EmissionMap),     wpos, blend).rgb;

                normalMapWS  = SampleTriplanarNormal(TEXTURE2D_ARGS(_NormalMap, sampler_NormalMap),
                                                     wpos, vertexNormalWS, blend, _NormalStrength);
            #else
                // 従来どおりメッシュUVでサンプル
                baseTex      = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);
                metalness    = SAMPLE_TEXTURE2D(_MetalnessMap, sampler_MetalnessMap, TRANSFORM_TEX(IN.uv, _MetalnessMap)).r * _MetalnessScale;
                roughness    = SAMPLE_TEXTURE2D(_RoughnessMap, sampler_RoughnessMap, TRANSFORM_TEX(IN.uv, _RoughnessMap)).r * _RoughnessScale;
                rawOcclusion = SAMPLE_TEXTURE2D(_OcclusionMap, sampler_OcclusionMap, TRANSFORM_TEX(IN.uv, _OcclusionMap)).r;

                float2 maskUV = TRANSFORM_TEX(IN.uv, _EmissionMaskMap)
                              + float2(_EmissionMaskScrollX, _EmissionMaskScrollY) * _Time.y;
                emMask       = SAMPLE_TEXTURE2D(_EmissionMaskMap, sampler_EmissionMaskMap, maskUV).r;
                emissionTex  = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, TRANSFORM_TEX(IN.uv, _EmissionMap)).rgb;

                float2 normalUV = TRANSFORM_TEX(IN.uv, _NormalMap);
                float3 normalTS = UnpackNormal(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUV));
                normalTS.xy    *= _NormalStrength;
                normalTS.z      = sqrt(saturate(1.0 - dot(normalTS.xy, normalTS.xy)));
                float3x3 TBN = float3x3(normalize(IN.tangentWS), normalize(IN.bitangentWS), vertexNormalWS);
                normalMapWS  = normalize(mul(normalTS, TBN));
            #endif

                roughness       = max(roughness, 0.04);
                float occlusion = lerp(1.0, rawOcclusion, _OcclusionStrength);

                float3 emission = emissionTex * _EmissionColor.rgb * _EmissionIntensity * emMask;

                float3 viewDir = normalize(GetCameraPositionWS() - IN.positionWS);

                // ── ライト方向取得（メインライト）───────────────
                //   カスケード縞対策: シャドウ座標は頂点ではなくfragでワールド位置から作る
                //   （URP Lit と同じ方式。頂点だとカスケード選択がズレて境界に縞が出る）
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light  mainLight  = GetMainLight(shadowCoord);
                float  shadow     = mainLight.shadowAttenuation; // 0=影, 1=影なし（なめらか）
                float3 lightDir   = normalize(mainLight.direction);
                float3 lightColor = mainLight.color;

                // ── NdotL / NdotV 2系統 ───────────────────────────
                //   vertexNdotL(ドット用)はメインは元どおり「向きだけ」。落ち影は含めない。
                //   光の強度反映は追加ライト側だけに掛ける（メインに掛けると基準の陰影がズレるため）。
                float vertexNdotL = dot(vertexNormalWS, lightDir);
                float vertexNdotV = saturate(dot(vertexNormalWS, viewDir));
                float normalNdotL = saturate(dot(normalMapWS, lightDir));
                float NdotV       = saturate(dot(normalMapWS, viewDir));

                // 追加ライト（Forward / Forward+ 両対応。LIGHT_LOOPマクロで回す）
                float3 additionalSpecular = float3(0, 0, 0);
                float3 addDiffuseLight    = float3(0, 0, 0); // 追加ライトの拡散光（色付き）

                // Forward+ はクラスタ探索に位置と画面UVが必要なので InputData をセット
                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionHCS);

                uint pixelLightCount = GetAdditionalLightsCount();
                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light  addLight = GetAdditionalLight(lightIndex, IN.positionWS);
                    float3 addDir   = normalize(addLight.direction);
                    float  atten    = addLight.distanceAttenuation * addLight.shadowAttenuation;
                    float  addN     = saturate(dot(normalMapWS, addDir));

                    // ドット寄与: 光の強度反映、距離のみ（影は含めない）
                    vertexNdotL     += lerp(1.0, Luminance(addLight.color), _LightDotScale)
                                     * saturate(dot(vertexNormalWS, addDir)) * addLight.distanceAttenuation;
                    normalNdotL     += addN * atten;                        // 陰影に寄与（影あり）
                    addDiffuseLight += addLight.color * addN * atten;       // 追加拡散（影なし）

                    float addShape = ToonSpecShape(normalMapWS, addDir, viewDir);
                    additionalSpecular += addLight.color * addShape * atten;
                LIGHT_LOOP_END
                vertexNdotL = saturate(vertexNdotL);
                normalNdotL = saturate(normalNdotL);

                // ── ノーマルマップ差分でテクスチャ陰影を計算 ─────────
                float normalDiff    = saturate(dot(vertexNormalWS, lightDir)) - normalNdotL;
                float normalShading = 1.0 + clamp(normalDiff * _NormalShading, -1.0, 1.0);
                float3 shadedTex    = baseTex.rgb * normalShading;

                // ── 環境光 × Occlusion ────────────────────────────
                //   HDRで環境光が跳ねてBloomが爆発するのを防ぐ:
                //   ・SampleSHに渡す法線を直前で確実に正規化（GPU差で長さがズレると異常値になる）
                //   ・環境光を _AmbientMax で上限クランプ（暴走だけ止める。他の演出は残す）
                float3 ambient = SampleSH(normalize(normalMapWS)) * occlusion;
                ambient = min(ambient, _AmbientMax.xxx);

                // ── Burleyディフューズ ────────────────────────────
                float burley = BurleyDiffuse(normalNdotL, NdotV, roughness);
                float3 kD    = (1.0 - metalness) * shadedTex * burley;

                // ── トゥーンスペキュラー（メイン/追加を分離）──────
                float  mainShape = ToonSpecShape(normalMapWS, lightDir, viewDir);
                float3 mainSpec  = lightColor * mainShape * _SpecColor.rgb * _SpecStrength;
                float3 addSpec   = additionalSpecular    * _SpecColor.rgb * _SpecStrength;

                //   複数ライト対応: メインの落ち影はメインの直接光だけに掛ける。
                //   追加ライト(フィル/ポイント)は影に関係なく照らす＝影の中でも消えない。
                //   環境光も影の影響を受けない＝影の中でも真っ黒にならない。
                float3 litColor = (kD + mainSpec) * shadow                    // メイン: 落ち影で暗く
                                + kD * addDiffuseLight * _AddLightDiffuse     // 追加拡散（色のせ量を調整可）
                                + addSpec                                     // 追加ライトのスペキュラー（影なし）
                                + ambient * baseTex.rgb * (1.0 - metalness);  // 環境光（影なし）

                // ── リムライト ────────────────────────────────────
                float rim     = 1.0 - vertexNdotV;
                float rimMask = smoothstep(1.0 - _RimThreshold,
                                           1.0 - _RimThreshold + _RimSmoothness, rim);
                rimMask      *= saturate(dot(vertexNormalWS, lightDir));
                litColor     += _RimColor.rgb * rimMask * _RimIntensity;

                litColor += emission;

                // ── ドットサイズ計算 ──────────────────────────────
                float dotInput = saturate(vertexNdotL - vertexNdotV * _ViewDotInfluence);
                float t = smoothstep(
                    _DotThreshold - _DotSmoothness,
                    _DotThreshold + _DotSmoothness + 1.0,
                    dotInput
                );
                float dotSize = lerp(_DotMin, _DotMax, t);

                // ── グリッド計算 ──────────────────────────────────
                // 光の向きでグリッド角度を回転（ワールドXZ投影＝面全体で一定）
                float  lightAngle = degrees(atan2(lightDir.z, lightDir.x));
                float  gridAngle  = _Angle + lightAngle * _LightAngleInfluence;

            #if defined(_SCREEN_SPACE_DOTS)
                // スクリーン空間: 画面ピクセル基準。UV/曲面に一切影響されず均一な丸ドット（漫画トーン）
                float2 dotCoord = IN.positionHCS.xy / max(_ScreenDotSize, 1.0);
            #else
                // メッシュUV基準（従来。床など）
                float2 dotCoord = IN.uv * _DotFreq;
            #endif

                float2 p    = Rot(dotCoord, gridAngle);
                float2 cell = p - floor(p) - 0.5;

                //   隙間対策: ドットが大きいほど「円→正方形(チェビシェフ距離)」に寄せる。
                //   円だと四隅が届かず隙間が残るが、正方形ならセルを完全に埋めて密着する。
                //   小さいときは丸のまま（sq≈0）。
                float distRound  = length(cell);
                float distSquare = max(abs(cell.x), abs(cell.y));
                float sq   = smoothstep(0.8, 1.3, dotSize); // 大きいほど正方形へ
                float dist = lerp(distRound, distSquare, sq);

                //   モアレ対策: 縁幅を画面解像度に合わせる。判定は連続な p の微分で行う
                //   （dist は境界でジャンプするため、そのfwidthを使うと境界に線が出る）。
                float aa   = (fwidth(p.x) + fwidth(p.y)) * 0.5 + 1e-5;
                float mask = 1.0 - smoothstep(dotSize * 0.5 - aa,
                                              dotSize * 0.5 + aa,
                                              dist);

                // 遠距離フェード（UVモードのみ。スクリーンモードは常に一定サイズなので不要）
                float cellFade = 1.0;
            #if !defined(_SCREEN_SPACE_DOTS)
                cellFade = saturate(1.0 - (fwidth(p.x) + fwidth(p.y)) * 1.5);
            #endif
                mask *= cellFade;

                mask = saturate(mask);

                // ── カラー合成 ────────────────────────────────────
                float3 bg   = litColor * _BgColor.rgb;
                float3 dotC = litColor * dotColor.rgb;
                float3 rgb   = lerp(bg, dotC, mask);

                // 全体透明度。Surface Type=Transparent のとき滑らかに透明化
                float alpha = lerp(_BgColor.a, dotColor.a, mask) * _Alpha;

                return float4(rgb, alpha);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }

    CustomEditor "HalftoneShaderGUI"

    FallBack "Universal Render Pipeline/Lit"
}
