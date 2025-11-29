using Fusion;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public int killsToWin = 10;
    private bool matchRunning = false;

    private void OnEnable()
    {
        Events.OnMatchStart += StartMatch;
        Events.OnMatchEnd += EndMatch;
        Events.OnUpdateScore += OnUpdateScoreUI;
    }

    private void OnDisable()
    {
        Events.OnMatchStart -= StartMatch;
        Events.OnMatchEnd -= EndMatch;
        Events.OnUpdateScore -= OnUpdateScoreUI;
    }

    private void StartMatch()
    {
        if (matchRunning)
        {
            Debug.Log("[GameManager] Match already running");
            return;
        }

        matchRunning = true;
        Debug.Log("[GameManager] Match started!");

        Events.RaiseSetStatusText("Match started!");
        Events.RaiseShowMenu(false);

        // Enable combat on all players
        var players = FindObjectsOfType<Scripts.Gameplay.PlayerController>();
        foreach (var p in players)
        {
            p.EnableCombat(true);
        }

        Debug.Log($"[GameManager] Combat enabled for {players.Length} players");
    }

    public void EndMatch()
    {
        if (!matchRunning)
        {
            Debug.Log("[GameManager] Match not running");
            return;
        }

        matchRunning = false;
        Debug.Log("[GameManager] Match ended!");

        Events.RaiseSetStatusText("Match ended!");
        Events.RaiseShowGameOver();

        // Disable combat on all players
        var players = FindObjectsOfType<Scripts.Gameplay.PlayerController>();
        foreach (var p in players)
        {
            p.EnableCombat(false);
        }
    }
        
    private void OnUpdateScoreUI(string playerName, int kills)
    {
        Debug.Log($"[GameManager] Score update: {playerName} - {kills} kills");

        if (kills >= killsToWin && matchRunning)
        {
            Debug.Log($"[GameManager] {playerName} reached {killsToWin} kills - Match over!");
            Events.RaiseSetStatusText($"{playerName} won!");
            EndMatch();
        }
    }

    public bool IsMatchRunning() => matchRunning;
}