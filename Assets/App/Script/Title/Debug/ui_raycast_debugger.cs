using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// マウス位置にあるUI Raycast対象をConsoleへ表示するデバッグ用クラスです。
/// 原因特定後は削除してください。
/// </summary>
public sealed class UIRaycastDebugger : MonoBehaviour
{
    private readonly List<RaycastResult> raycastResults = new();

    private void Update()
    {
        if (!Input.GetKeyDown(KeyCode.F8))
        {
            return;
        }

        if (EventSystem.current == null)
        {
            Debug.LogWarning("EventSystem が見つかりません。");
            return;
        }

        PointerEventData pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        raycastResults.Clear();
        EventSystem.current.RaycastAll(pointerData, raycastResults);

        Debug.Log($"Raycast Count: {raycastResults.Count}");

        for (int i = 0; i < raycastResults.Count; i++)
        {
            RaycastResult result = raycastResults[i];
            Debug.Log($"{i}: {result.gameObject.name} / Root: {result.gameObject.transform.root.name}");
        }
    }
}
