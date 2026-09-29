using System.Collections.Generic;
using UnityEngine;

// ─────────────────────────────────────────────────────────────
// 子レンダラーのマテリアルを集中管理する
//   ・defaultMaterial … 全ての子レンダラーへ適用（1個入れれば全メッシュに乗る）
//   ・overrides ……… 特定の子だけ別マテリアルに（レンダラー名で指定）
//   ・"__Outline"(SkinnedOutlineBaker生成物)は除外するので競合しない
//   ・[ExecuteAlways] なので Edit Mode で即反映
//   置き場所: 通常フォルダ(Editorフォルダではない)
//
//   使い方の例:
//     defaultMaterial = M_Halftone   → 全身が M_Halftone
//     overrides に {rendererName:"Player_BoosterCore", material:M_Glow} を足す
//                                    → その部位だけ M_Glow
//   ※ defaultMaterial を空にすれば、overrides で指定した部位だけ変更し
//     他は元のマテリアルを維持する運用もできる
// ─────────────────────────────────────────────────────────────
[ExecuteAlways]
[DisallowMultipleComponent]
public class MaterialAssigner : MonoBehaviour
{
    [Tooltip("全ての子レンダラーに適用する基本マテリアル(空なら全体適用しない)")]
    public Material defaultMaterial;

    [System.Serializable]
    public struct Override
    {
        [Tooltip("対象レンダラーのGameObject名(完全一致)")]
        public string rendererName;
        public Material material;
    }

    [Tooltip("特定の子だけ別マテリアルにしたい場合の上書きリスト")]
    public Override[] overrides;

    [Tooltip("非アクティブな子も対象に含める")]
    public bool includeInactive = true;

    // SkinnedOutlineBaker が付ける名前サフィックス。これで終わるレンダラーは対象外
    const string OutlineSuffix = "__Outline";

    void OnEnable() { Apply(); }

    [ContextMenu("Apply Now")]
    public void Apply()
    {
        var targets = CollectTargets();

        // 1) まず全員へ基本マテリアル
        if (defaultMaterial != null)
        {
            foreach (var r in targets)
                r.sharedMaterial = defaultMaterial; // slot0のみ
        }

        // 2) 名前が一致する子だけ上書き
        if (overrides != null)
        {
            foreach (var ov in overrides)
            {
                if (ov.material == null || string.IsNullOrEmpty(ov.rendererName)) continue;
                foreach (var r in targets)
                {
                    if (r.gameObject.name == ov.rendererName)
                        r.sharedMaterial = ov.material;
                }
            }
        }
    }

    // 対象レンダラーを収集(アウトライン生成物は除外)
    List<Renderer> CollectTargets()
    {
        var all = GetComponentsInChildren<Renderer>(includeInactive);
        var list = new List<Renderer>(all.Length);
        foreach (var r in all)
        {
            if (r == null) continue;
            if (r.gameObject.name.EndsWith(OutlineSuffix)) continue; // アウトラインは触らない
            list.Add(r);
        }
        return list;
    }

    // 対象と現在のマテリアルを一覧表示(名前確認用)
    [ContextMenu("Log Targets")]
    void LogTargets()
    {
        var targets = CollectTargets();
        foreach (var r in targets)
            Debug.Log($"target: {r.name}  (current: {(r.sharedMaterial ? r.sharedMaterial.name : "none")})", r);
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        UnityEditor.EditorApplication.delayCall -= DelayedApply;
        UnityEditor.EditorApplication.delayCall += DelayedApply;
    }
    void DelayedApply()
    {
        UnityEditor.EditorApplication.delayCall -= DelayedApply;
        if (this == null || !isActiveAndEnabled) return;
        Apply();
    }
#endif
}