using Fusion;
using UnityEngine;
using System.Collections.Generic;

// Authoritative game manager: starts and ends match, tracks kills and scoreboard.
public class GameManager : MonoBehaviour
{
    public int killsToWin = 40;
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
        if (matchRunning) return;
        matchRunning = true;
        Events.RaiseSetStatusText("Match started!");
        Events.RaiseShowMenu(false);
        // Notify player objects to enable combat
        var players = FindObjectsOfType<Scripts.Gameplay.PlayerController>();
        foreach (var p in players)
        {
            p.EnableCombat(true);
        }
    }

    public void EndMatch()
    {
        if (!matchRunning) return;
        matchRunning = false;
        Events.RaiseSetStatusText("Match ended!");
        Events.RaiseShowGameOver();
        Events.RaiseMatchEnd();
        // Disable combat on players
        var players = FindObjectsOfType<Scripts.Gameplay.PlayerController>();
        foreach (var p in players)
            p.EnableCombat(false);
    }

    // Called by PlayerController (or the PlayerController triggers score UI updates via Events)
    private void OnUpdateScoreUI(string playerName, int kills)
    {
        if (kills >= killsToWin)
        {
            // Match end
            Events.RaiseSetStatusText($"{playerName} won!");
            EndMatch();
        }
    }
}