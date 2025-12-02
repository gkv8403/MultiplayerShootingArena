using Fusion;
using Fusion.Sockets;
using Scripts.Gameplay;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public NetworkRunner runnerPrefab;
    private NetworkRunner runner;

    public NetworkPrefabRef playerPrefab;
    public Transform[] spawnPoints;

    private string currentSessionName = "";
    private bool isConnecting = false;
    private int connectionAttempts = 0;
    private const int MAX_CONNECTION_ATTEMPTS = 3;

    private void Start()
    {
        Events.OnHostClicked += OnHostClicked;
        Events.OnQuickJoinClicked += OnQuickJoinClicked;
        Events.OnRestartClicked += OnRestartClicked;
        Events.OnLeaveClicked += OnLeaveClicked;

        Events.RaiseShowMenu(true);
        Events.RaiseSetStatusText("Ready to connect");
        Debug.Log("[NetworkManager] Initialized");
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

    private void OnHostClicked()
    {
        if (isConnecting) return;

        // Generate unique session name with timestamp
        currentSessionName = $"ARENA_{System.DateTime.UtcNow.Ticks}";
        Debug.Log($"[NetworkManager] Creating NEW session as Host: {currentSessionName}");

        StartRunner(GameMode.Host, currentSessionName);
    }

    private void OnQuickJoinClicked()
    {
        if (isConnecting) return;

        Debug.Log("[NetworkManager] Quick Join - searching for available rooms...");
        currentSessionName = ""; // Empty = search for any room

        StartRunner(GameMode.AutoHostOrClient, currentSessionName);
    }

    private async void StartRunner(GameMode mode, string sessionName)
    {
        if (isConnecting)
        {
            Events.RaiseSetStatusText("Already connecting...");
            return;
        }

        // Cleanup existing runner before starting new one
        if (runner != null)
        {
            Debug.Log("[NetworkManager] Cleaning up existing runner...");
            await runner.Shutdown();
            CleanupRunner();
            await Task.Delay(500);
        }

        isConnecting = true;
        connectionAttempts = 0;

        while (connectionAttempts < MAX_CONNECTION_ATTEMPTS)
        {
            connectionAttempts++;

            string statusMsg = mode == GameMode.Host
                ? $"Creating room... ({connectionAttempts}/{MAX_CONNECTION_ATTEMPTS})"
                : $"Finding room... ({connectionAttempts}/{MAX_CONNECTION_ATTEMPTS})";

            Events.RaiseSetStatusText(statusMsg);
            Debug.Log($"[NetworkManager] Attempt {connectionAttempts} - Mode: {mode}");

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

                Debug.Log($"[NetworkManager] Starting - Mode: {mode}, Session: {sessionName ?? "ANY"}");

                var startResult = await runner.StartGame(args);

                if (startResult.Ok)
                {
                    string roleText = runner.IsServer ? "Host/Server" : "Client";
                    currentSessionName = runner.SessionInfo.Name;

                    Debug.Log($"[NetworkManager] ✓ Connected as {roleText}");
                    Debug.Log($"[NetworkManager] Session: {currentSessionName}");

                    Events.RaiseSetStatusText($"Connected as {roleText}");
                    isConnecting = false;
                    Events.RaiseShowMenu(false);
                    return;
                }
                else
                {
                    Debug.LogWarning($"[NetworkManager] Start failed: {startResult.ShutdownReason}");

                    // If AutoHostOrClient failed to find room, become host
                    if (mode == GameMode.AutoHostOrClient && connectionAttempts >= 2)
                    {
                        Debug.Log("[NetworkManager] No rooms found, becoming host...");
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
                Debug.LogError($"[NetworkManager] Exception: {ex.Message}");
                Events.RaiseSetStatusText($"Error: {ex.Message}");
                CleanupRunner();
                await Task.Delay(1000);
            }
        }

        // All attempts failed
        Debug.LogError("[NetworkManager] Connection failed after all retries");
        Events.RaiseSetStatusText("Connection failed - Please retry");
        isConnecting = false;
        Events.RaiseShowMenu(true);
    }

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

    private void OnRestartClicked()
    {
        Debug.Log("[NetworkManager] Restart requested");

        if (runner != null && runner.IsServer)
        {
            // ✅ Use RPC to start match on all clients
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
            Debug.Log("[NetworkManager] Only host can restart");
            Events.RaiseSetStatusText("Only host can restart");
        }
    }

    private async void OnLeaveClicked()
    {
        Debug.Log("[NetworkManager] Leave clicked");

        if (runner != null)
        {
            await runner.Shutdown();
        }

        CleanupRunner();
        Events.RaiseShowMenu(true);
        Events.RaiseSetStatusText("Disconnected");
    }

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

    public void OnPlayerJoined(NetworkRunner runnerRef, PlayerRef player)
    {
        Debug.Log($"[NetworkManager] Player {player.PlayerId} joined (IsServer: {runnerRef.IsServer})");

        if (runnerRef.IsServer)
        {
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

            var playerObj = runnerRef.Spawn(playerPrefab, pos, rot, player);
            Debug.Log($"[NetworkManager] ✓ Spawned player {player.PlayerId} at {pos}");

            // ✅ Start match when 2+ players join
            if (runnerRef.ActivePlayers.Count() >= 2)
            {
                Debug.Log("[NetworkManager] 2+ players - starting match via RPC");

                // Wait a frame for all spawns to complete
                Invoke(nameof(StartMatchForAllClients), 0.5f);
            }
        }

        Events.RaiseSetStatusText($"Players: {runnerRef.ActivePlayers.Count()}");
    }

    // ✅ NEW: Start match using RPC
    private void StartMatchForAllClients()
    {
        if (runner == null || !runner.IsServer) return;

        var players = FindObjectsOfType<PlayerController>();
        if (players.Length > 0)
        {
            // Find any player with state authority to call RPC
            foreach (var p in players)
            {
                if (p.Object != null && p.Object.HasStateAuthority)
                {
                    Debug.Log($"[NetworkManager] 🎮 Starting match via {p.PlayerName}");
                    p.RPC_StartMatchOnAllClients();
                    break;
                }
            }
        }
    }

    public void OnPlayerLeft(NetworkRunner runnerRef, PlayerRef player)
    {
        Debug.Log($"[NetworkManager] Player {player.PlayerId} left");
        Events.RaiseSetStatusText($"Players: {runnerRef.ActivePlayers.Count()}");

        if (runnerRef.ActivePlayers.Count() < 2)
        {
            Debug.Log("[NetworkManager] Less than 2 players - ending match");
            Events.RaiseMatchEnd();
        }
    }

    public void OnShutdown(NetworkRunner runnerRef, ShutdownReason shutdownReason)
    {
        Debug.Log($"[NetworkManager] Shutdown: {shutdownReason}");
        CleanupRunner();
        Events.RaiseShowMenu(true);
        Events.RaiseSetStatusText($"Disconnected: {shutdownReason}");
        isConnecting = false;
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("[NetworkManager] Connected to server");
        Events.RaiseSetStatusText("Connected!");
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"[NetworkManager] Connect failed: {reason}");
        Events.RaiseSetStatusText($"Failed: {reason}");
        isConnecting = false;
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.LogWarning($"[NetworkManager] Disconnected: {reason}");
        Events.RaiseSetStatusText($"Disconnected: {reason}");
        isConnecting = false;
    }

    // Unused callbacks
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
    {
        Debug.Log($"[NetworkManager] Sessions found: {sessionList.Count}");
    }
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
}