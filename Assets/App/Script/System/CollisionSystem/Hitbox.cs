using UnityEngine;

/// 論理的な当たり判定単位（Colliderの集合をまとめる）
public class Hitbox : MonoBehaviour
{
    // ===== ロジック用（実際の処理で使う） =====
    public IHitReceiver receiver; // 被弾側
    public IHitSource source;     // 攻撃側

    // ===== デバッグ用（Inspector表示） =====
    [SerializeField] private GameObject ownerObject;

    void Awake()
    {
        // 親から自動取得
        receiver = GetComponentInParent<IHitReceiver>();
        source = GetComponentInParent<IHitSource>();

        // Inspectorで見えるようにする
        if (receiver != null)
        {
            ownerObject = (receiver as MonoBehaviour).gameObject;
        }
        else if (source != null)
        {
            ownerObject = (source as MonoBehaviour).gameObject;
        }

        // 見つからなかった場合警告
        if (ownerObject == null)
        {
            Debug.LogWarning($"{name}: Owner not found.");
        }
    }
}