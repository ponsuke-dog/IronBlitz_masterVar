using System.Collections.Generic;
using UnityEngine;

#region エフェクトプール
/// <summary>
/// エフェクトの生成・再利用管理
/// </summary>
public class EffectPool
{
    private class PoolData
    {
        public Queue<GameObject> inactive = new();
        public int totalCount;
        public int maxSize;
    }

    private Dictionary<GameObject, PoolData> pool = new();

    #region 取得
    public GameObject Get(EffectData data,Transform parent)
    {
        var prefab = data.prefab;

        if (!pool.TryGetValue(prefab,out var pl))// プールの存在チェック & なければ生成
        {
            pl = new PoolData // Dictionary検索の回数減少 と 現時点の参照保持(事故防止)
            {
                maxSize = data.maxPoolSize
            };

            pool[prefab] = pl; // データをDictionaryに登録
        }

        // 再利用
        if (pl.inactive.Count > 0)
        {
            var obj = pl.inactive.Dequeue();
            obj.transform.SetParent(parent);    // 再び親設定(やらないと親がバラバラに)
            obj.SetActive(true);

            // 念のため
            var inst = obj.GetComponent<EffectInstance>();
            inst.IsPooled = true;

            return obj;
        }

        // 上限未満ならプール対象として生成
        if (pl.totalCount < pl.maxSize)
        {
            // インスタンス生成
            var obj = GameObject.Instantiate(prefab,parent);
            pl.totalCount++;

            // エフェクトインスタンスの取得
            var inst = obj.GetComponent<EffectInstance>();
            if (inst == null)
            {
                inst = obj.AddComponent<EffectInstance>();
            }
            inst.IsPooled = true;

            return obj;
        }

        // 上限到達したら使い捨て
        {
            // インスタンス生成
            var temp = GameObject.Instantiate(prefab, parent);

            var inst = temp.GetComponent<EffectInstance>();
            if (inst == null)
            {
                inst = temp.AddComponent<EffectInstance>();
            }
            inst.IsPooled = false;


            return temp;
        }
    }
    #endregion

    #region 返却
    public void Release(GameObject obj, GameObject prefab)
    {
        if (obj == null)
            return;

        var inst = obj.GetComponent<EffectInstance>();

        if (inst == null)
        {
            GameObject.Destroy(obj);
            return;
        }

        // 使い捨てを破棄
        if (!inst.IsPooled)
        {
            GameObject.Destroy(obj);
            return;
        }

        if (prefab == null || !pool.TryGetValue(prefab, out var pl))
        {
            GameObject.Destroy(obj);
            return;
        }

        obj.SetActive(false);
        pl.inactive.Enqueue(obj);
    }
    #endregion
}
#endregion
