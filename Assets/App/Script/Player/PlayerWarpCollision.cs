using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class PlayerWarpCollision : MonoBehaviour
{
    [Serializable]
    private class WarpTargetSet
    {
        [Header("判定対象")]
        [Tooltip("このColliderに触れたらワープします。")]
        public Collider targetCollider;

        [Header("ワープ先")]
        [Tooltip("ワープ先座標です。空のGameObjectを置いてTransformを指定する想定です。")]
        public Transform warpPoint;

        [Tooltip("ONならワープ先Transformの回転も適用します。OFFならプレイヤーの向きは維持します。")]
        public bool applyWarpRotation = false;

        [Header("ダメージ")]
        [Tooltip("ONならワープ時にダメージも与えます。")]
        public bool useDamage = false;

        [Tooltip("ワープ時に与えるダメージ量です。")]
        public int damage = 0;
    }

    [Header("参照")]
    [SerializeField]
    [Tooltip("親のPlayerControllerです。未設定なら親から自動取得します。")]
    private PlayerController playerController;

    [Header("ワープ設定")]
    [SerializeField]
    [Tooltip("対象Colliderとワープ先Transformの対応リストです。")]
    private List<WarpTargetSet> warpTargetSets = new List<WarpTargetSet>();

    [Header("連続発動防止")]
    [SerializeField]
    [Tooltip("ワープ直後に連続で再ワープしないためのクールタイムです。")]
    private float warpCooldown = 0.1f;

    [Header("Debug")]
    [SerializeField]
    private bool debugLog = false;

    private Collider selfCollider;
    private float cooldownTimer = 0f;

    private void Awake()
    {
        selfCollider = GetComponent<Collider>();

        if (selfCollider != null && !selfCollider.isTrigger)
        {
            Debug.LogWarning("[PlayerWarpCollision] このColliderはTrigger推奨です。isTriggerをONにしてください。", this);
        }

        if (playerController == null)
        {
            playerController = GetComponentInParent<PlayerController>();
        }

        if (playerController == null)
        {
            Debug.LogWarning("[PlayerWarpCollision] 親からPlayerControllerを取得できませんでした。", this);
        }
    }

    private void Update()
    {
        if (cooldownTimer <= 0f)
            return;

        cooldownTimer -= Time.deltaTime;

        if (cooldownTimer < 0f)
            cooldownTimer = 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (playerController == null)
            return;

        if (other == null)
            return;

        if (cooldownTimer > 0f)
            return;

        WarpTargetSet matchedSet = FindMatchedWarpTargetSet(other);

        if (matchedSet == null)
            return;

        ExecuteWarp(matchedSet);
    }

    private WarpTargetSet FindMatchedWarpTargetSet(Collider other)
    {
        for (int i = 0; i < warpTargetSets.Count; i++)
        {
            WarpTargetSet set = warpTargetSets[i];

            if (set == null)
                continue;

            if (set.targetCollider == null)
                continue;

            // 登録済みColliderと、接触したColliderが完全一致した時だけ反応する
            if (set.targetCollider == other)
                return set;
        }

        return null;
    }

    private void ExecuteWarp(WarpTargetSet set)
    {
        if (set.warpPoint == null)
        {
            Debug.LogWarning("[PlayerWarpCollision] warpPointが未設定です。", this);
            return;
        }

        Vector3 warpPosition = set.warpPoint.position;
        Quaternion warpRotation = set.warpPoint.rotation;

        if (set.useDamage && set.damage > 0)
        {
            if (set.applyWarpRotation)
            {
                playerController.WarpWithDamage(
                    warpPosition,
                    warpRotation,
                    set.damage
                );
            }
            else
            {
                playerController.WarpWithDamage(
                    warpPosition,
                    set.damage
                );
            }
        }
        else
        {
            if (set.applyWarpRotation)
            {
                playerController.Warp(
                    warpPosition,
                    warpRotation
                );
            }
            else
            {
                playerController.Warp(
                    warpPosition
                );
            }
        }

        cooldownTimer = Mathf.Max(warpCooldown, 0f);

        if (debugLog)
        {
            Debug.Log(
                $"[PlayerWarpCollision] Warp実行 / target={set.targetCollider.name}, point={set.warpPoint.name}, damage={(set.useDamage ? set.damage : 0)}",
                this
            );
        }
    }
}