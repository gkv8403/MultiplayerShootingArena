using Fusion;
using Fusion.Sockets;
using Scripts.Gameplay;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Manages network connections, session creation, and player spawning.
/// Implements INetworkRunnerCallbacks for Fusion networking events.
/// </summary>
public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Network Configuration")]
    public NetworkRunner runnerPrefab;
    public NetworkPrefabRef playerPrefab;
    public Transform[] spawnPoints;

    private NetworkRunner runner;
    private string currentSessionName = "";
    private bool isConnecting = false;
    private int connectionAttempts = 0;
    private const int MAX_CONNECTION_ATTEMPTS = 3;

    private void Start()
    {
        // Subscribe to UI events
        Events.OnHostClicked += OnHostClicked;
        Events.OnQuickJoinClicked += OnQuickJoinClicked;
        Events.OnRestartClicked += OnRestartClicked;
        Events.OnLeaveClicked += OnLeaveClicked;

        Events.RaiseShowMenu(true);
        Events.RaiseSetStatusText("Ready to connect");
    }

    private void OnDestroy()
    {
        Events.OnHostClicked -= OnHostClicked;
        Events.OnQuickJoinClicked -= OnQuickJoinClicked;
        Events.OnRestartClicked -= OnRestartClicked;
        Events.OnLeaveClicked -= OnLeaveClicked;

        if (runner != null)
            runner.RemoveCallbacks(this);
    }

    /// <summary>
    /// Handle host button click - create new session
    /// </summary>
    private void OnHostClicked()
    {
        if (isConnecting) return;

        currentSessionName = $"ARENA_{System.DateTime.UtcNow.Ticks}";
        StartRunner(GameMode.Host, currentSessionName);
    }

    /// <summary>
    /// Handle quick join button click - find available session
    /// </summary>
    private void OnQuickJoinClicked()
    {
        if (isConnecting) return;

        currentSessionName = "";
        StartRunner(GameMode.AutoHostOrClient, currentSessionName);
    }

    /// <summary>
    /// Start network runner with specified mode and session
    /// </summary>
    private async void StartRunner(GameMode mode, string sessionName)
    {
        if (isConnecting)
        {
            Events.RaiseSetStatusText("Already connecting...");
            return;
        }

        // Cleanup existing runner
        if (runner != null)
        {
            await runner.Shutdown();
            CleanupRunner();
            await Task.Delay(500);
        }

        isConnecting = true;
        connectionAttempts = 0;

        // Retry loop
        while (connectionAttempts < MAX_CONNECTION_ATTEMPTS)
        {
            connectionAttempts++;

            string statusMsg = mode == GameMode.Host
                ? $"Creating room... ({connectionAttempts}/{MAX_CONNECTION_ATTEMPTS})"
                : $"Finding room... ({connectionAttempts}/{MAX_CONNECTION_ATTEMPTS})";

            Events.RaiseSetStatusText(statusMsg);

            try
            {
                runner = Instantiate(runnerPrefab);
                runner.name = "NetworkRunner";
                runner.ProvideInput = true;
                runner.AddCallbacks(this);

                var sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

                var args = new StartGameArgs()
                {
                    GameMode = mode,
                    SessionName = string.IsNullOrEmpty(sessionName) ? null : sessionName,
                    PlayerCount = 16,
                    SceneManager = sceneManager,
                    SessionProperties = new Dictionary<string, SessionProperty>
                    {
                        { "GameType", "Arena" },
                        { "Version", "1.0" },
                        { "Map", "Arena_Default" }
                    },
                    CustomLobbyName = "DefaultLobby"
                };

                var startResult = await runner.StartGame(args);

                if (startResult.Ok)
                {
                    string roleText = runner.IsServer ? "Host" : "Client";
                    currentSessionName = runner.SessionInfo.Name;

                    Events.RaiseSetStatusText($"Connected as {roleText}");
                    isConnecting = false;
                    Events.RaiseShowMenu(false);
                    return;
                }
                else
                {
                    // If AutoHostOrClient failed to find room, become host
                    if (mode == GameMode.AutoHostOrClient && connectionAttempts >= 2)
                    {
                        CleanupRunner();
                        currentSessionName = $"ARENA_{System.DateTime.UtcNow.Ticks}";
                        mode = GameMode.Host;
                        connectionAttempts = 0;
                        continue;
                    }

                    Events.RaiseSetStatusText($"Attempt {connectionAttempts} failed");
                    CleanupRunner();
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Events.RaiseSetStatusText($"Error: {ex.Message}");
                CleanupRunner();
                await Task.Delay(1000);
            }
        }

        // All attempts failed
        Events.RaiseSetStatusText("Connection failed - Please retry");
        isConnecting = false;
        Events.RaiseShowMenu(true);
    }

    /// <summary>
    /// Clean up runner instance
    /// </summary>
    private void CleanupRunner()
    {
        if (runner != null)
        {
            runner.RemoveCallbacks(this);
            if (runner.gameObject != null)
                Destroy(runner.gameObject);
            runner = null;
        }
    }

    /// <summary>
    /// Handle restart button click
    /// </summary>
    private void OnRestartClicked()
    {
        if (runner != null && runner.IsServer)
        {
            // Use RPC to start match on all clients
            var players = FindObjectsOfType<PlayerController>();
            if (players.Length > 0 && players[0].Object != null && players[0].Object.HasStateAuthority)
            {
                players[0].RPC_StartMatchOnAllClients();
            }

            // Reset all players on server
            foreach (var p in players)
            {
                if (p.Object != null && p.Object.HasStateAuthority)
                {
                    p.Kills = 0;
                    p.Deaths = 0;
                    p.Health = p.maxHealth;
                    p.RPC_UpdateScoreOnAllClients(p.PlayerName, 0);
                }
            }

            Events.RaiseSetStatusText("Match restarted!");
        }
        else
        {
            Events.RaiseSetStatusText("Only host can restart");
        }
    }

    /// <summary>
    /// Handle leave button click - disconnect and return to menu
    /// </summary>
    private async void OnLeaveClicked()
    {
        if (runner != null)
        {
            await runner.Shutdown();
        }

        CleanupRunner();
        Events.RaiseShowMenu(true);
        Events.RaiseSetStatusText("Disconnected");
    }

    #region Fusion Callbacks

    /// <summary>
    /// Provide input data to Fusion
    /// </summary>
    public void OnInput(NetworkRunner runnerRef, NetworkInput input)
    {
        var im = InputManager.Instance;
        if (im == null) return;

        var data = new NetworkInputData
        {
            moveInput = im.CurrentMoveInput,
            lookInput = im.CurrentLookDelta,
            fire = im.CurrentFire,
            verticalMove = im.CurrentVerticalMove
        };

        input.Set(data);
    }

    /// <summary>
    /// Called when a player joins the session
    /// </summary>
    public void OnPlayerJoined(NetworkRunner runnerRef, PlayerRef player)
    {
        if (runnerRef.IsServer)
        {
            // Spawn player at random spawn point
            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;

            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                var sp = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
                pos = sp.position;

                Vector3 dirToCenter = (Vector3.zero - pos);
                dirToCenter.y = 0;
                if (dirToCenter.sqrMagnitude > 0.001f)
                    rot = Quaternion.LookRotation(dirToCenter.normalized, Vector3.up);
                else
                    rot = sp.rotation;
            }

            runnerRef.Spawn(playerPrefab, pos, rot, player);

            // Start match when 2+ players join
            if (runnerRef.ActivePlayers.Count() >= 2)
            {
                Invoke(nameof(StartMatchForAllClients), 0.5f);
            }
        }

        Events.RaiseSetStatusText($"Players: {runnerRef.ActivePlayers.Count()}");
    }

    /// <summary>
    /// Start match using RPC when enough players join
    /// </summary>
    private void StartMatchForAllClients()
    {
        if (runner == null || !runner.IsServer) return;

        var players = FindObjectsOfType<PlayerController>();
        if (players.Length > 0)
        {
            foreach (var p in players)
            {
                if (p.Object != null && p.Object.HasStateAuthority)
                {
                    p.RPC_StartMatchOnAllClients();
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Called when a player leaves the session
    /// </summary>
    public void OnPlayerLeft(NetworkRunner runnerRef, PlayerRef player)
    {
        Events.RaiseSetStatusText($"Players: {runnerRef.ActivePlayers.Count()}");

        if (runnerRef.ActivePlayers.Count() < 2)
        {
            Events.RaiseMatchEnd();
        }
    }

    /// <summary>
    /// Called when runner shuts down
    /// </summary>
    public void OnShutdown(NetworkRunner runnerRef, ShutdownReason shutdownReason)
    {
        CleanupRunner();
        Events.RaiseShowMenu(true);
        Events.RaiseSetStatusText($"Disconnected: {shutdownReason}");
        isConnecting = false;
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Events.RaiseSetStatusText("Connected!");
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Events.RaiseSetStatusText($"Failed: {reason}");
        isConnecting = false;
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Events.RaiseSetStatusText($"Disconnected: {reason}");
        isConnecting = false;
    }

    // Unused callbacks
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnPlayerConnected(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerDisconnected(NetworkRunner runner, PlayerRef player) { }
    public void OnObjectSpawned(NetworkRunner runner, NetworkObject obj) { }
    public void OnObjectDestroyed(NetworkRunner runner, NetworkObject obj) { }

    #endregion
}