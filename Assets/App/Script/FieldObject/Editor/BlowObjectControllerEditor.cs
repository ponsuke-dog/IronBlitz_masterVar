#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(BlowObjectController))]
public class BlowObjectControllerEditor : Editor
{
    private SerializedProperty objectType;

    private SerializedProperty hitParam;
    private SerializedProperty motionParam;
    private SerializedProperty bounceParam;
    private SerializedProperty chainParam;
    private SerializedProperty rotationParam;
    private SerializedProperty hpParam;
    private SerializedProperty bounceEffect;

    private SerializedProperty lightObject;
    private SerializedProperty heavyObject;
    private SerializedProperty bombObject;
    private SerializedProperty stickyObject;

    private bool showCommonHit = false;
    private bool showCommonMotion = false;
    private bool showCommonBounce = false;
    private bool showCommonChain = false;
    private bool showCommonRotation = false;
    private bool showCommonHP = false;
    private bool showCommonEffect = false;

    private void OnEnable()
    {
        objectType = serializedObject.FindProperty("objectType");

        hitParam = serializedObject.FindProperty("hitParam");
        motionParam = serializedObject.FindProperty("motionParam");
        bounceParam = serializedObject.FindProperty("bounceParam");
        chainParam = serializedObject.FindProperty("chainParam");
        rotationParam = serializedObject.FindProperty("rotationParam");
        hpParam = serializedObject.FindProperty("hpParam");
        bounceEffect = serializedObject.FindProperty("bounceEffect");

        lightObject = serializedObject.FindProperty("lightObject");
        heavyObject = serializedObject.FindProperty("heavyObject");
        bombObject = serializedObject.FindProperty("bombObject");
        stickyObject = serializedObject.FindProperty("stickyObject");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.Space(4);
        EditorGUILayout.PropertyField(objectType);

        EditorGUILayout.Space(8);
        DrawTypeSpecificParam();

        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("共通パラメーター", EditorStyles.boldLabel);

        DrawFoldoutProperty(ref showCommonHit, "当たり判定 / レイヤー", hitParam);
        DrawFoldoutProperty(ref showCommonMotion, "移動 / 基本物理", motionParam);
        DrawFoldoutProperty(ref showCommonBounce, "反射 / 接地", bounceParam);
        DrawFoldoutProperty(ref showCommonChain, "連鎖", chainParam);
        DrawFoldoutProperty(ref showCommonRotation, "回転", rotationParam);
        DrawFoldoutProperty(ref showCommonHP, "HP / 破壊", hpParam);
        DrawFoldoutProperty(ref showCommonEffect, "反射エフェクト", bounceEffect);

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawTypeSpecificParam()
    {
        EditorGUILayout.LabelField("タイプ別パラメーター", EditorStyles.boldLabel);

        switch (objectType.enumValueIndex)
        {
            case 0:
                EditorGUILayout.PropertyField(lightObject, true);
                break;

            case 1:
                EditorGUILayout.PropertyField(heavyObject, true);
                break;

            case 2:
                EditorGUILayout.PropertyField(bombObject, true);
                break;

            case 3:
                EditorGUILayout.PropertyField(stickyObject, true);
                break;
        }
    }

    private void DrawFoldoutProperty(
        ref bool foldout,
        string label,
        SerializedProperty property)
    {
        if (property == null)
            return;

        foldout = EditorGUILayout.Foldout(foldout, label, true);

        if (!foldout)
            return;

        EditorGUI.indentLevel++;
        EditorGUILayout.PropertyField(property, true);
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(4);
    }
}
#endif