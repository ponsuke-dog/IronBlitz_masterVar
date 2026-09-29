using UnityEngine;

// ============================================
// TimeSystem テスト用スクリプト
// ============================================

public class testTimeManager : MonoBehaviour
{
    // テスト対象
    [SerializeField] GameObject Player;
    [SerializeField] GameObject[] Enemies;

    TimeAgent playerAgent;
    TimeAgent[] enemyAgents;

    // Handleテスト用
    TimeHandle testHandle;

    void Start()
    {
        playerAgent = Player.GetComponent<TimeAgent>();

        enemyAgents = new TimeAgent[Enemies.Length];

        for (int i = 0; i < Enemies.Length; i++)
        {
            enemyAgents[i] =
                Enemies[i].GetComponent<TimeAgent>();
        }
    }


    void Update()
    {
        var tm = GameTimeManager.Instance;

        // ==============================
        // 1 : Globalスロー
        // ==============================
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            tm.GlobalScale = 0.3f;
            Debug.Log("Global Slow 0.3");
        }

        // ==============================
        // 2 : Globalリセット
        // ==============================
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            tm.GlobalScale = 1f;
            Debug.Log("Global Reset");
        }


        // ==============================
        // 3 : Enemy Groupスロー
        // ==============================
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            tm.SlowGroup(TimeGroupType.Enemy, 0.3f);
            Debug.Log("Enemy Group Slow");
        }

        // ==============================
        // 4 : Enemy Groupリセット
        // ==============================
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            tm.SlowGroup(TimeGroupType.Enemy, 1f);
            Debug.Log("Enemy Group Reset");
        }


        // ==============================
        // 5 : Gameplay Layerスロー
        // ==============================
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            tm.SlowLayer(TimeLayerType.Gameplay, 0.5f);
            Debug.Log("Gameplay Layer Slow");
        }

        // ==============================
        // 6 : Gameplay Layerリセット
        // ==============================
        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            tm.SlowLayer(TimeLayerType.Gameplay, 1f);
            Debug.Log("Gameplay Layer Reset");
        }


        // ==============================
        // 7 : CombatEffect Channelスロー
        // ==============================
        if (Input.GetKeyDown(KeyCode.Alpha7))
        {
            tm.SlowChannel(TimeChannelType.CombatEffect, 0.2f);
            Debug.Log("CombatEffect Channel Slow");
        }

        // ==============================
        // 8 : CombatEffect Channelリセット
        // ==============================
        if (Input.GetKeyDown(KeyCode.Alpha8))
        {
            tm.SlowChannel(TimeChannelType.CombatEffect, 1f);
            Debug.Log("CombatEffect Channel Reset");
        }


        // ==============================
        // 9 : Enemyヒットストップ(全員)
        // ==============================
        if (Input.GetKeyDown(KeyCode.Alpha9))
        {
            foreach (var e in enemyAgents)
            {
                tm.HitStop(e, 0.5f);
            }

            Debug.Log("All Enemy HitStop");
        }


        // ==============================
        // 0 : Playerヒットストップ
        // ==============================
        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            tm.HitStop(playerAgent, 0.6f);

            Debug.Log("Player HitStop");
        }


        // ==============================
        // Q : 範囲ヒットストップ(中心含む)
        // ==============================
        if (Input.GetKeyDown(KeyCode.Q))
        {
            tm.HitStopInRadius(
                Player.transform.position,
                5f,
                0.5f,
                true);

            Debug.Log("Radius HitStop includeCenter");
        }

        // ==============================
        // W : 範囲ヒットストップ(中心除外)
        // ==============================
        if (Input.GetKeyDown(KeyCode.W))
        {
            tm.HitStopInRadius(
                Player.transform.position,
                5f,
                0.5f,
                false);

            Debug.Log("Radius HitStop excludeCenter");
        }


        // ==============================
        // E : Blend Slow Enemy
        // ==============================
        if (Input.GetKeyDown(KeyCode.E))
        {
            AnimationCurve curve =
                AnimationCurve.EaseInOut(0, 0, 1, 1);

            tm.BlendSlowGroup(
                TimeGroupType.Enemy,
                0.2f,
                1f,
                1f,
                curve);

            Debug.Log("BlendSlow Enemy");
        }


        // ==============================
        // R : Blend Slow Player
        // ==============================
        if (Input.GetKeyDown(KeyCode.R))
        {
            AnimationCurve curve =
                AnimationCurve.EaseInOut(0, 0, 1, 1);

            tm.BlendSlowGroup(
                TimeGroupType.Player,
                0.2f,
                1f,
                1f,
                curve);

            Debug.Log("BlendSlow Player");
        }


        // ==============================
        // T : Modifier Handleテスト
        // ==============================
        if (Input.GetKeyDown(KeyCode.T))
        {
            testHandle =
                enemyAgents[0].AddModifier(
                    0.1f,
                    5f,
                    TimeModifierMode.Multiply);

            Debug.Log("Manual Modifier Start");
        }

        // ==============================
        // Y : Modifier強制終了
        // ==============================
        if (Input.GetKeyDown(KeyCode.Y))
        {
            if (testHandle != null)
            {
                testHandle.End();
                Debug.Log("Manual Modifier End");
            }
        }


        // ==============================
        // U : Override競合テスト
        // ==============================
        if (Input.GetKeyDown(KeyCode.U))
        {
            enemyAgents[0].AddModifier(
                0f,
                0.5f,
                TimeModifierMode.Override,
                50);

            enemyAgents[0].AddModifier(
                0.2f,
                0.5f,
                TimeModifierMode.Override,
                100);

            Debug.Log("Override Priority Test");
        }

        // ==============================
        // I : Enemy[0] ヒットストップ
        // ==============================
        if (Input.GetKeyDown(KeyCode.I))
        {
            if (enemyAgents.Length > 0)
            {
                tm.HitStop(enemyAgents[0], 0.5f);
                Debug.Log("Enemy[0] HitStop");
            }
        }

        // ==============================
        // O : Enemy[1] ヒットストップ
        // ==============================
        if (Input.GetKeyDown(KeyCode.O))
        {
            if (enemyAgents.Length > 1)
            {
                tm.HitStop(enemyAgents[1], 0.5f);
                Debug.Log("Enemy[1] HitStop");
            }
        }

        // ==============================
        // P : ランダムEnemyヒットストップ
        // ==============================
        if (Input.GetKeyDown(KeyCode.P))
        {
            int index = Random.Range(0, enemyAgents.Length);

            tm.HitStop(enemyAgents[index], 0.5f);

            Debug.Log("Random Enemy HitStop : " + index);
        }

        // ==============================
        // A : Modifier重ね掛け
        // ==============================
        if (Input.GetKeyDown(KeyCode.A))
        {
            enemyAgents[0].AddModifier(
                0.5f,
                3f,
                TimeModifierMode.Multiply);

            enemyAgents[0].AddModifier(
                0.5f,
                3f,
                TimeModifierMode.Multiply);

            Debug.Log("Modifier Stack Test");
        }

        // ==============================
        // S : Slow + HitStop競合
        // ==============================
        if (Input.GetKeyDown(KeyCode.S))
        {
            enemyAgents[0].AddModifier(
                0.2f,
                5f,
                TimeModifierMode.Multiply);

            tm.HitStop(enemyAgents[0], 0.5f);

            Debug.Log("Slow + HitStop Test");
        }

        // ==============================
        // D : Override Priority Chain
        // ==============================
        if (Input.GetKeyDown(KeyCode.D))
        {
            enemyAgents[0].AddModifier(
                0.3f,
                3f,
                TimeModifierMode.Override,
                10);

            enemyAgents[0].AddModifier(
                0.5f,
                3f,
                TimeModifierMode.Override,
                20);

            enemyAgents[0].AddModifier(
                0.8f,
                3f,
                TimeModifierMode.Override,
                5);

            Debug.Log("Override Chain Test");
        }

        // ==============================
        // F : Handle複数
        // ==============================
        if (Input.GetKeyDown(KeyCode.F))
        {
            var h1 = enemyAgents[0].AddModifier(
                0.5f,
                5f,
                TimeModifierMode.Multiply);

            var h2 = enemyAgents[0].AddModifier(
                0.5f,
                5f,
                TimeModifierMode.Multiply);

            Invoke(nameof(EndHandleTest), 2f);

            Debug.Log("Handle Multi Test");
        }

        // ==============================
        // G : Multi Scale Test
        // ==============================
        if (Input.GetKeyDown(KeyCode.G))
        {
            tm.SlowGroup(TimeGroupType.Enemy, 0.5f);
            tm.SlowLayer(TimeLayerType.Gameplay, 0.5f);
            tm.SlowChannel(TimeChannelType.CombatEffect, 0.5f);

            Debug.Log("MultiScale Test");
        }

        // ==============================
        // H : Modifier Stress Test
        // ==============================
        if (Input.GetKeyDown(KeyCode.H))
        {
            for (int i = 0; i < 20; i++)
            {
                enemyAgents[0].AddModifier(
                    0.9f,
                    5f,
                    TimeModifierMode.Multiply);
            }

            Debug.Log("Modifier Stress Test");
        }

    }

    void EndHandleTest()
    {
        testHandle?.End();
    }

    void OnDrawGizmos()
    {
        if (Player == null) return;

        Gizmos.color = Color.red;

        Gizmos.DrawWireSphere(
            Player.transform.position,
            5f);
    }
}