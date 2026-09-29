using System.Collections.Generic;
using UnityEngine;

public sealed class AfterimageRenderer : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("残像として描画したいSkinnedMeshRenderer群の検索ルートです。未設定の場合、このコンポーネントが付いているTransform以下から検索します。Player本体ではなくMeshRootを指定すると扱いやすいです。")]
    [SerializeField] private Transform _rendererRoot;

    [Tooltip("trueにすると非アクティブな子オブジェクトに付いているSkinnedMeshRendererも検索対象に含めます。通常はfalseで問題ありません。")]
    [SerializeField] private bool _includeInactiveRenderers = false;

    [Tooltip("カメラ方向判定に使う基準Transformです。通常はPlayer本体、または向き制御しているルートTransformを指定してください。未設定の場合はRendererRootやSkinnedMeshRendererの向きを基準にします。")]
    [SerializeField] private Transform _directionReferenceRoot;

    [Header("Afterimage Material")]
    [Tooltip("残像描画に使用するマテリアルです。未設定の場合は各SkinnedMeshRendererのsharedMaterialを使用します。基本的には残像専用の半透明マテリアルを指定してください。")]
    [SerializeField] private Material _afterimageMaterial;

    [Header("Spawn")]
    [Tooltip("残像を生成する間隔です。値が小さいほど密度の高い残像になりますが、BakeMeshの回数が増えるため負荷も上がります。")]
    [SerializeField, Min(0.001f)] private float _spawnInterval = 0.035f;

    [Tooltip("生成された残像が残り続ける時間です。長くすると残像が長く残り、短くするとすぐ消えます。")]
    [SerializeField, Min(0.01f)] private float _lifeTime = 0.18f;

    [Tooltip("同時に存在できる残像スナップショットの最大数です。多いほど残像を多く残せますが、描画負荷とメモリ使用量が増えます。0以下の場合は上限判定を行いません。")]
    [SerializeField] private int _maxActiveSnapshots = 16;

    [Header("Scale")]
    [Tooltip("残像全体に掛ける見た目上の倍率です。1で通常サイズ、1.1で少し大きく、0.9で少し小さく表示されます。MeshRoot側のスケール二重適用対策とは別の、残像専用の調整値です。")]
    [SerializeField, Min(0.0f)] private float _afterimageScaleMultiplier = 1.0f;

    [Tooltip("trueにすると、残像が透明になっていくのに合わせてサイズも小さくなります。タックルの軌跡を自然に消したい場合に有効です。")]
    [SerializeField] private bool _shrinkWithFade = false;

    [Tooltip("Shrink With Fadeがtrueのとき、透明化が進んだ最後のサイズ割合です。0なら最後はほぼ0サイズ、0.3なら元サイズの30%まで縮小します。")]
    [SerializeField, Range(0.0f, 1.0f)] private float _minimumFadeScaleRate = 0.0f;

    [Header("Camera Adaptive Rendering")]
    [Tooltip("残像の見え方を調整するために参照するカメラです。未設定の場合はCamera.mainを自動取得します。プレイヤー追従カメラを明示的に指定するのがおすすめです。")]
    [SerializeField] private Transform _cameraTransform;

    [Tooltip("trueにすると、カメラとの距離に応じて残像の透明度を変えます。カメラがプレイヤーに近いときに残像が画面を覆って見づらい場合に有効です。")]
    [SerializeField] private bool _useCameraDistanceFade = true;

    [Tooltip("この距離以下では残像の透明度をもっとも弱くします。カメラとプレイヤーがかなり近い状態で残像を薄くしたい場合に調整してください。")]
    [SerializeField, Min(0.001f)] private float _closeFadeDistance = 1.5f;

    [Tooltip("この距離以上では距離による透明度低下を行わず、通常の濃さで表示します。Close Fade Distanceより大きい値にしてください。")]
    [SerializeField, Min(0.001f)] private float _fullVisibleDistance = 4.0f;

    [Tooltip("カメラがClose Fade Distance以下まで近づいたときの残像透明度の倍率です。0ならほぼ見えなくなり、0.3なら30%の濃さで残ります。")]
    [SerializeField, Range(0.0f, 1.0f)] private float _closeDistanceAlphaRate = 0.25f;

    [Tooltip("trueにすると、カメラが近いときに残像のサイズも小さくします。背後カメラで残像が画面いっぱいに広がる場合に有効です。")]
    [SerializeField] private bool _scaleDownWhenCloseToCamera = true;

    [Tooltip("カメラがClose Fade Distance以下まで近づいたときの残像サイズ倍率です。0.5なら近距離では半分サイズになります。")]
    [SerializeField, Range(0.0f, 1.0f)] private float _closeDistanceScaleRate = 0.65f;

    [Header("Back View Fade")]
    [Tooltip("trueにすると、カメラがプレイヤーの背後方向に近いときだけ残像を薄くします。水平背後カメラで残像が見づらい場合に有効です。")]
    [SerializeField] private bool _useBackViewFade = true;

    [Tooltip("trueにすると、背後判定でY方向を無視してXZ平面だけで判定します。水平に背後から追うカメラではtrue推奨です。")]
    [SerializeField] private bool _useHorizontalDirectionOnly = true;

    [Tooltip("もっとも背後カメラ扱いにする内積値です。-1に近いほど完全に真後ろです。通常は-0.85前後がおすすめです。")]
    [SerializeField, Range(-1.0f, 1.0f)] private float _strongestBackViewDot = -0.85f;

    [Tooltip("この内積値以上なら背後カメラによる透明度低下をほぼ行いません。通常は-0.25前後がおすすめです。")]
    [SerializeField, Range(-1.0f, 1.0f)] private float _fullVisibleBackViewDot = -0.25f;

    [Tooltip("カメラがもっとも背後方向に近いときの残像透明度倍率です。0ならほぼ消え、0.4なら40%の濃さで残ります。")]
    [SerializeField, Range(0.0f, 1.0f)] private float _backViewAlphaRate = 0.35f;

    [Header("Time")]
    [Tooltip("trueにするとTime.timeScaleの影響を受けないunscaledDeltaTimeで残像を更新します。ポーズ中や独自タイムスケール中でも残像を通常時間で消したい場合に使います。")]
    [SerializeField] private bool _useUnscaledTime = false;

    [Header("Debug")]
    [Tooltip("trueにするとStart時点で自動的に残像発生を開始します。タックル処理に接続する前の見た目確認用です。本番ではfalse推奨です。")]
    [SerializeField] private bool _emitOnStartForTest = false;

    private SkinnedMeshRenderer[] _renderers;

    private readonly Stack<AfterImageSnapshot> _pool = new Stack<AfterImageSnapshot>();
    private readonly List<AfterImageSnapshot> _activeSnapshots = new List<AfterImageSnapshot>();

    private bool _isEmitting;
    private float _spawnTimer;

    private TimeAgent timeAgent;

    public bool IsEmitting => _isEmitting;

    private void Awake()
    {
        RefreshRenderers();
        ResolveCameraIfNeeded();

        timeAgent = GetComponent<TimeAgent>();
    }

    private void Start()
    {
        if (_emitOnStartForTest)
        {
            BeginAfterimage();
        }
    }

    private void LateUpdate()
    {
        ResolveCameraIfNeeded();

        float deltaTime = _useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        if (timeAgent != null)
        {
            deltaTime *= timeAgent.TimeScale;
        }

        if (deltaTime <= 0.0f)
        {
            return;
        }

        if (_isEmitting)
        {
            UpdateEmission(deltaTime);
        }

        UpdateAndRenderSnapshots(deltaTime);
    }

    private void OnDestroy()
    {
        for (int i = 0; i < _activeSnapshots.Count; i++)
        {
            _activeSnapshots[i]?.Dispose();
        }

        while (_pool.Count > 0)
        {
            _pool.Pop()?.Dispose();
        }

        _activeSnapshots.Clear();
    }

    /// <summary>
    /// 残像化対象のSkinnedMeshRendererを再取得する。
    /// モデル差し替え、衣装差し替え、RendererRoot変更後などに呼ぶ。
    /// </summary>
    public void RefreshRenderers()
    {
        Transform root = _rendererRoot != null ? _rendererRoot : transform;
        _renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(_includeInactiveRenderers);
    }

    /// <summary>
    /// 残像の継続発生を開始する。
    /// タックル開始時やチャージタックル開始時など、状態に入った瞬間に1回だけ呼ぶ想定。
    /// </summary>
    public void BeginAfterimage()
    {
        if (_renderers == null || _renderers.Length == 0)
        {
            RefreshRenderers();
        }

        _isEmitting = true;
        _spawnTimer = 0.0f;

        EmitOnce();
    }

    /// <summary>
    /// 残像の継続発生を停止する。
    /// 既に生成済みの残像はLifeTimeに従って自然に消える。
    /// </summary>
    public void EndAfterimage()
    {
        _isEmitting = false;
        _spawnTimer = 0.0f;
    }

    /// <summary>
    /// 残像の発生を停止し、現在残っている残像も即座に消す。
    /// 死亡、リスポーン、強制キャンセル、シーン遷移などで使用する。
    /// </summary>
    public void ClearAfterimage()
    {
        _isEmitting = false;
        _spawnTimer = 0.0f;

        for (int i = _activeSnapshots.Count - 1; i >= 0; i--)
        {
            AfterImageSnapshot snapshot = _activeSnapshots[i];
            snapshot.Reset();
            _pool.Push(snapshot);
            _activeSnapshots.RemoveAt(i);
        }
    }

    /// <summary>
    /// 現在の姿勢を1枚だけ残像として生成する。
    /// 継続発生ではなく、アニメーションイベントなどから単発で出したい場合にも使用できる。
    /// </summary>
    public void EmitOnce()
    {
        if (_renderers == null || _renderers.Length == 0)
        {
            return;
        }

        if (_maxActiveSnapshots > 0 && _activeSnapshots.Count >= _maxActiveSnapshots)
        {
            AfterImageSnapshot oldest = _activeSnapshots[0];
            _activeSnapshots.RemoveAt(0);
            oldest.Reset();
            _pool.Push(oldest);
        }

        AfterImageSnapshot snapshot = GetSnapshot();

        snapshot.Capture(
            _renderers,
            _afterimageMaterial,
            _lifeTime,
            _afterimageScaleMultiplier,
            _shrinkWithFade,
            _minimumFadeScaleRate,
            _directionReferenceRoot != null ? _directionReferenceRoot : transform
        );

        _activeSnapshots.Add(snapshot);
    }

    private void UpdateEmission(float deltaTime)
    {
        _spawnTimer -= deltaTime;

        if (_spawnTimer > 0.0f)
        {
            return;
        }

        EmitOnce();
        _spawnTimer = Mathf.Max(0.001f, _spawnInterval);
    }

    private void UpdateAndRenderSnapshots(float deltaTime)
    {
        for (int i = _activeSnapshots.Count - 1; i >= 0; i--)
        {
            AfterImageSnapshot snapshot = _activeSnapshots[i];

            snapshot.Tick(deltaTime);

            if (!snapshot.IsAlive)
            {
                snapshot.Reset();
                _pool.Push(snapshot);
                _activeSnapshots.RemoveAt(i);
                continue;
            }

            snapshot.Render(
                _cameraTransform,
                _useCameraDistanceFade,
                _closeFadeDistance,
                _fullVisibleDistance,
                _closeDistanceAlphaRate,
                _scaleDownWhenCloseToCamera,
                _closeDistanceScaleRate,
                _useBackViewFade,
                _directionReferenceRoot != null ? _directionReferenceRoot : transform,
                _useHorizontalDirectionOnly,
                _strongestBackViewDot,
                _fullVisibleBackViewDot,
                _backViewAlphaRate
            );
        }
    }

    private AfterImageSnapshot GetSnapshot()
    {
        if (_pool.Count > 0)
        {
            AfterImageSnapshot snapshot = _pool.Pop();

            if (snapshot != null && snapshot.SlotCount == _renderers.Length)
            {
                return snapshot;
            }

            snapshot?.Dispose();
        }

        return new AfterImageSnapshot(_renderers.Length);
    }

    private void ResolveCameraIfNeeded()
    {
        if (_cameraTransform != null)
        {
            return;
        }

        Camera mainCamera = Camera.main;

        if (mainCamera != null)
        {
            _cameraTransform = mainCamera.transform;
        }
    }
}