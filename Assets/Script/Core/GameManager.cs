using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages match lifecycle, win conditions, and score tracking.
/// Handles player state initialization and combat enablement.
/// Uses RPC through PlayerController to sync state across network.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Game Settings")]
    public int killsToWin = 10;

    private bool matchRunning = false;
    private string winnerName = "";
    private int winnerKills = 0;
    private NetworkRunner runner;
    private Dictionary<string, int> playerKills = new Dictionary<string, int>();
    private bool matchEnding = false;

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        runner = FindObjectOfType<NetworkRunner>();
    }

    private void OnEnable()
    {
        Events.OnMatchStart += StartMatch;
        Events.OnMatchEnd += OnMatchEndRequested;
        Events.OnUpdateScore += OnScoreUpdate;
        Events.OnShowGameOverWithWinner += OnGameOverReceived;
    }

    private void OnDisable()
    {
        Events.OnMatchStart -= StartMatch;
        Events.OnMatchEnd -= OnMatchEndRequested;
        Events.OnUpdateScore -= OnScoreUpdate;
        Events.OnShowGameOverWithWinner -= OnGameOverReceived;
    }

    /// <summary>
    /// Called when match end is requested by external source
    /// </summary>
    private void OnMatchEndRequested()
    {
        if (GetIsServer())
        {
            EndMatch("", 0);
        }
    }

    /// <summary>
    /// Check if this instance is the server
    /// </summary>
    private bool GetIsServer()
    {
        if (runner == null)
            runner = FindObjectOfType<NetworkRunner>();

        return runner != null && runner.IsServer;
    }

    /// <summary>
    /// Start a new match - initialize all players and scores
    /// </summary>
    private void StartMatch()
    {
        // SERVER: Full initialization
        if (GetIsServer())
        {
            if (matchRunning) return;

            matchRunning = true;
            matchEnding = false;
            winnerName = "";
            winnerKills = 0;
            playerKills.Clear();

            Debug.Log("[GameManager] SERVER: Starting match");

            // Initialize all players
            var players = FindObjectsOfType<Scripts.Gameplay.PlayerController>();
            foreach (var p in players)
            {
                if (p.Object != null && p.Object.HasStateAuthority)
                {
                    p.Kills = 0;
                    p.Deaths = 0;
                    p.Health = p.maxHealth;
                    p.EnableCombat(true);

                    string pName = p.PlayerName.ToString();
                    playerKills[pName] = 0;

                    // Broadcast initial score
                    p.RPC_UpdateScoreOnAllClients(p.PlayerName, 0);
                }
            }

            // Use RPC to notify all clients match started
            var anyPlayer = players.FirstOrDefault(p => p.Object != null && p.Object.HasStateAuthority);
            if (anyPlayer != null)
            {
                anyPlayer.RPC_SyncMatchState(true, "", 0);
            }

            Events.RaiseShowMenu(false);
        }
        // CLIENT: Just update local state
        else
        {
            matchRunning = true;
            matchEnding = false;
            Events.RaiseShowMenu(false);
            Debug.Log("[GameManager] CLIENT: Match started");
            Debug.Log("[GameManager] CLIENT: Match started");
        }
    }

    /// <summary>
    /// End the current match
    /// </summary>
    private void EndMatch(string winner, int kills)
    {
        if (!GetIsServer()) return;
        if (!matchRunning) return;
        if (matchEnding) return;

        matchEnding = true;
        matchRunning = false;
        winnerName = winner;
        winnerKills = kills;

        Debug.Log($"[GameManager] SERVER: Ending match - Winner: {winner}, Kills: {kills}");

        // Disable combat for all players
        var players = FindObjectsOfType<Scripts.Gameplay.PlayerController>();
        foreach (var p in players)
        {
            if (p.Object != null && p.Object.HasStateAuthority)
            {
                p.EnableCombat(false);
            }
        }

        // Broadcast match end to ALL clients through RPC
        var anyPlayer = players.FirstOrDefault(p => p.Object != null && p.Object.HasStateAuthority);
        if (anyPlayer != null)
        {
            anyPlayer.RPC_BroadcastGameOver(winner, kills);
        }

        // Also show locally on server
        string winText = string.IsNullOrEmpty(winner)
            ? "Match ended!"
            : $"{winner} wins with {kills} kills!";

        Events.RaiseSetStatusText(winText);
        Events.RaiseShowGameOverWithWinner(winner, kills);
    }

    /// <summary>
    /// Called when game over is received from network
    /// </summary>
    private void OnGameOverReceived(string winner, int kills)
    {
        matchEnding = true;
        matchRunning = false;
        winnerName = winner;
        winnerKills = kills;

        Debug.Log($"[GameManager] CLIENT: Game over received - Winner: {winner}, Kills: {kills}");
    }

    /// <summary>
    /// Called when player score updates - check for win condition
    /// </summary>
    private void OnScoreUpdate(string playerName, int kills)
    {
        // Always update local score tracking
        playerKills[playerName] = kills;

        // Only server checks win condition
        if (!GetIsServer()) return;
        if (!matchRunning) return;
        if (matchEnding) return;

        Debug.Log($"[GameManager] Score update: {playerName} = {kills} (Win at {killsToWin})");

        // Check win condition
        if (kills >= killsToWin)
        {
            Debug.Log($"[GameManager] WIN CONDITION MET! {playerName} reached {kills} kills");
            EndMatch(playerName, kills);
        }
    }

    /// <summary>
    /// Check if match is currently running
    /// </summary>
    public bool IsMatchRunning() => matchRunning && !matchEnding;

    /// <summary>
    /// Public method to sync match state from client (called by RPC)
    /// </summary>
    public void SyncMatchStateFromNetwork(bool isRunning, string winner, int kills)
    {
        matchRunning = isRunning;
        winnerName = winner;
        winnerKills = kills;

        if (!isRunning && !string.IsNullOrEmpty(winner))
        {
            matchEnding = true;
        }

        Debug.Log($"[GameManager] Match state synced: Running={isRunning}, Winner={winner}, Kills={kills}");
    }
}