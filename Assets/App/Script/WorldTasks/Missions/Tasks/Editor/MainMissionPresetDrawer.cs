using UnityEditor;
using UnityEngine;
using static MainMissionPreset;

[CustomPropertyDrawer(typeof(MainMissionPreset))]
public class MainMissionPresetDrawer : PropertyDrawer
{
    private const float LineHeight = 20f;
    private const float Space = 4f;
    private const float LabelWidth = 120f;


    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int lines = 0;

        SerializedProperty missiontype = property.FindPropertyRelative("presetType");

        // 共通項目
        lines += 1;

        var type = (MainMissionPreset.StageMission)missiontype.enumValueIndex;

        switch (type)
        {
            case MainMissionPreset.StageMission.Goal:        // クリアタイム
                break;
            case MainMissionPreset.StageMission.Kill:        // 撃破数
                lines += 3;
                break;
            case MainMissionPreset.StageMission.Collect:     // 収集
                lines += 2;
                break;
        }

        return lines * (LineHeight + Space);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty missionType = property.FindPropertyRelative("presetType");
        SerializedProperty killType = property.FindPropertyRelative("killType");
        SerializedProperty killCount = property.FindPropertyRelative("KillCount");
        SerializedProperty CollectCount = property.FindPropertyRelative("CollectCount");
        SerializedProperty killOBJ = property.FindPropertyRelative("KillObject");
        SerializedProperty CollectOBJ = property.FindPropertyRelative("CollectObject");

        Rect r = new Rect(position.x, position.y, position.width, LineHeight);

        DrawRow(ref r, "ミッション方式", missionType);

        switch ((MainMissionPreset.StageMission)missionType.enumValueIndex)
        {
            case MainMissionPreset.StageMission.Goal:        // ゴール
                break;
            case MainMissionPreset.StageMission.Kill:        // 撃破数
                DrawRow(ref r, "キル数タイプ", killType);
                if ((MainMissionPreset.KillConditionType)killType.enumValueIndex == KillConditionType.ALLEnemy)
                {
                }
                else
                {
                    DrawRow(ref r, "必要撃破数", killCount);
                    DrawRow(ref r, "必要撃破エネミー", killOBJ);
                }
                break;
            case MainMissionPreset.StageMission.Collect:     // 収集
                DrawRow(ref r, "必要収集数", CollectCount);
                DrawRow(ref r, "必要収集アイテム", CollectOBJ);
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
