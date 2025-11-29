using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public int killsToWin = 10;
    private bool matchRunning = false;
    private string winnerName = "";
    private NetworkRunner runner;

    private void Awake()
    {
        // Singleton pattern (optional)
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

    private void OnMatchEndRequested()
    {
        if (GetIsServer())
        {
            EndMatch("");
        }
    }

    private bool GetIsServer()
    {
        if (runner == null)
            runner = FindObjectOfType<NetworkRunner>();

        return runner != null && runner.IsServer;
    }

    private void StartMatch()
    {
        if (!GetIsServer())
        {
            Debug.Log("[GameManager] Not server, ignoring start request");
            return;
        }

        if (matchRunning)
        {
            Debug.Log("[GameManager] Match already running");
            return;
        }

        matchRunning = true;
        winnerName = "";
        Debug.Log("[GameManager] Match started!");

        // Reset all players and enable combat
        var players = FindObjectsOfType<Scripts.Gameplay.PlayerController>();
        foreach (var p in players)
        {
            if (p.Object != null && p.Object.HasStateAuthority)
            {
                p.Kills = 0;
                p.Deaths = 0;
                p.Health = p.maxHealth;
                p.EnableCombat(true);
                p.RPC_BroadcastScore(p.PlayerName, 0);
            }
        }

        // Notify all clients
        Events.RaiseSetStatusText("Match started!");
        Events.RaiseShowMenu(false);

        Debug.Log($"[GameManager] Combat enabled for {players.Length} players");
    }

    private void EndMatch(string winner)
    {
        if (!GetIsServer())
        {
            Debug.Log("[GameManager] Not server, ignoring end request");
            return;
        }

        if (!matchRunning)
        {
            Debug.Log("[GameManager] Match not running");
            return;
        }

        matchRunning = false;
        winnerName = winner;
        Debug.Log($"[GameManager] Match ended! Winner: {winner}");

        // Disable combat on all players
        var players = FindObjectsOfType<Scripts.Gameplay.PlayerController>();
        foreach (var p in players)
        {
            if (p.Object != null)
            {
                p.EnableCombat(false);
            }
        }

        // Notify via events
        string winText = string.IsNullOrEmpty(winner)
            ? "Match ended!"
            : $"{winner} wins!";

        Events.RaiseSetStatusText(winText);
        Events.RaiseShowGameOver();
    }

    private void OnScoreUpdate(string playerName, int kills)
    {
        if (!GetIsServer()) return;
        if (!matchRunning) return;

        Debug.Log($"[GameManager] Score update: {playerName} - {kills} kills (target: {killsToWin})");

        // Check win condition
        if (kills >= killsToWin)
        {
            Debug.Log($"[GameManager] {playerName} reached {killsToWin} kills - Match over!");
            EndMatch(playerName);
        }
    }

    public bool IsMatchRunning() => matchRunning;
}