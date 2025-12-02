using System;
using UnityEngine;

/// <summary>
/// Central event system for decoupled communication between game systems.
/// Provides events for UI, network, input, and game state management.
/// </summary>
public static class Events
{
    #region Network Events
    public static event Action OnHostClicked;
    public static event Action OnQuickJoinClicked;
    public static event Action OnRestartClicked;
    public static event Action OnLeaveClicked;
    #endregion

    #region Input Events
    public static event Action<Vector2> OnMoveInput;
    public static event Action OnMoveInputStop;
    public static event Action<Vector2> OnLookDelta;
    public static event Action OnFireDown;
    public static event Action OnFireUp;
    public static event Action<float> OnVerticalMoveInput;
    public static event Action OnVerticalMoveInputStop;
    #endregion

    #region UI Events
    public static event Action<string> OnSetStatusText;
    public static event Action<bool> OnShowMenu;

    // NEW: Event with winner information
    public static event Action<string, int> OnShowGameOverWithWinner;
    public static event Action OnShowGameOver; // Keep for compatibility

    public static event Action<string, int> OnUpdateScore;
    #endregion

    #region Game State Events
    public static event Action OnMatchStart;
    public static event Action OnMatchEnd;
    #endregion

    #region Event Raisers - Network
    public static void RaiseHostClicked() => OnHostClicked?.Invoke();
    public static void RaiseQuickJoinClicked() => OnQuickJoinClicked?.Invoke();
    public static void RaiseRestartClicked() => OnRestartClicked?.Invoke();
    public static void RaiseLeaveClicked() => OnLeaveClicked?.Invoke();
    #endregion

    #region Event Raisers - Input
    public static void RaiseMoveInput(Vector2 v) => OnMoveInput?.Invoke(v);
    public static void RaiseMoveInputStop() => OnMoveInputStop?.Invoke();
    public static void RaiseLookDelta(Vector2 d) => OnLookDelta?.Invoke(d);
    public static void RaiseFireDown() => OnFireDown?.Invoke();
    public static void RaiseFireUp() => OnFireUp?.Invoke();
    public static void RaiseVerticalMoveInput(float dir) => OnVerticalMoveInput?.Invoke(dir);
    public static void RaiseVerticalMoveInputStop() => OnVerticalMoveInputStop?.Invoke();
    #endregion

    #region Event Raisers - UI
    public static void RaiseSetStatusText(string t) => OnSetStatusText?.Invoke(t);
    public static void RaiseShowMenu(bool b) => OnShowMenu?.Invoke(b);

    // NEW: Raise game over with winner info
    public static void RaiseShowGameOverWithWinner(string winner, int kills)
    {
        OnShowGameOverWithWinner?.Invoke(winner, kills);
        OnShowGameOver?.Invoke(); // Also trigger old event for compatibility
    }

    public static void RaiseShowGameOver() => OnShowGameOver?.Invoke();

    public static void RaiseUpdateScore(string name, int kills) => OnUpdateScore?.Invoke(name, kills);
    #endregion

    #region Event Raisers - Game State
    public static void RaiseMatchStart() => OnMatchStart?.Invoke();
    public static void RaiseMatchEnd() => OnMatchEnd?.Invoke();
    #endregion
}