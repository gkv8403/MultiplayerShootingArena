using Fusion;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public int killsToWin = 10;
    private bool matchRunning = false;
    private string winnerName = "";
    private int winnerKills = 0;
    private NetworkRunner runner;

    // Track scores locally too for debugging
    private Dictionary<string, int> playerKills = new Dictionary<string, int>();

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

    private void OnMatchEndRequested()
    {
        if (GetIsServer())
        {
            EndMatch("", 0);
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
        winnerKills = 0;
        playerKills.Clear();

        Debug.Log("[GameManager] ========== MATCH STARTED ==========");

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
                p.RPC_BroadcastScore(p.PlayerName, 0);

                Debug.Log($"[GameManager] Reset player {pName} - Combat: ENABLED");
            }
        }

        Events.RaiseShowMenu(false);
        Debug.Log($"[GameManager] Combat enabled for {players.Length} players");
    }

    private void EndMatch(string winner, int kills)
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
        winnerKills = kills;

        Debug.Log($"[GameManager] ========== MATCH ENDED ==========");
        Debug.Log($"[GameManager] Winner: {winner} with {kills} kills");

        var players = FindObjectsOfType<Scripts.Gameplay.PlayerController>();
        foreach (var p in players)
        {
            if (p.Object != null)
            {
                p.EnableCombat(false);
                Debug.Log($"[GameManager] Disabled combat for {p.PlayerName}");
            }
        }

        string winText = string.IsNullOrEmpty(winner)
            ? "Match ended!"
            : $"{winner} wins with {kills} kills!";

        Events.RaiseSetStatusText(winText);
        Events.RaiseShowGameOver();
    }

    private void OnScoreUpdate(string playerName, int kills)
    {
        // Update local tracking
        playerKills[playerName] = kills;

        Debug.Log($"[GameManager] === SCORE UPDATE ===");
        Debug.Log($"[GameManager] Player: {playerName}");
        Debug.Log($"[GameManager] Kills: {kills}");
        Debug.Log($"[GameManager] Target: {killsToWin}");
        Debug.Log($"[GameManager] Match Running: {matchRunning}");
        Debug.Log($"[GameManager] Is Server: {GetIsServer()}");

        // Show all current scores
        foreach (var kvp in playerKills)
        {
            Debug.Log($"[GameManager]   {kvp.Key}: {kvp.Value} kills");
        }

        if (!GetIsServer())
        {
            Debug.Log("[GameManager] Not server, score update ignored");
            return;
        }

        if (!matchRunning)
        {
            Debug.Log("[GameManager] Match not running, score update ignored");
            return;
        }

        // Check win condition
        if (kills >= killsToWin)
        {
            Debug.Log($"[GameManager] 🏆 {playerName} REACHED {killsToWin} KILLS - MATCH OVER!");
            EndMatch(playerName, kills);
        }
        else
        {
            Debug.Log($"[GameManager] {playerName} needs {killsToWin - kills} more kills to win");
        }
    }

    public bool IsMatchRunning() => matchRunning;

    // Debug helper
    private void OnGUI()
    {
        if (!GetIsServer() || !Debug.isDebugBuild) return;

        GUI.color = Color.cyan;
        GUILayout.BeginArea(new Rect(10, 10, 250, 150));
        GUILayout.Label("=== GAME MANAGER ===");
        GUILayout.Label($"Match Running: {matchRunning}");
        GUILayout.Label($"Kills to Win: {killsToWin}");
        GUILayout.Label("--- Scores ---");
        foreach (var kvp in playerKills)
        {
            GUILayout.Label($"{kvp.Key}: {kvp.Value}");
        }
        GUILayout.EndArea();
    }
}