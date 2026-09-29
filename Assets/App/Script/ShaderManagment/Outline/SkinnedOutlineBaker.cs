using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────
// 【キャラ(SkinnedMeshRenderer)用】アウトライン生成 + スムース法線ベイカー
//   ・親(ルート)に付けると子の SkinnedMeshRenderer を全部自動で処理する
//   ・UVはスキンで変形しないため使えない → スムース法線を「NORMAL」に焼く
//   ・元メッシュのNORMALを壊すとLitの陰影が崩れるので、
//     スムース法線を焼いたクローンを「Outline専用の別SMR」に差し、
//     元と同じ bones / rootBone を共有してアニメに追従させる
//   ・[ExecuteAlways] なので Play しなくても Edit Mode で反映される
//
//   置き場所: 通常のスクリプトフォルダ(Editorフォルダではない)
//   ※ outlineMaterial は _USE_SMOOTH_NORMAL を「OFF」のものを使う
//     (NORMALで押し出すため。クローンのNORMALが既にスムースになっている)
//   ※ ランタイム再計算するモデルは Import Settings の Read/Write Enabled をON
// ─────────────────────────────────────────────────────────────
[ExecuteAlways]
[DisallowMultipleComponent]
public class SkinnedOutlineBaker : MonoBehaviour
{
    [Tooltip("キャラ用Outlineマテリアル(_USE_SMOOTH_NORMAL は OFF)")]
    public Material outlineMaterial;

    [Tooltip("同じ位置の頂点とみなす丸め桁数")]
    public int roundDigits = 3;

    const string OutlineSuffix = "__Outline";

    void OnEnable() { Rebuild(); }
    void OnDisable() { Clear(); }

    [ContextMenu("Rebuild Now")]
    public void Rebuild()
    {
        Clear(); // 既存の生成物を一旦削除して重複生成を防ぐ
        if (outlineMaterial == null) return;

        var smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in smrs)
        {
            if (smr == null || smr.sharedMesh == null) continue;
            if (smr.name.EndsWith(OutlineSuffix)) continue; // 自前の生成物はスキップ
            CreateOutline(smr);
        }
    }

    void CreateOutline(SkinnedMeshRenderer src)
    {
        // スムース法線をNORMALに焼いたクローンメッシュを作る
        Mesh clone = Instantiate(src.sharedMesh);
        clone.name = src.sharedMesh.name + OutlineSuffix;
        clone.hideFlags = HideFlags.DontSave;
        clone.normals = ComputeSmoothNormals(clone, roundDigits); // ← NORMALへ焼く
        clone.UploadMeshData(false);

        // 元レンダラーと同じ階層に子として生成(ローカルTRSは初期値)
        var go = new GameObject(src.name + OutlineSuffix);
        go.transform.SetParent(src.transform, false);
        go.hideFlags = HideFlags.DontSave;

        var outline = go.AddComponent<SkinnedMeshRenderer>();
        outline.sharedMesh = clone;
        outline.bones = src.bones;     // 同じボーンを共有 → 同じ変形
        outline.rootBone = src.rootBone;
        outline.localBounds = src.localBounds;
        outline.quality = src.quality;
        outline.updateWhenOffscreen = src.updateWhenOffscreen;
        outline.sharedMaterial = outlineMaterial;

        // ブレンドシェイプの初期値だけ合わせる
        // (毎フレーム動くブレンドシェイプには追従しない。必要なら別途同期処理を追加)
        for (int i = 0; i < clone.blendShapeCount; i++)
            outline.SetBlendShapeWeight(i, src.GetBlendShapeWeight(i));
    }

    void Clear()
    {
        var smrs = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        foreach (var smr in smrs)
        {
            if (smr == null) continue;
            if (smr.name.EndsWith(OutlineSuffix))
            {
                SafeDestroy(smr.sharedMesh);
                SafeDestroy(smr.gameObject);
            }
        }
    }

    // 同一位置の頂点の法線を平均化してスムース法線を作る
    static Vector3[] ComputeSmoothNormals(Mesh mesh, int digits)
    {
        Vector3[] verts = mesh.vertices;
        Vector3[] normals = mesh.normals;
        float scale = Mathf.Pow(10f, digits);

        var map = new Dictionary<Vector3, Vector3>();
        var keys = new Vector3[verts.Length];

        for (int i = 0; i < verts.Length; i++)
        {
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
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall -= DelayedRebuild;
        UnityEditor.EditorApplication.delayCall += DelayedRebuild;
    }
    void DelayedRebuild()
    {
        UnityEditor.EditorApplication.delayCall -= DelayedRebuild;
        if (this == null || !isActiveAndEnabled) return;
        Rebuild();
    }
#endif
}