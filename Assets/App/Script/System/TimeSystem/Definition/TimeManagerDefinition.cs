/// <summary>
/// TimeManagerDefinition.cs
/// </summary>


// ==========================================
// 時間管理グループ
// オブジェクトをカテゴリ分けする
// ==========================================

public enum TimeGroupType
{
    Player,         // プレイヤー
    Enemy,          // 敵
    Effect,         // エフェクト
    Stage,          // ステージオブジェクト
    UI,              // UI
    ReflectObject  // 反射オブジェクト
}

// ==========================================
// 時間レイヤー
// グループより上位の時間制御
// ==========================================

public enum TimeLayerType
{
    Gameplay,    // 通常ゲーム
    Cutscene,    // カットシーン
    UI           // UI
}

// ========================================
// 演出用タイムチャンネル
// ========================================

public enum TimeChannelType
{
    Default,        // 通常
    CombatEffect,   // 戦闘演出
    PlayerAbility   // プレイヤー能力
}

// ============================================
// Modifierの合成方法
// ============================================

public enum TimeModifierMode
{
    Multiply,   // 乗算
    Min,        // 最小値
    Override    // 強制上書き
}

// ============================================
// TimeModifierPriority
// ============================================

public enum TimePriority
{
    Low = 0,

    GameplaySlow = 10,

    Ability = 30,

    HitStop = 100,

    Cutscene = 1000
}