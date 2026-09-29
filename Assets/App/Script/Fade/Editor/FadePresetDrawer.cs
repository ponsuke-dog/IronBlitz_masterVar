using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(FadePreset))]
public class FadePresetDrawer : PropertyDrawer
{
    private const float LineHeight = 20f;
    private const float Space = 4f;
    private const float LabelWidth = 120f;

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        int lines = 0;

        SerializedProperty fadetype = property.FindPropertyRelative("fadetype");
        SerializedProperty tileOrderMode = property.FindPropertyRelative("tileOrderMode");

        // 共通項目
        lines += 4;
        // presetname, fadetype, duration, fadecolor

        if ((FadeType)fadetype.enumValueIndex == FadeType.Tile)
        {
            lines += 5;
            // columns, rows, edgewidth, edgecolor, tileOrderMode

            TileOrderMode mode = (TileOrderMode)tileOrderMode.enumValueIndex;

            if (UsesStartCorner(mode))
            {
                lines += 1;
            }

            if (UsesRandomness(mode))
            {
                lines += 2;
                // randomness, randomSeed
            }

            if (UsesRandomBatch(mode))
            {
                lines += 1;
            }
        }

        return lines * (LineHeight + Space);
    }

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        SerializedProperty presetname = property.FindPropertyRelative("presetname");
        SerializedProperty fadetype = property.FindPropertyRelative("fadetype");
        SerializedProperty duration = property.FindPropertyRelative("duration");
        SerializedProperty fadecolor = property.FindPropertyRelative("fadecolor");
        SerializedProperty columns = property.FindPropertyRelative("columns");
        SerializedProperty rows = property.FindPropertyRelative("rows");
        SerializedProperty edgewidth = property.FindPropertyRelative("edgewidth");
        SerializedProperty edgecolor = property.FindPropertyRelative("edgecolor");
        SerializedProperty randomness = property.FindPropertyRelative("randomness");
        SerializedProperty randomSeed = property.FindPropertyRelative("randomSeed");
        SerializedProperty tileOrderMode = property.FindPropertyRelative("tileOrderMode");
        SerializedProperty tileStartCorner = property.FindPropertyRelative("tileStartCorner");
        SerializedProperty randomBatchSize = property.FindPropertyRelative("randomBatchSize");

        Rect r = new Rect(position.x, position.y, position.width, LineHeight);

        DrawRow(ref r, "プリセット名", presetname);
        DrawRow(ref r, "フェード方式", fadetype);
        DrawRow(ref r, "フェード時間", duration);
        DrawRow(ref r, "フェード色", fadecolor);

        if ((FadeType)fadetype.enumValueIndex == FadeType.Tile)
        {
            DrawRow(ref r, "タイル列数", columns);
            DrawRow(ref r, "タイル行数", rows);
            DrawRow(ref r, "境界の太さ", edgewidth);
            DrawRow(ref r, "境界色", edgecolor);
            DrawRow(ref r, "タイル順序", tileOrderMode);

            TileOrderMode mode = (TileOrderMode)tileOrderMode.enumValueIndex;

            if (UsesStartCorner(mode))
            {
                DrawRow(ref r, "開始コーナー", tileStartCorner);
            }

            if (UsesRandomness(mode))
            {
                DrawRow(ref r, "ランダム性", randomness);
                DrawRow(ref r, "ランダムシード", randomSeed);
            }

            if (UsesRandomBatch(mode))
            {
                DrawRow(ref r, "一度に進むタイル数", randomBatchSize);
            }
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

    private static bool UsesStartCorner(TileOrderMode mode)
    {
        switch (mode)
        {
            case TileOrderMode.CenterOut:
            case TileOrderMode.OutsideIn:
            case TileOrderMode.Random:
            case TileOrderMode.Checkerboard:
                return false;

            default:
                return true;
        }
    }

    private static bool UsesRandomness(TileOrderMode mode)
    {
        switch (mode)
        {
            case TileOrderMode.Random:
            case TileOrderMode.ZigZagHorizontal:
            case TileOrderMode.ZigZagVertical:
            case TileOrderMode.Checkerboard:
                return true;

            default:
                return false;
        }
    }

    private static bool UsesRandomBatch(TileOrderMode mode)
    {
        return mode == TileOrderMode.Random;
    }
}
