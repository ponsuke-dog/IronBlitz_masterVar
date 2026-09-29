using UnityEditor;
using UnityEngine;
using static SubMissionPreset;

[CustomPropertyDrawer(typeof(SubMissionPreset))]
public class SubMissionPresetDrawer : PropertyDrawer
{
    private const float LineHeight = 20f;
    private const float Space = 4f;
    private const float LabelWidth = 120f;


    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int lines = 0;

        SerializedProperty missiontype = property.FindPropertyRelative("presetType");

        // 共通項目
        lines += 2;

        var type = (SubMissionPreset.MissionType)missiontype.enumValueIndex;

        switch (type)
        {
            case SubMissionPreset.MissionType.ClearTime:        // クリアタイム
                lines += 1;
                break;
            case SubMissionPreset.MissionType.CollectCount:     // 収集
                lines += 2;
                break;
            case SubMissionPreset.MissionType.KillCount:        // 撃破数
             
                    lines += 2;
                
                break;
            case SubMissionPreset.MissionType.TackleCountLimit:      // タックル回数 まで
                lines += 1;
                break;
            case SubMissionPreset.MissionType.TackleCountOverthan:      // タックル回数 以上
                lines += 1;
                break;
            case SubMissionPreset.MissionType.BreakBlockCountLimit:  // ブロック破壊数
                lines += 2;
                break;
            case SubMissionPreset.MissionType.BreakBlockCountOverthan:  // ブロック破壊数
                lines += 2;
                break;
            case SubMissionPreset.MissionType.JumpCountLimit:        // ジャンプ回数
                lines += 1;
                break;
            case SubMissionPreset.MissionType.JumpCountOverthan:        // ジャンプ回数
                lines += 1;
                break;
            case SubMissionPreset.MissionType.HPSaving:         // HPセーフティ
                lines += 1;
                break;
            
        }

        return lines * (LineHeight + Space);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty missionType = property.FindPropertyRelative("presetType");
        SerializedProperty clearTime = property.FindPropertyRelative("TimeCount");
        SerializedProperty objectCount = property.FindPropertyRelative("ObjectCount");
        SerializedProperty killType = property.FindPropertyRelative("killType");
        SerializedProperty actionCount = property.FindPropertyRelative("PlayerActionCount");
        SerializedProperty playerHP = property.FindPropertyRelative("PlayerHP");
        SerializedProperty collectOBJ = property.FindPropertyRelative("CollectObject");
        SerializedProperty enemyOBJ = property.FindPropertyRelative("EnemyObject");
     
        Rect r = new Rect(position.x, position.y, position.width, LineHeight);

        DrawRow(ref r, "ミッション方式", missionType);

        switch ((SubMissionPreset.MissionType)missionType.enumValueIndex)
        {
            case SubMissionPreset.MissionType.ClearTime:        // クリアタイム
                DrawRow(ref r, "クリアタイム : 秒以上", clearTime);
                break;
            case SubMissionPreset.MissionType.CollectCount:     // 収集
                DrawRow(ref r, "必要収集数", objectCount);
                DrawRow(ref r, "必要収集アイテム", collectOBJ);
                break;
            case SubMissionPreset.MissionType.KillCount:        // 撃破数
                DrawRow(ref r, "キル数タイプ", killType);
                if ((SubMissionPreset.KillConditionType)killType.enumValueIndex == KillConditionType.ALLEnemy)
                {
                }
                else
                {
                    DrawRow(ref r, "必要撃破数", objectCount);
                    DrawRow(ref r, "必要撃破エネミー", enemyOBJ);
                }
                break;
            case SubMissionPreset.MissionType.TackleCountLimit:     // タックル回数
                DrawRow(ref r, "上限タックル数", actionCount);
                break;
            case SubMissionPreset.MissionType.TackleCountOverthan:     // タックル回数
                DrawRow(ref r, "必要タックル数", actionCount);
                break;
            case SubMissionPreset.MissionType.BreakBlockCountLimit:  // ブロック破壊数上限
                DrawRow(ref r, "上限ブロック破壊数", objectCount);
                break;
            case SubMissionPreset.MissionType.BreakBlockCountOverthan:  // ブロック破壊数
                DrawRow(ref r, "必要ブロック破壊数", objectCount);
                break;
            case SubMissionPreset.MissionType.JumpCountLimit:        // ジャンプ回数
                DrawRow(ref r, "上限ジャンプ数", actionCount);
                break;
            case SubMissionPreset.MissionType.JumpCountOverthan:        // ジャンプ回数
                DrawRow(ref r, "必要ジャンプ数", actionCount);
                break;
            case SubMissionPreset.MissionType.HPSaving:         // HPセーフティ
                DrawRow(ref r, "必要HP残量数", playerHP);
                break;

        }

    
        EditorGUI.EndProperty();
    }

    private void DrawRow(ref Rect rowRect, string label, SerializedProperty property)
    {
        Rect labelRect = new Rect(rowRect.x, rowRect.y, LabelWidth, LineHeight);
        Rect fieldRect = new Rect(rowRect.x + LabelWidth + 4f, rowRect.y, rowRect.width - LabelWidth - 4f, LineHeight);

        EditorGUI.LabelField(labelRect, label);
        EditorGUI.PropertyField(fieldRect, property, GUIContent.none);

        rowRect.y += LineHeight + Space;
    }

}
