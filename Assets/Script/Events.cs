using System;
using UnityEngine;

public static class Events
{
    // UI -> NetworkManager
    public static event Action OnHostClicked;
    public static event Action OnQuickJoinClicked;
    public static event Action OnRestartClicked;
    public static event Action OnLeaveClicked;

    // UI -> InputManager (touch & button inputs)
    public static event Action<Vector2> OnMoveInput; // vector normalized
    public static event Action OnMoveInputStop;
    public static event Action<Vector2> OnLookDelta; // screen delta
    public static event Action OnFireDown;
    public static event Action OnFireUp;

    // Network/Game -> UI
    public static event Action<string> OnSetStatusText;
    public static event Action<bool> OnShowMenu; // show/hide join menu
    public static event Action OnShowGameOver;
    public static event Action<string, int> OnUpdateScore; // playerName, kills

    // Game -> others
    public static event Action OnMatchStart;
    public static event Action OnMatchEnd;

    // Helper invokers (avoid null checks outside)
    public static void RaiseHostClicked() => OnHostClicked?.Invoke();
    public static void RaiseQuickJoinClicked() => OnQuickJoinClicked?.Invoke();
    public static void RaiseRestartClicked() => OnRestartClicked?.Invoke();
    public static void RaiseLeaveClicked() => OnLeaveClicked?.Invoke();
    public static void RaiseMoveInput(Vector2 v) => OnMoveInput?.Invoke(v);
    public static void RaiseMoveInputStop() => OnMoveInputStop?.Invoke();
    public static void RaiseLookDelta(Vector2 d) => OnLookDelta?.Invoke(d);
    public static void RaiseFireDown() => OnFireDown?.Invoke();
    public static void RaiseFireUp() => OnFireUp?.Invoke();
    public static void RaiseSetStatusText(string t) => OnSetStatusText?.Invoke(t);
    public static void RaiseShowMenu(bool b) => OnShowMenu?.Invoke(b);
    public static void RaiseShowGameOver() => OnShowGameOver?.Invoke();
    public static void RaiseUpdateScore(string name, int kills) => OnUpdateScore?.Invoke(name, kills);
    public static void RaiseMatchStart() => OnMatchStart?.Invoke();
    public static void RaiseMatchEnd() => OnMatchEnd?.Invoke();
}
