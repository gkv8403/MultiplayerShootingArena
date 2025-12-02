using System;
using UnityEngine;

/// <summary>
/// Central event system for decoupled communication between game systems.
/// Provides events for UI, network, input, and game state management.
/// </summary>
public static class Events
{
    #region Network Events
    /// <summary>
    /// Triggered when host button is clicked in UI
    /// </summary>
    public static event Action OnHostClicked;

    /// <summary>
    /// Triggered when quick join button is clicked in UI
    /// </summary>
    public static event Action OnQuickJoinClicked;

    /// <summary>
    /// Triggered when restart button is clicked (host only)
    /// </summary>
    public static event Action OnRestartClicked;

    /// <summary>
    /// Triggered when leave/disconnect button is clicked
    /// </summary>
    public static event Action OnLeaveClicked;
    #endregion

    #region Input Events
    /// <summary>
    /// Triggered when movement input is detected (mobile touch controls)
    /// </summary>
    public static event Action<Vector2> OnMoveInput;

    /// <summary>
    /// Triggered when movement input stops
    /// </summary>
    public static event Action OnMoveInputStop;

    /// <summary>
    /// Triggered when look/camera rotation input is detected
    /// </summary>
    public static event Action<Vector2> OnLookDelta;

    /// <summary>
    /// Triggered when fire button is pressed
    /// </summary>
    public static event Action OnFireDown;

    /// <summary>
    /// Triggered when fire button is released
    /// </summary>
    public static event Action OnFireUp;

    /// <summary>
    /// Triggered when vertical movement input is detected (Q/E keys or mobile buttons)
    /// Direction: 1 = up, -1 = down
    /// </summary>
    public static event Action<float> OnVerticalMoveInput;

    /// <summary>
    /// Triggered when vertical movement input stops
    /// </summary>
    public static event Action OnVerticalMoveInputStop;
    #endregion

    #region UI Events
    /// <summary>
    /// Triggered to update status text in UI (connection status, etc.)
    /// </summary>
    public static event Action<string> OnSetStatusText;

    /// <summary>
    /// Triggered to show or hide the main menu
    /// </summary>
    public static event Action<bool> OnShowMenu;

    /// <summary>
    /// Triggered to show the game over screen
    /// </summary>
    public static event Action OnShowGameOver;

    /// <summary>
    /// Triggered when player score updates
    /// Parameters: playerName, killCount
    /// </summary>
    public static event Action<string, int> OnUpdateScore;
    #endregion

    #region Game State Events
    /// <summary>
    /// Triggered when match starts (combat becomes enabled)
    /// </summary>
    public static event Action OnMatchStart;

    /// <summary>
    /// Triggered when match ends (win condition met or player leaves)
    /// </summary>
    public static event Action OnMatchEnd;
    #endregion

    #region Event Raisers - Network
    /// <summary>
    /// Raise host button click event
    /// </summary>
    public static void RaiseHostClicked() => OnHostClicked?.Invoke();

    /// <summary>
    /// Raise quick join button click event
    /// </summary>
    public static void RaiseQuickJoinClicked() => OnQuickJoinClicked?.Invoke();

    /// <summary>
    /// Raise restart button click event
    /// </summary>
    public static void RaiseRestartClicked() => OnRestartClicked?.Invoke();

    /// <summary>
    /// Raise leave button click event
    /// </summary>
    public static void RaiseLeaveClicked() => OnLeaveClicked?.Invoke();
    #endregion

    #region Event Raisers - Input
    /// <summary>
    /// Raise movement input event
    /// </summary>
    /// <param name="v">Movement direction vector</param>
    public static void RaiseMoveInput(Vector2 v) => OnMoveInput?.Invoke(v);

    /// <summary>
    /// Raise movement stop event
    /// </summary>
    public static void RaiseMoveInputStop() => OnMoveInputStop?.Invoke();

    /// <summary>
    /// Raise look/camera rotation event
    /// </summary>
    /// <param name="d">Look delta (how much camera moved)</param>
    public static void RaiseLookDelta(Vector2 d) => OnLookDelta?.Invoke(d);

    /// <summary>
    /// Raise fire button down event
    /// </summary>
    public static void RaiseFireDown() => OnFireDown?.Invoke();

    /// <summary>
    /// Raise fire button up event
    /// </summary>
    public static void RaiseFireUp() => OnFireUp?.Invoke();

    /// <summary>
    /// Raise vertical movement input event
    /// </summary>
    /// <param name="dir">Direction: 1 for up, -1 for down</param>
    public static void RaiseVerticalMoveInput(float dir) => OnVerticalMoveInput?.Invoke(dir);

    /// <summary>
    /// Raise vertical movement stop event
    /// </summary>
    public static void RaiseVerticalMoveInputStop() => OnVerticalMoveInputStop?.Invoke();
    #endregion

    #region Event Raisers - UI
    /// <summary>
    /// Raise status text update event
    /// </summary>
    /// <param name="t">Status text to display</param>
    public static void RaiseSetStatusText(string t) => OnSetStatusText?.Invoke(t);

    /// <summary>
    /// Raise show/hide menu event
    /// </summary>
    /// <param name="b">True to show, false to hide</param>
    public static void RaiseShowMenu(bool b) => OnShowMenu?.Invoke(b);

    /// <summary>
    /// Raise show game over event
    /// </summary>
    public static void RaiseShowGameOver() => OnShowGameOver?.Invoke();

    /// <summary>
    /// Raise score update event
    /// </summary>
    /// <param name="name">Player name</param>
    /// <param name="kills">Current kill count</param>
    public static void RaiseUpdateScore(string name, int kills) => OnUpdateScore?.Invoke(name, kills);
    #endregion

    #region Event Raisers - Game State
    /// <summary>
    /// Raise match start event
    /// </summary>
    public static void RaiseMatchStart() => OnMatchStart?.Invoke();

    /// <summary>
    /// Raise match end event
    /// </summary>
    public static void RaiseMatchEnd() => OnMatchEnd?.Invoke();
    #endregion
}