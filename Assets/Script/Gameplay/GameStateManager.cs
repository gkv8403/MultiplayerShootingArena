using UnityEngine;
using Fusion;
using System;

public class GameStateManager : MonoBehaviour
{
    public static GameStateManager Instance { get; private set; }

    public event Action<NetworkRunner> OnRunnerSpawned;

    //[SerializeField] private NetworkRunner runner;

    private GameState currentState = GameState.Menu;

    public enum GameState
    {
        Menu,
        Connecting,
        InGame,
        GameOver
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("[GameStateManager] Initialized as singleton");
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
        Debug.Log("[GameStateManager] Match started");
        SetGameState(GameState.InGame);
    }

    private void OnMatchEnd()
    {
        Debug.Log("[GameStateManager] Match ended");
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
        Debug.Log($"[GameStateManager] State: {newState}");

        switch (newState)
        {
            case GameState.Menu:
                Events.RaiseShowMenu(true);
                break;

            case GameState.Connecting:
                Events.RaiseShowMenu(true);
                Events.RaiseSetStatusText("Connecting...");
                break;

            case GameState.InGame:
                Events.RaiseShowMenu(false);
                Events.RaiseSetStatusText("Match Running");
                break;

            case GameState.GameOver:
                Events.RaiseShowGameOver();
                break;
        }
    }

    /*public void NotifyRunnerSpawned(NetworkRunner newRunner)
    {
        Debug.Log("[GameStateManager] Runner spawned - notifying subscribers");
        OnRunnerSpawned?.Invoke(newRunner);
    }*/

    public GameState GetCurrentState() => currentState;
}