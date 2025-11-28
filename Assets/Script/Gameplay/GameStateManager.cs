using UnityEngine;
using Fusion;

/// ===== NEW: Centralized game state manager =====
/// This ensures both host and client have synchronized UI states
public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    [SerializeField] private NetworkRunner runner;

    private GameState currentState = GameState.Menu;

    public enum GameState
    {
        Menu,        // Join menu visible
        Connecting,  // Connecting to session
        InGame,      // Game running - show controls
        GameOver     // Match ended
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        Events.OnMatchStart += OnMatchStart;
        Events.OnMatchEnd += OnMatchEnd;
        Events.OnHostClicked += OnConnecting;
        Events.OnQuickJoinClicked += OnConnecting;
        Events.OnLeaveClicked += OnLeaveGame;

        SetGameState(GameState.Menu);
    }

    private void OnDestroy()
    {
        Events.OnMatchStart -= OnMatchStart;
        Events.OnMatchEnd -= OnMatchEnd;
        Events.OnHostClicked -= OnConnecting;
        Events.OnQuickJoinClicked -= OnConnecting;
        Events.OnLeaveClicked -= OnLeaveGame;
    }

    private void OnConnecting()
    {
        SetGameState(GameState.Connecting);
    }

    private void OnMatchStart()
    {
        Debug.Log("[GameStateManager] Match started - showing game UI");
        SetGameState(GameState.InGame);
    }

    private void OnMatchEnd()
    {
        Debug.Log("[GameStateManager] Match ended - showing game over");
        SetGameState(GameState.GameOver);
    }

    private void OnLeaveGame()
    {
        SetGameState(GameState.Menu);
    }

    public void SetGameState(GameState newState)
    {
        if (currentState == newState)
            return;

        currentState = newState;
        Debug.Log($"[GameStateManager] State changed to: {newState}");

        // Update UI based on state
        switch (newState)
        {
            case GameState.Menu:
                Events.RaiseShowMenu(true);
                break;

            case GameState.Connecting:
                Events.RaiseShowMenu(true); // Keep menu visible during connection
                Events.RaiseSetStatusText("Connecting...");
                break;

            case GameState.InGame:
                Events.RaiseShowMenu(false); // Hide menu
                Events.RaiseSetStatusText("Match Running");
                break;

            case GameState.GameOver:
                Events.RaiseShowGameOver();
                break;
        }
    }

    public GameState GetCurrentState()
    {
        return currentState;
    }
}