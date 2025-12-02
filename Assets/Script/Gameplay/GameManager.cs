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
        Debug.Log($"[GameManager] ========================================");
        Debug.Log($"[GameManager] START MATCH CALLED");
        Debug.Log($"[GameManager] Is Server: {GetIsServer()}");
        Debug.Log($"[GameManager] ========================================");

        // CLIENT: Just update state
        if (!GetIsServer())
        {
            if (matchRunning)
            {
                Debug.Log("[GameManager] ⚠️ CLIENT: Match already running");
                return;
            }

            matchRunning = true;
            matchEnding = false;
            Debug.Log($"[GameManager] ✅ CLIENT: Match running (tracking {playerKills.Count} scores)");
            Events.RaiseShowMenu(false);
            return;
        }

        // SERVER: Full initialization
        if (matchRunning)
        {
            Debug.Log("[GameManager] ⚠️ SERVER: Match already running");
            return;
        }

        matchRunning = true;
        matchEnding = false;
        winnerName = "";
        winnerKills = 0;
        playerKills.Clear();

        Debug.Log("[GameManager] ✅ SERVER: MATCH IS NOW RUNNING!");

        var players = FindObjectsOfType<Scripts.Gameplay.PlayerController>();
        Debug.Log($"[GameManager] SERVER: Found {players.Length} players");

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

                Debug.Log($"[GameManager] SERVER: ✓ Reset {pName} (Player{p.Object.InputAuthority.PlayerId})");
                Debug.Log($"[GameManager] SERVER:   - Combat: {p.CombatEnabled}");
                Debug.Log($"[GameManager] SERVER:   - Health: {p.Health}");

                // Broadcast initial score
                p.RPC_UpdateScoreOnAllClients(p.PlayerName, 0);
            }
        }

        Events.RaiseShowMenu(false);
        Debug.Log($"[GameManager] ========================================");
        Debug.Log($"[GameManager] MATCH STARTED SUCCESSFULLY");
        Debug.Log($"[GameManager] ========================================");
    }

    private void EndMatch(string winner, int kills)
    {
        if (!GetIsServer()) return;
        if (!matchRunning) return;
        if (matchEnding) return;

        matchEnding = true;
        matchRunning = false;
        winnerName = winner;
        winnerKills = kills;

        Debug.Log($"[GameManager] ========== MATCH ENDED ==========");
        Debug.Log($"[GameManager] 🏆 Winner: {winner} with {kills} kills");

        var players = FindObjectsOfType<Scripts.Gameplay.PlayerController>();
        foreach (var p in players)
        {
            if (p.Object != null && p.Object.HasStateAuthority)
            {
                p.EnableCombat(false);
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
        // Always update local dictionary
        playerKills[playerName] = kills;

        Debug.Log($"[GameManager] ==========================================");
        Debug.Log($"[GameManager] 📊 OnScoreUpdate EVENT");
        Debug.Log($"[GameManager] Player: {playerName}");
        Debug.Log($"[GameManager] Kills: {kills}");
        Debug.Log($"[GameManager] Is Server: {GetIsServer()}");
        Debug.Log($"[GameManager] Match Running: {matchRunning}");
        Debug.Log($"[GameManager] Match Ending: {matchEnding}");
        Debug.Log($"[GameManager] Total Tracked: {playerKills.Count}");

        // Show all scores
        Debug.Log($"[GameManager] --- Current Scores ---");
        foreach (var kvp in playerKills.OrderByDescending(x => x.Value))
        {
            Debug.Log($"[GameManager]   {kvp.Key}: {kvp.Value}");
        }
        Debug.Log($"[GameManager] ==========================================");

        // Only server checks win condition
        if (!GetIsServer())
        {
            Debug.Log("[GameManager] CLIENT - Score tracked for UI only");
            return;
        }

        if (!matchRunning)
        {
            Debug.Log("[GameManager] ⚠️ SERVER: Match not running - ignoring for win check");
            return;
        }

        if (matchEnding)
        {
            Debug.Log("[GameManager] ⚠️ SERVER: Match ending - ignoring");
            return;
        }

        // Check win condition
        if (kills >= killsToWin)
        {
            Debug.Log($"[GameManager] ========================================");
            Debug.Log($"[GameManager] 🏆🎉 VICTORY!");
            Debug.Log($"[GameManager] Winner: {playerName}");
            Debug.Log($"[GameManager] Kills: {kills}/{killsToWin}");
            Debug.Log($"[GameManager] ========================================");
            EndMatch(playerName, kills);
        }
        else
        {
            Debug.Log($"[GameManager] 💪 SERVER: {playerName} needs {killsToWin - kills} more kills to win");
        }
    }

    public bool IsMatchRunning() => matchRunning && !matchEnding;

    private void OnGUI()
    {
        if (!Debug.isDebugBuild) return;

        string role = GetIsServer() ? "SERVER" : "CLIENT";
        GUI.color = matchRunning ? Color.green : Color.red;

        float xPos = GetIsServer() ? 10 : Screen.width - 310;
        GUILayout.BeginArea(new Rect(xPos, 10, 300, 350));

        GUILayout.Label($"=== GAME MANAGER ({role}) ===");
        GUILayout.Label($"Match Running: {matchRunning}");
        GUILayout.Label($"Match Ending: {matchEnding}");
        GUILayout.Label($"Kills to Win: {killsToWin}");

        GUILayout.Space(10);
        GUILayout.Label("--- SCORES TRACKED ---");

        if (playerKills.Count == 0)
        {
            GUILayout.Label("No scores yet");
        }
        else
        {
            foreach (var kvp in playerKills.OrderByDescending(x => x.Value))
            {
                string winIndicator = kvp.Value >= killsToWin ? " 🏆" : "";
                string progress = $"{kvp.Value}/{killsToWin}";
                GUI.color = kvp.Value >= killsToWin ? Color.yellow : Color.white;
                GUILayout.Label($"{kvp.Key}: {progress}{winIndicator}");
                GUI.color = matchRunning ? Color.green : Color.red;
            }
        }

        if (GetIsServer())
        {
            GUILayout.Space(10);
            GUILayout.Label("--- SERVER PLAYER STATE ---");
            var allPlayers = FindObjectsOfType<Scripts.Gameplay.PlayerController>();
            GUILayout.Label($"Total Players: {allPlayers.Length}");

            foreach (var p in allPlayers)
            {
                if (p.Object != null && p.Object.HasStateAuthority)
                {
                    string combatStatus = p.CombatEnabled ? "✅" : "❌";
                    GUILayout.Label($"{p.PlayerName}: {combatStatus}");
                    GUILayout.Label($"  K:{p.Kills} D:{p.Deaths} HP:{p.Health}");
                }
            }
        }

        GUILayout.EndArea();
    }
}