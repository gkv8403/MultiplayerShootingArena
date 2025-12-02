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

    // Track scores locally for debugging
    private Dictionary<string, int> playerKills = new Dictionary<string, int>();

    // ✅ FIX: Prevent duplicate win triggers
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
        matchEnding = false; // ✅ Reset ending flag
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
                p.RPC_UpdateScoreOnAllClients(p.PlayerName, 0);

                Debug.Log($"[GameManager] ✓ Reset player {pName} - Combat: ENABLED");
            }
        }

        Events.RaiseShowMenu(false);
        Debug.Log($"[GameManager] ✓ Combat enabled for {players.Length} players");
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

        // ✅ FIX: Prevent duplicate end calls
        if (matchEnding)
        {
            Debug.Log("[GameManager] ⚠️ Match already ending, ignoring duplicate");
            return;
        }

        matchEnding = true;
        matchRunning = false;
        winnerName = winner;
        winnerKills = kills;

        Debug.Log($"[GameManager] ========== MATCH ENDED ==========");
        Debug.Log($"[GameManager] 🏆 Winner: {winner} with {kills} kills");

        var players = FindObjectsOfType<Scripts.Gameplay.PlayerController>();
        foreach (var p in players)
        {
            if (p.Object != null)
            {
                p.EnableCombat(false);
                Debug.Log($"[GameManager] 🚫 Disabled combat for {p.PlayerName}");
            }
        }

        string winText = string.IsNullOrEmpty(winner)
            ? "Match ended!"
            : $"{winner} wins with {kills} kills!";

        Events.RaiseSetStatusText(winText);
        Events.RaiseShowGameOver();
    }

    // ✅ FIX: Enhanced score tracking with better logging
    private void OnScoreUpdate(string playerName, int kills)
    {
        // Update local tracking
        playerKills[playerName] = kills;

        Debug.Log($"[GameManager] ==================");
        Debug.Log($"[GameManager] 📊 SCORE UPDATE");
        Debug.Log($"[GameManager] Player: {playerName}");
        Debug.Log($"[GameManager] Kills: {kills}/{killsToWin}");
        Debug.Log($"[GameManager] Match Running: {matchRunning}");
        Debug.Log($"[GameManager] Match Ending: {matchEnding}");
        Debug.Log($"[GameManager] Is Server: {GetIsServer()}");
        Debug.Log($"[GameManager] ------------------");

        // Show all current scores
        foreach (var kvp in playerKills.OrderByDescending(x => x.Value))
        {
            string marker = kvp.Key == playerName ? " ← NEW" : "";
            Debug.Log($"[GameManager]   {kvp.Key}: {kvp.Value} kills{marker}");
        }
        Debug.Log($"[GameManager] ==================");

        if (!GetIsServer())
        {
            Debug.Log("[GameManager] ⚠️ Not server, score update logged only");
            return;
        }

        if (!matchRunning)
        {
            Debug.Log("[GameManager] ⚠️ Match not running, score ignored");
            return;
        }

        if (matchEnding)
        {
            Debug.Log("[GameManager] ⚠️ Match ending, score ignored");
            return;
        }

        // ✅ FIX: Check win condition
        if (kills >= killsToWin)
        {
            Debug.Log($"[GameManager] 🎉🏆🎉 {playerName} REACHED {killsToWin} KILLS!");
            Debug.Log($"[GameManager] 🎉🏆🎉 MATCH OVER - WINNER: {playerName}!");
            EndMatch(playerName, kills);
        }
        else
        {
            int remaining = killsToWin - kills;
            Debug.Log($"[GameManager] 💪 {playerName} needs {remaining} more kill{(remaining > 1 ? "s" : "")} to win");
        }
    }

    public bool IsMatchRunning() => matchRunning && !matchEnding;

    // ✅ FIX: Enhanced debug UI
    private void OnGUI()
    {
        if (!GetIsServer() || !Debug.isDebugBuild) return;

        GUI.color = matchRunning ? Color.green : Color.yellow;
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));

        GUILayout.Label("=== GAME MANAGER (SERVER) ===");
        GUILayout.Label($"Match Running: {matchRunning}");
        GUILayout.Label($"Match Ending: {matchEnding}");
        GUILayout.Label($"Kills to Win: {killsToWin}");

        GUILayout.Label("--- SCORES ---");

        if (playerKills.Count == 0)
        {
            GUILayout.Label("No players yet");
        }
        else
        {
            foreach (var kvp in playerKills.OrderByDescending(x => x.Value))
            {
                string winIndicator = kvp.Value >= killsToWin ? " 🏆" : "";
                GUILayout.Label($"{kvp.Key}: {kvp.Value}/{killsToWin}{winIndicator}");
            }
        }

        GUILayout.EndArea();
    }
}