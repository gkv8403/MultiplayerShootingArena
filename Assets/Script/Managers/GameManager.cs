using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Manages match lifecycle, win conditions, and score tracking.
/// Handles player state initialization and combat enablement.
/// </summary>
public class GameManager : MonoBehaviour
{
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
    }

    private void OnDisable()
    {
        Events.OnMatchStart -= StartMatch;
        Events.OnMatchEnd -= OnMatchEndRequested;
        Events.OnUpdateScore -= OnScoreUpdate;
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
        // CLIENT: Just update local state
        if (!GetIsServer())
        {
            if (matchRunning) return;

            matchRunning = true;
            matchEnding = false;
            Events.RaiseShowMenu(false);
            return;
        }

        // SERVER: Full initialization
        if (matchRunning) return;

        matchRunning = true;
        matchEnding = false;
        winnerName = "";
        winnerKills = 0;
        playerKills.Clear();

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

        Events.RaiseShowMenu(false);
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

        // Disable combat for all players
        var players = FindObjectsOfType<Scripts.Gameplay.PlayerController>();
        foreach (var p in players)
        {
            if (p.Object != null && p.Object.HasStateAuthority)
            {
                p.EnableCombat(false);
            }
        }

        // Display match result
        string winText = string.IsNullOrEmpty(winner)
            ? "Match ended!"
            : $"{winner} wins with {kills} kills!";

        Events.RaiseSetStatusText(winText);
        Events.RaiseShowGameOver();
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

        // Check win condition
        if (kills >= killsToWin)
        {
            EndMatch(playerName, kills);
        }
    }

    /// <summary>
    /// Check if match is currently running
    /// </summary>
    public bool IsMatchRunning() => matchRunning && !matchEnding;
}   