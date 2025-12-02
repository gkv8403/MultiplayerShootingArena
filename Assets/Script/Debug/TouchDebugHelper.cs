using UnityEngine;
using TMPro;

/// <summary>
/// ATTACH TO A UI TEXT OBJECT TO DEBUG TOUCH INPUT
/// Shows real-time touch delta values on screen
/// </summary>
public class TouchDebugHelper : MonoBehaviour
{
    private TMP_Text debugText;
    private InputManager inputManager;

    private void Start()
    {
        debugText = GetComponent<TMP_Text>();
        if (debugText == null)
            debugText = gameObject.AddComponent<TMP_Text>();

        // Position at top-left corner
        RectTransform rt = GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(10, -10);
            rt.sizeDelta = new Vector2(400, 200);
        }

        debugText.fontSize = 14;
        debugText.color = Color.yellow;
        debugText.alignment = TextAlignmentOptions.TopLeft;
    }

    private void Update()
    {
        if (inputManager == null)
            inputManager = InputManager.Instance;

        if (inputManager == null || debugText == null)
            return;

        string info = "=== TOUCH DEBUG ===\n";
        info += $"Mode: {(inputManager.useKeyboardMouse ? "KEYBOARD" : "MOBILE")}\n";
        info += $"Look Delta: {inputManager.CurrentLookDelta}\n";
        info += $"Move Input: {inputManager.CurrentMoveInput}\n";
        info += $"Fire: {inputManager.CurrentFire}\n";
        info += $"Vertical: {inputManager.CurrentVerticalMove}\n\n";

        // Show touch count
        info += $"Touch Count: {Input.touchCount}\n";

        if (Input.touchCount > 0)
        {
            for (int i = 0; i < Input.touchCount && i < 3; i++)
            {
                Touch touch = Input.GetTouch(i);
                info += $"Touch {i}: {touch.phase} at {touch.position}\n";
                info += $"  Delta: {touch.deltaPosition}\n";
            }
        }

        debugText.text = info;
    }
}