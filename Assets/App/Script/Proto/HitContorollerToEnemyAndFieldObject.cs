using UnityEngine;
using System.Collections.Generic;

// ============================================
// プレイヤー攻撃用コライダー → Enemy FieldObjectヒット通知
// ============================================
public class HitContorollerToEnemyAndFieldObject : MonoBehaviour
{
    // 親のプレイヤー参照
    private PlayerController_C player;

    // 既にヒットしたEnemy一覧（同一個体多段防止）
    private HashSet<GameObject> hitObjects = new HashSet<GameObject>();
    
    private void Awake()
    {
        // 親からPlayerControllerを取得
        player = GetComponentInParent<PlayerController_C>();
    }

    private void OnEnable()
    {
        // タックル開始ごとにリセット
        hitObjects.Clear();
    }

    private void OnTriggerEnter(Collider other)
    {
        GameObject root = other.transform.root.gameObject;

        if (hitObjects.Contains(root)) return;

        FieldObjectController field =
            root.GetComponent<FieldObjectController>();

        if (field == null) return;

        hitObjects.Add(root);

        player?.OnTackleHit(other);
    }
}