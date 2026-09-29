using System.Collections.Generic;
using UnityEngine;

public class ChainHitManager : MonoBehaviour
{
    public static ChainHitManager Instance { get; private set; }

    [Header("コンボ表示用")]
    [SerializeField] private float comboResetTime = 1.2f;

    private readonly Dictionary<ulong, float> pairCooldowns = new Dictionary<ulong, float>();
    
    private int chainCount;
    private float comboTimer;

    private void Awake()
    {
        Instance = this;
    }

    private void Update()
    {
        UpdateComboTimer();
        CleanupExpiredPairs();
    }

    public bool TryBeginChainPair(GameObject a, GameObject b, float cooldown, out int chainIndex)
    {
        chainIndex = chainCount + 1;

        if (a == null || b == null || a == b)
            return false;

        ulong key = MakePairKey(a, b);
        float now = Time.time;

        if (pairCooldowns.TryGetValue(key, out float untilTime))
        {
            if (untilTime > now)
                return false;

            pairCooldowns.Remove(key);
        }

        pairCooldowns[key] = now + Mathf.Max(0f, cooldown);

        chainCount++;
        comboTimer = comboResetTime;
        chainIndex = chainCount;

        return true;
    }


    public void NotifyChain(int chain)
    {
        Debug.Log($"HIT x{chain}");
    }


    private void UpdateComboTimer()
    {
        if (comboTimer <= 0f)
            return;

        comboTimer -= Time.deltaTime;
        if (comboTimer <= 0f)
        {
            chainCount = 0;
        }
    }

    private void CleanupExpiredPairs()
    {
        if (pairCooldowns.Count == 0)
            return;

        float now = Time.time;

        List<ulong> removeKeys = null;

        foreach (var pair in pairCooldowns)
        {
            if (pair.Value <= now)
            {
                removeKeys ??= new List<ulong>();
                removeKeys.Add(pair.Key);
            }
        }

        if (removeKeys == null)
            return;

        for (int i = 0; i < removeKeys.Count; i++)
        {
            pairCooldowns.Remove(removeKeys[i]);
        }
    }

    private ulong MakePairKey(GameObject a, GameObject b)
    {
        uint idA = unchecked((uint)a.GetInstanceID());
        uint idB = unchecked((uint)b.GetInstanceID());

        uint min = idA < idB ? idA : idB;
        uint max = idA < idB ? idB : idA;

        return ((ulong)min << 32) | max;
    }
}
