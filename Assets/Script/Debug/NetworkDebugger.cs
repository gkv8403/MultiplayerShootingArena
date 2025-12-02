using UnityEngine;
using Fusion;
using Scripts.Gameplay;
using System.Linq;

/// <summary>
/// ATTACH TO ANY GAMEOBJECT IN YOUR SCENE
/// Shows real-time network status for debugging
/// Press F3 to toggle display
/// </summary>
public class NetworkDebugger : MonoBehaviour
{
    private NetworkRunner runner;
    private GameManager gameManager;
    private bool showDebug = true;

    private void Update()
    {
        if (runner == null)
            runner = FindObjectOfType<NetworkRunner>();

        if (gameManager == null)
            gameManager = FindObjectOfType<GameManager>();

        // Press F3 to toggle debug display
        if (Input.GetKeyDown(KeyCode.F3))
        {
            showDebug = !showDebug;
            Debug.Log($"[NetworkDebugger] Display {(showDebug ? "enabled" : "disabled")}");
        }
    }

    private void OnGUI()
    {
        if (!showDebug) return;

        GUI.color = Color.cyan;
        GUILayout.BeginArea(new Rect(10, Screen.height - 400, 450, 390));
        GUILayout.Box("=== NETWORK DEBUG (F3 to toggle) ===");

        // Network Status
        if (runner != null)
        {
            string role = runner.IsServer ? "🖥️ HOST/SERVER" : "💻 CLIENT";
            GUI.color = runner.IsServer ? Color.green : Color.yellow;
            GUILayout.Label($"Role: {role}");
            GUI.color = Color.cyan;
            GUILayout.Label($"Session: {runner.SessionInfo.Name}");
            GUILayout.Label($"Players Connected: {runner.ActivePlayers.Count()}");
          //  GUILayout.Label($"Tick Rate: {runner.Config.Simulation.TickRate} Hz");
        }
        else
        {
            GUI.color = Color.red;
            GUILayout.Label("❌ No NetworkRunner - Not Connected");
            GUI.color = Color.cyan;
        }

        GUILayout.Space(10);

        // Match Status
        GUILayout.Label("--- MATCH STATUS ---");
        if (gameManager != null)
        {
            bool matchRunning = gameManager.IsMatchRunning();
            GUI.color = matchRunning ? Color.green : Color.red;
            GUILayout.Label($"Match Running: {matchRunning}");
            GUI.color = Color.cyan;

            if (!matchRunning)
            {
                GUI.color = Color.yellow;
                GUILayout.Label("⚠️ Match NOT started - combat disabled!");
                GUI.color = Color.cyan;
            }
        }
        else
        {
            GUI.color = Color.red;
            GUILayout.Label("❌ No GameManager found");
            GUI.color = Color.cyan;
        }

        GUILayout.Space(10);
        GUILayout.Label("--- PLAYERS ---");

        // Player Status
        var players = FindObjectsOfType<PlayerController>();
        if (players.Length == 0)
        {
            GUILayout.Label("No players spawned yet");
        }
        else
        {
            foreach (var p in players)
            {
                if (p.Object != null)
                {
                    bool isLocal = p.Object.HasInputAuthority;
                    string prefix = isLocal ? "👤 YOU: " : "👥 OTHER: ";

                    GUI.color = isLocal ? Color.green : Color.gray;
                    GUILayout.Label($"{prefix}{p.PlayerName}");

                    // Combat status
                    if (p.CombatEnabled)
                    {
                        GUI.color = Color.green;
                        GUILayout.Label($"  ⚔️ Combat: ENABLED");
                    }
                    else
                    {
                        GUI.color = Color.red;
                        GUILayout.Label($"  ❌ Combat: DISABLED");
                    }

                    GUI.color = Color.cyan;
                    GUILayout.Label($"  Kills: {p.Kills} | Deaths: {p.Deaths} | HP: {p.Health}/{p.maxHealth}");

                    if (isLocal && !p.CombatEnabled)
                    {
                        GUI.color = Color.yellow;
                        GUILayout.Label("  ⚠️ YOU CAN'T SHOOT - MATCH NOT STARTED!");
                        GUI.color = Color.cyan;
                    }
                }
            }
        }

        GUILayout.Space(10);

        // Projectile Status
        GUILayout.Label("--- PROJECTILES ---");
        var pool = NetworkProjectilePool.Instance;
        if (pool != null)
        {
            var projectiles = FindObjectsOfType<Projectile>();
            int totalProjectiles = projectiles.Length;
            int activeProjectiles = 0;
            int visibleProjectiles = 0;

            foreach (var proj in projectiles)
            {
                if (proj.IsActiveNetworked)
                {
                    activeProjectiles++;
                    var mr = proj.GetComponentInChildren<MeshRenderer>();
                    if (mr != null && mr.enabled)
                        visibleProjectiles++;
                }
            }

            GUILayout.Label($"Total Spawned: {totalProjectiles}");
            GUILayout.Label($"Active (Flying): {activeProjectiles}");

            if (visibleProjectiles == activeProjectiles)
            {
                GUI.color = Color.green;
                GUILayout.Label($"✅ Visible: {visibleProjectiles} (OK)");
            }
            else
            {
                GUI.color = Color.red;
                GUILayout.Label($"❌ Visible: {visibleProjectiles} (BROKEN!)");
                GUILayout.Label($"⚠️ {activeProjectiles - visibleProjectiles} INVISIBLE BULLETS!");
            }
            GUI.color = Color.cyan;
        }
        else
        {
            GUI.color = Color.yellow;
            GUILayout.Label("⚠️ No Projectile Pool (match not started)");
            GUI.color = Color.cyan;
        }

        GUILayout.Space(10);
        GUI.color = Color.gray;
        GUILayout.Label("Press F3 to hide this debug panel");

        GUILayout.EndArea();
    }
}