using UnityEngine;

#region エフェクトマネージャー
/// <summary>
/// エフェクト生成の窓口
/// </summary>
public class EffectManager : MonoBehaviour
{
    public static EffectManager Instance;

    private EffectPool pool = new EffectPool();
    private Transform EffectRoot;
    #region 初期化
    private void Awake()
    {
        Instance = this;

        if (EffectRoot == null)
        {
            GameObject root = new GameObject("Effects");
            root.transform.SetParent(this.transform);
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            EffectRoot = root.transform;
        }
    }
    #endregion

    #region 生成
    public EffectInstance Spawn(EffectData data)
    {
        GameObject obj = pool.Get(data,EffectRoot);

        EffectInstance inst = obj.GetComponent<EffectInstance>();
        if (inst == null)
        {
            inst = obj.AddComponent<EffectInstance>();
        }

        inst.Initialize(this, data);

        return inst;
    }
    #endregion

    #region 返却
    public void Release(EffectInstance inst)
    {
        pool.Release(inst.gameObject, inst.SourcePrefab);
    }
    #endregion
}
#endregion
