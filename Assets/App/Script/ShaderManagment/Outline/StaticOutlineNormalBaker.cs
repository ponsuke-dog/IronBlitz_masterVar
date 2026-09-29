using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────
// 【静的メッシュ用】スムース法線ベイカー
//   ・親(ルート)に付けると子の MeshFilter を全部自動で処理する
//   ・LitとOutlineで同じメッシュを共有するため、NORMALは触らず UV3 に焼く
//   ・[ExecuteAlways] なので Play しなくても Edit Mode で反映される
//
//   置き場所: 通常のスクリプトフォルダ(例 Assets/Scripts/)。Editorフォルダではない
//   ※ 対象モデルの Import Settings で「Read/Write Enabled」をONにすること
//     (ランタイムでも頂点を再計算するため。Edit Modeのみなら不要)
//   ※ Outline用マテリアルは _USE_SMOOTH_NORMAL を「ON」にして element1 に割り当てる
// ─────────────────────────────────────────────────────────────
[ExecuteAlways]
[DisallowMultipleComponent]
public class StaticOutlineNormalBaker : MonoBehaviour
{
    [Tooltip("同じ位置の頂点とみなす丸め桁数。3で約1mm単位")]
    public int roundDigits = 3;

    const string CloneSuffix = " (SmoothBaked)";

    // 復元用に「元メッシュ／生成クローン」を覚えておく
    [System.Serializable]
    struct Entry { public MeshFilter filter; public Mesh original; public Mesh clone; }
    [SerializeField, HideInInspector] List<Entry> _entries = new List<Entry>();

    void OnEnable() { Restore(); Bake(); }
    void OnDisable() { Restore(); }

    [ContextMenu("Rebake Now")]
    public void Rebake() { Restore(); Bake(); }

    void Bake()
    {
        // 同じ元メッシュを共有するブロックはクローンも共有して無駄を省く
        var cloneCache = new Dictionary<Mesh, Mesh>();

        var filters = GetComponentsInChildren<MeshFilter>(true);
        foreach (var mf in filters)
        {
            Mesh src = mf.sharedMesh;
            if (src == null) continue;
            if (src.name.EndsWith(CloneSuffix)) continue; // 既にクローン → スキップ

            if (!cloneCache.TryGetValue(src, out Mesh clone))
            {
                clone = Instantiate(src);
                clone.name = src.name + CloneSuffix;
                clone.hideFlags = HideFlags.DontSave; // シーン/アセットに保存しない

                Vector3[] smooth = ComputeSmoothNormals(clone, roundDigits);
                clone.SetUVs(3, new List<Vector3>(smooth)); // UV3へ格納
                clone.UploadMeshData(false);                // GPUへ反映(読み取り可のまま)

                cloneCache.Add(src, clone);
            }

            mf.sharedMesh = clone;
            _entries.Add(new Entry { filter = mf, original = src, clone = clone });
        }
    }

    void Restore()
    {
        foreach (var e in _entries)
        {
            if (e.filter != null && e.original != null)
                e.filter.sharedMesh = e.original;
            SafeDestroy(e.clone); // 共有クローンは2回目以降 null扱いで自動スキップ
        }
        _entries.Clear();
    }

    // 同一位置の頂点の法線を平均化してスムース法線を作る
    static Vector3[] ComputeSmoothNormals(Mesh mesh, int digits)
    {
        Vector3[] verts = mesh.vertices;
        Vector3[] normals = mesh.normals;
        float scale = Mathf.Pow(10f, digits);

        var map = new Dictionary<Vector3, Vector3>(); // 丸めた位置 → 法線合計
        var keys = new Vector3[verts.Length];

        for (int i = 0; i < verts.Length; i++)
        {
            // float誤差で一致判定が崩れないよう丸めた値をキーにする
            Vector3 k = new Vector3(
                Mathf.Round(verts[i].x * scale) / scale,
                Mathf.Round(verts[i].y * scale) / scale,
                Mathf.Round(verts[i].z * scale) / scale);
            keys[i] = k;
            map[k] = map.TryGetValue(k, out var acc) ? acc + normals[i] : normals[i];
        }

        var result = new Vector3[verts.Length];
        for (int i = 0; i < verts.Length; i++)
            result[i] = map[keys[i]].normalized;
        return result;
    }

    static void SafeDestroy(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

#if UNITY_EDITOR
    // インスペクタで値を変えたら遅延して再ベイク(OnValidate中は生成不可のため)
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall -= DelayedRebake;
        UnityEditor.EditorApplication.delayCall += DelayedRebake;
    }
    void DelayedRebake()
    {
        UnityEditor.EditorApplication.delayCall -= DelayedRebake;
        if (this == null || !isActiveAndEnabled) return;
        Rebake();
    }
#endif
}