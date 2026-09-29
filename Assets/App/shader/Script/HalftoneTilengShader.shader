Shader "Custom/URP/HalftoneLitTiling"
{
    Properties
    {
        [Header(Surface Options)]
        [Enum(Opaque,0,Transparent,1)] _Surface ("Surface Type", Float) = 0
        [Enum(Both,0,Back,1,Front,2)]  _Cull    ("Render Face", Float)  = 2

        [HideInInspector] _SrcBlend ("__src", Float) = 1
        [HideInInspector] _DstBlend ("__dst", Float) = 0
        [HideInInspector] _ZWrite   ("__zw",  Float) = 1

        [Header(Texture)]
        _MainTex  ("Texture", 2D) = "white" {}

        [Header(Auto Tiling)]
        _AutoTileBaseScaleX ("Auto Tile Base Scale X", Float) = 10.0
        _AutoTileBaseScaleY ("Auto Tile Base Scale Y", Float) = 10.0
        _AutoTileBaseScaleZ ("Auto Tile Base Scale Z", Float) = 10.0

        [Header(World Tiling)]
        [Toggle(_WORLD_TRIPLANAR)] _WorldTriplanar ("World-Space Tiling (Triplanar)", Float) = 0
        _WorldTexScale      ("World Tiling Scale", Float)               = 1.0
        _TriplanarSharpness ("Triplanar Blend Sharpness", Range(1, 16)) = 4.0

        [Header(Normal Map)]
        _NormalMap      ("Normal Map", 2D)                         = "bump" {}
        _NormalStrength ("Normal Strength", Range(0, 2))           = 1.0
        _NormalShading  ("Normal Shading Intensity", Range(0, 2))  = 1.0

        [Header(PBR Maps)]
        _MetalnessMap   ("Metalness Map", 2D)             = "black" {}
        _RoughnessMap   ("Roughness Map", 2D)             = "white" {}
        _MetalnessScale ("Metalness Scale", Range(0, 1))  = 1.0
        _RoughnessScale ("Roughness Scale", Range(0, 1))  = 1.0

        [Header(Toon Specular)]
        _SpecColor     ("Specular Color", Color)                          = (1,1,1,1)
        _SpecStrength  ("Specular Strength", Range(0, 2))                 = 1.0
        _SpecGloss     ("Specular Size (higher = smaller)", Range(1,256)) = 40.0
        _SpecThreshold ("Specular Threshold", Range(0, 1))                = 0.5
        _SpecSoftness  ("Specular Edge Softness", Range(0.001, 0.5))      = 0.02

        [Header(Occlusion)]
        _OcclusionMap      ("Occlusion Map", 2D)               = "white" {}
        _OcclusionStrength ("Occlusion Strength", Range(0, 1)) = 1.0

        [Header(Emission)]
        _EmissionMap       ("Emission Map", 2D)                   = "black" {}
        [HDR]
        _EmissionColor     ("Emission Color", Color)              = (0,0,0,1)
        _EmissionIntensity ("Emission Intensity", Range(0, 10))   = 5.0

        _EmissionMaskMap     ("Emission Mask Map", 2D) = "white" {}
        _EmissionMaskScrollX ("Mask Scroll X", Float)  = 0.0
        _EmissionMaskScrollY ("Mask Scroll Y", Float)  = 0.0

        [Header(Rim Light)]
        _RimColor      ("Rim Color", Color)                  = (1,1,1,1)
        _RimThreshold  ("Rim Threshold", Range(0, 1))        = 0.2
        _RimSmoothness ("Rim Smoothness", Range(0.001,0.2))  = 0.05
        _RimIntensity  ("Rim Intensity", Range(0, 2))        = 1.0

        [Header(Halftone)]
        _DotFreq          ("Dot Frequency (dots per UV)", Float)          = 20.0
        _DotMin           ("Dot Size (shadow areas)", Range(0.00, 5.0))   = 0.01
        _DotMax           ("Dot Size (lit areas)", Range(0.0, 2.0))       = 1.5
        _Angle            ("Grid Angle (deg)", Range(-90, 90))            = 45.0
        _DotThreshold     ("Dot Threshold", Range(-1, 1))                 = 0.0
        _DotSmoothness    ("Dot Smoothness", Range(-0.5, 1))              = -0.2
        _ViewDotInfluence ("View Angle Dot Influence", Range(0, 1))       = 0.3

        _AddLightDiffuse      ("Additional Light Diffuse (0=dots only)", Range(0, 1)) = 1.0
        _LightDotScale        ("Light Intensity to Dot Size", Range(0, 1))             = 1.0
        _LightAngleInfluence  ("Light Dir to Grid Angle", Range(0, 1))                 = 1.0

        [Toggle(_SCREEN_SPACE_DOTS)] _ScreenSpaceDots ("Screen-Space Dots", Float) = 0
        _ScreenDotSize ("Screen Dot Size (px)", Float) = 8.0

        [Header(BaseColor)]
        _BgColor ("Base Color", Color) = (1,1,1,1)
        _Color   ("Dot Color", Color)  = (0,0,0,1)
        _Alpha   ("Alpha (Opacity)", Range(0, 1)) = 1.0
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

                float  _AutoTileBaseScaleX;
                float  _AutoTileBaseScaleY;
                float  _AutoTileBaseScaleZ;

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

                // 元シェーダー互換UV。
                // Normal / PBR / Halftone などは基本こちらを使う。
                float2 uv          : TEXCOORD0;

                // 自動タイリングUV。
                // 通常UVモード時の MainTex / Emission / EmissionMask に使う。
                float2 tiledUV     : TEXCOORD1;

                float3 normalWS    : TEXCOORD2;
                float3 tangentWS   : TEXCOORD3;
                float3 bitangentWS : TEXCOORD4;
                float3 positionWS  : TEXCOORD5;
            };

            float2 Rot(float2 p, float deg)
            {
                float r = deg * (PI / 180.0);
                float s = sin(r);
                float c = cos(r);
                return float2(c * p.x - s * p.y, s * p.x + c * p.y);
            }

            float3 GetObjectWorldScale()
            {
                float3 scale;

                scale.x = length(float3(
                    unity_ObjectToWorld._m00,
                    unity_ObjectToWorld._m10,
                    unity_ObjectToWorld._m20
                ));

                scale.y = length(float3(
                    unity_ObjectToWorld._m01,
                    unity_ObjectToWorld._m11,
                    unity_ObjectToWorld._m21
                ));

                scale.z = length(float3(
                    unity_ObjectToWorld._m02,
                    unity_ObjectToWorld._m12,
                    unity_ObjectToWorld._m22
                ));

                return scale;
            }

            // ------------------------------------------------------------
            // 自動タイリングUV
            //
            // 旧HalftoneLitTilingと同じ思想。
            // 既存メッシュUVを拡大するだけで、頂点座標は変更しない。
            //
            // 注意:
            // 親に非均一Scaleがあり、その子孫をRotationさせると
            // Transform階層側でシアー変形が出る。
            // これはシェーダーではなくTransform行列の問題。
            // ------------------------------------------------------------
            float2 GetAutoTiledUV(float2 uv, float3 normalOS)
            {
                float3 objectScale = GetObjectWorldScale();

                float3 baseScale = float3(
                    max(_AutoTileBaseScaleX, 0.0001),
                    max(_AutoTileBaseScaleY, 0.0001),
                    max(_AutoTileBaseScaleZ, 0.0001)
                );

                float3 tileScale = objectScale / baseScale;

                float3 absNormal = abs(normalize(normalOS));

                float2 faceTile = float2(1.0, 1.0);

                // ローカルX面、左右面。
                // 面上のUVには Z/Y のスケールを使う。
                if (absNormal.x >= absNormal.y && absNormal.x >= absNormal.z)
                {
                    faceTile = float2(tileScale.z, tileScale.y);
                }
                // ローカルY面、上下面。
                // 面上のUVには X/Z のスケールを使う。
                else if (absNormal.y >= absNormal.x && absNormal.y >= absNormal.z)
                {
                    faceTile = float2(tileScale.x, tileScale.z);
                }
                // ローカルZ面、前後面。
                // 面上のUVには X/Y のスケールを使う。
                else
                {
                    faceTile = float2(tileScale.x, tileScale.y);
                }

                return uv * faceTile;
            }

            // トゥーン用ステップスペキュラー
            float ToonSpecShape(float3 normal, float3 lightDir, float3 viewDir)
            {
                float3 h       = normalize(lightDir + viewDir);
                float  specRaw = pow(saturate(dot(normal, h)), _SpecGloss);

                float  w     = max(_SpecSoftness, fwidth(specRaw) * 0.5);
                float  shape = smoothstep(_SpecThreshold - w, _SpecThreshold + w, specRaw);

                shape *= step(0.0, dot(normal, lightDir));
                return shape;
            }

            // Burleyディフューズ
            float BurleyDiffuse(float NdotL, float NdotV, float roughness)
            {
                float FD90 = 0.5 + 2.0 * roughness * NdotL * NdotL;
                float FdV  = 1.0 + (FD90 - 1.0) * pow(1.0 - NdotV, 5.0);
                float FdL  = 1.0 + (FD90 - 1.0) * pow(1.0 - NdotL, 5.0);
                return FdV * FdL;
            }

            // トライプラナーサンプル
            float4 SampleTriplanar(TEXTURE2D_PARAM(tex, samp), float3 wpos, float3 blend)
            {
                float4 cx = SAMPLE_TEXTURE2D(tex, samp, wpos.zy);
                float4 cy = SAMPLE_TEXTURE2D(tex, samp, wpos.xz);
                float4 cz = SAMPLE_TEXTURE2D(tex, samp, wpos.xy);
                return cx * blend.x + cy * blend.y + cz * blend.z;
            }

            // トライプラナー法線
            float3 SampleTriplanarNormal(TEXTURE2D_PARAM(tex, samp), float3 wpos, float3 wnormal, float3 blend, float strength)
            {
                float3 nx = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, wpos.zy));
                float3 ny = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, wpos.xz));
                float3 nz = UnpackNormal(SAMPLE_TEXTURE2D(tex, samp, wpos.xy));

                nx.xy *= strength;
                ny.xy *= strength;
                nz.xy *= strength;

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

                // 頂点位置は新規HalftoneLitと同じ。
                // ここではメッシュ形状を変更しない。
                OUT.positionHCS  = posInputs.positionCS;
                OUT.positionWS   = posInputs.positionWS;
                OUT.normalWS     = nrmInputs.normalWS;
                OUT.tangentWS    = nrmInputs.tangentWS;
                OUT.bitangentWS  = nrmInputs.bitangentWS;

                // 元シェーダー互換UV。
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);

                // 自動タイリングUV。
                // 通常UVモード時の MainTex / Emission / EmissionMask に使用。
                float2 autoTiledUV = GetAutoTiledUV(IN.uv, IN.normalOS);
                OUT.tiledUV = TRANSFORM_TEX(autoTiledUV, _MainTex);

                return OUT;
            }

            float4 frag(Varyings IN) : SV_Target
            {
                float4 dotColor = _Color;

                // 頂点法線
                float3 vertexNormalWS = normalize(IN.normalWS);

                // ── テクスチャサンプル ────────────────────────────
                float4 baseTex;
                float  metalness;
                float  roughness;
                float  rawOcclusion;
                float  emMask;
                float3 emissionTex;
                float3 normalMapWS;

            #if defined(_WORLD_TRIPLANAR)
                // ワールド座標からタイリング。
                // このモードでは新規HalftoneLitと同じくトライプラナーを優先する。
                float3 wpos  = IN.positionWS * _WorldTexScale;
                float3 blend = pow(abs(vertexNormalWS), _TriplanarSharpness);
                blend /= max(blend.x + blend.y + blend.z, 1e-4);

                baseTex      = SampleTriplanar(TEXTURE2D_ARGS(_MainTex, sampler_MainTex), wpos, blend);
                metalness    = SampleTriplanar(TEXTURE2D_ARGS(_MetalnessMap, sampler_MetalnessMap), wpos, blend).r * _MetalnessScale;
                roughness    = SampleTriplanar(TEXTURE2D_ARGS(_RoughnessMap, sampler_RoughnessMap), wpos, blend).r * _RoughnessScale;
                rawOcclusion = SampleTriplanar(TEXTURE2D_ARGS(_OcclusionMap, sampler_OcclusionMap), wpos, blend).r;

                float3 emScroll = float3(_EmissionMaskScrollX, _EmissionMaskScrollY, 0.0) * _Time.y;
                emMask      = SampleTriplanar(TEXTURE2D_ARGS(_EmissionMaskMap, sampler_EmissionMaskMap), wpos + emScroll, blend).r;
                emissionTex = SampleTriplanar(TEXTURE2D_ARGS(_EmissionMap, sampler_EmissionMap), wpos, blend).rgb;

                normalMapWS = SampleTriplanarNormal(
                    TEXTURE2D_ARGS(_NormalMap, sampler_NormalMap),
                    wpos,
                    vertexNormalWS,
                    blend,
                    _NormalStrength
                );
            #else
                // 通常UVモード。
                // MainTex / Emission / EmissionMask は自動タイリングUV。
                // Normal / PBR / Occlusion は元UV。
                baseTex = SAMPLE_TEXTURE2D(
                    _MainTex,
                    sampler_MainTex,
                    IN.tiledUV
                );

                metalness = SAMPLE_TEXTURE2D(
                    _MetalnessMap,
                    sampler_MetalnessMap,
                    TRANSFORM_TEX(IN.uv, _MetalnessMap)
                ).r * _MetalnessScale;

                roughness = SAMPLE_TEXTURE2D(
                    _RoughnessMap,
                    sampler_RoughnessMap,
                    TRANSFORM_TEX(IN.uv, _RoughnessMap)
                ).r * _RoughnessScale;

                rawOcclusion = SAMPLE_TEXTURE2D(
                    _OcclusionMap,
                    sampler_OcclusionMap,
                    TRANSFORM_TEX(IN.uv, _OcclusionMap)
                ).r;

                float2 maskUV = TRANSFORM_TEX(IN.tiledUV, _EmissionMaskMap)
                              + float2(_EmissionMaskScrollX, _EmissionMaskScrollY) * _Time.y;

                emMask = SAMPLE_TEXTURE2D(
                    _EmissionMaskMap,
                    sampler_EmissionMaskMap,
                    maskUV
                ).r;

                emissionTex = SAMPLE_TEXTURE2D(
                    _EmissionMap,
                    sampler_EmissionMap,
                    TRANSFORM_TEX(IN.tiledUV, _EmissionMap)
                ).rgb;

                float2 normalUV = TRANSFORM_TEX(IN.uv, _NormalMap);

                float3 normalTS = UnpackNormal(
                    SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, normalUV)
                );

                normalTS.xy *= _NormalStrength;
                normalTS.z   = sqrt(saturate(1.0 - dot(normalTS.xy, normalTS.xy)));

                float3x3 TBN = float3x3(
                    normalize(IN.tangentWS),
                    normalize(IN.bitangentWS),
                    vertexNormalWS
                );

                normalMapWS = normalize(mul(normalTS, TBN));
            #endif

                roughness = max(roughness, 0.04);

                float occlusion = lerp(1.0, rawOcclusion, _OcclusionStrength);
                float3 emission = emissionTex * _EmissionColor.rgb * _EmissionIntensity * emMask;

                float3 viewDir = normalize(GetCameraPositionWS() - IN.positionWS);

                // ── メインライト ────────────────────────────────
                float4 shadowCoord = TransformWorldToShadowCoord(IN.positionWS);
                Light  mainLight   = GetMainLight(shadowCoord);

                float  shadow     = mainLight.shadowAttenuation;
                float3 lightDir   = normalize(mainLight.direction);
                float3 lightColor = mainLight.color;

                // ── NdotL / NdotV ───────────────────────────────
                float vertexNdotL = dot(vertexNormalWS, lightDir);
                float vertexNdotV = saturate(dot(vertexNormalWS, viewDir));
                float normalNdotL = saturate(dot(normalMapWS, lightDir));
                float NdotV       = saturate(dot(normalMapWS, viewDir));

                // ── 追加ライト ─────────────────────────────────
                float3 additionalSpecular = float3(0, 0, 0);
                float3 addDiffuseLight    = float3(0, 0, 0);

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalMapWS;
                inputData.viewDirectionWS = viewDir;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionHCS);

                uint pixelLightCount = GetAdditionalLightsCount();

                LIGHT_LOOP_BEGIN(pixelLightCount)
                    Light addLight = GetAdditionalLight(lightIndex, IN.positionWS);
                    float3 addDir  = normalize(addLight.direction);
                    float atten     = addLight.distanceAttenuation * addLight.shadowAttenuation;
                    float addN      = saturate(dot(normalMapWS, addDir));

                    vertexNdotL += lerp(1.0, Luminance(addLight.color), _LightDotScale)
                                  * saturate(dot(vertexNormalWS, addDir))
                                  * addLight.distanceAttenuation;

                    normalNdotL += addN * atten;
                    addDiffuseLight += addLight.color * addN * atten;

                    float addShape = ToonSpecShape(normalMapWS, addDir, viewDir);
                    additionalSpecular += addLight.color * addShape * atten;
                LIGHT_LOOP_END

                vertexNdotL = saturate(vertexNdotL);
                normalNdotL = saturate(normalNdotL);

                // ── ノーマルマップ差分でテクスチャ陰影を計算 ─────
                float normalDiff    = saturate(dot(vertexNormalWS, lightDir)) - normalNdotL;
                float normalShading = 1.0 + clamp(normalDiff * _NormalShading, -1.0, 1.0);
                float3 shadedTex    = baseTex.rgb * normalShading;

                // ── 環境光 × Occlusion ──────────────────────────
                float3 ambient = SampleSH(normalMapWS) * occlusion;

                // ── Burleyディフューズ ──────────────────────────
                float burley = BurleyDiffuse(normalNdotL, NdotV, roughness);
                float3 kD    = (1.0 - metalness) * shadedTex * burley;

                // ── トゥーンスペキュラー ───────────────────────
                float mainShape = ToonSpecShape(normalMapWS, lightDir, viewDir);
                float3 mainSpec = lightColor * mainShape * _SpecColor.rgb * _SpecStrength;
                float3 addSpec  = additionalSpecular * _SpecColor.rgb * _SpecStrength;

                // メインライトは落ち影を受ける。
                // 追加ライトと環境光は影内でも残す。
                float3 litColor = (kD + mainSpec) * shadow
                                + kD * addDiffuseLight * _AddLightDiffuse
                                + addSpec
                                + ambient * baseTex.rgb * (1.0 - metalness);

                // ── リムライト ─────────────────────────────────
                float rim = 1.0 - vertexNdotV;

                float rimMask = smoothstep(
                    1.0 - _RimThreshold,
                    1.0 - _RimThreshold + _RimSmoothness,
                    rim
                );

                rimMask *= saturate(dot(vertexNormalWS, lightDir));
                litColor += _RimColor.rgb * rimMask * _RimIntensity;

                // Emission加算
                litColor += emission;

                // ── ドットサイズ計算 ────────────────────────────
                float dotInput = saturate(vertexNdotL - vertexNdotV * _ViewDotInfluence);

                float t = smoothstep(
                    _DotThreshold - _DotSmoothness,
                    _DotThreshold + _DotSmoothness + 1.0,
                    dotInput
                );

                float dotSize = lerp(_DotMin, _DotMax, t);

                // ── グリッド角度 ────────────────────────────────
                float lightAngle = degrees(atan2(lightDir.z, lightDir.x));
                float gridAngle  = _Angle + lightAngle * _LightAngleInfluence;

            #if defined(_SCREEN_SPACE_DOTS)
                float2 dotCoord = IN.positionHCS.xy / max(_ScreenDotSize, 1.0);
            #else
                float2 dotCoord = IN.uv * _DotFreq;
            #endif

                float2 p    = Rot(dotCoord, gridAngle);
                float2 cell = p - floor(p) - 0.5;

                float distRound  = length(cell);
                float distSquare = max(abs(cell.x), abs(cell.y));
                float sq         = smoothstep(0.8, 1.3, dotSize);
                float dist       = lerp(distRound, distSquare, sq);

                float aa = (fwidth(p.x) + fwidth(p.y)) * 0.5 + 1e-5;

                float mask = 1.0 - smoothstep(
                    dotSize * 0.5 - aa,
                    dotSize * 0.5 + aa,
                    dist
                );

                float cellFade = 1.0;

            #if !defined(_SCREEN_SPACE_DOTS)
                cellFade = saturate(1.0 - (fwidth(p.x) + fwidth(p.y)) * 1.5);
            #endif

                mask *= cellFade;
                mask = saturate(mask);

                // ── カラー合成 ─────────────────────────────────
                float3 bg   = litColor * _BgColor.rgb;
                float3 dotC = litColor * dotColor.rgb;
                float3 rgb  = lerp(bg, dotC, mask);

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