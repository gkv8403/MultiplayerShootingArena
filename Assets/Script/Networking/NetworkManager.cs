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

    private const string GLOBAL_SESSION_NAME = "GLOBAL_MULTIPLAYER_ROOM";
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

    private void OnHostClicked() => StartRunnerAsHost();
    private void OnQuickJoinClicked() => StartRunnerQuickJoin();

    // Create a new session as Host
    public void StartRunnerAsHost()
    {
        if (isConnecting) return;
        Debug.Log("[NetworkManager] Starting as explicit Host");
        StartRunner(GameMode.Host);
    }

    // Try to join existing session, or create new one if none available
    public void StartRunnerQuickJoin()
    {
        if (isConnecting) return;
        Debug.Log("[NetworkManager] Starting Quick Join (AutoHostOrClient)");
        StartRunner(GameMode.AutoHostOrClient);
    }

    private async void StartRunner(GameMode mode)
    {
        if (isConnecting)
        {
            Events.RaiseSetStatusText("Already connecting...");
            return;
        }

        if (runner != null)
        {
            Events.RaiseSetStatusText("Already connected");
            return;
        }

        isConnecting = true;
        connectionAttempts = 0;

        while (connectionAttempts < MAX_CONNECTION_ATTEMPTS)
        {
            connectionAttempts++;
            Events.RaiseSetStatusText($"Connecting... ({connectionAttempts}/{MAX_CONNECTION_ATTEMPTS})");
            Debug.Log($"[NetworkManager] Connection attempt {connectionAttempts} - Mode: {mode}");

            try
            {
                // Create runner instance
                runner = Instantiate(runnerPrefab);
                runner.name = "NetworkRunner";
                runner.ProvideInput = true;
                runner.AddCallbacks(this);

                var sceneManager = runner.gameObject.AddComponent<NetworkSceneManagerDefault>();

                var args = new StartGameArgs()
                {
                    GameMode = mode,
                    SessionName = GLOBAL_SESSION_NAME,
                    PlayerCount = 16,
                    SceneManager = sceneManager,
                    // Add session properties for better matchmaking
                    SessionProperties = new Dictionary<string, SessionProperty>
                    {
                        { "GameType", "Arena" },
                        { "Version", "1.0" }
                    }
                };

                Debug.Log($"[NetworkManager] Starting game with mode: {mode}, session: {GLOBAL_SESSION_NAME}");

                var startResult = await runner.StartGame(args);

                if (startResult.Ok)
                {
                    string roleText = runner.IsServer ? "Host/Server" : "Client";
                    Debug.Log($"[NetworkManager] ✓ Started as {roleText}");
                    Events.RaiseSetStatusText($"Connected as {roleText}");
                    isConnecting = false;
                    Events.RaiseShowMenu(false);
                    return;
                }
                else
                {
                    Debug.LogError($"[NetworkManager] Start failed: {startResult.ShutdownReason}");
                    Events.RaiseSetStatusText($"Failed: {startResult.ShutdownReason}");
                    CleanupRunner();
                    await Task.Delay(1000);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] Exception: {ex.Message}\n{ex.StackTrace}");
                Events.RaiseSetStatusText($"Error: {ex.Message}");
                CleanupRunner();
                await Task.Delay(1000);
            }
        }

        Events.RaiseSetStatusText("Connection failed after retries");
        isConnecting = false;

        // Show retry option
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
            // Server/Host restarts the match
            Events.RaiseMatchStart();

            // Reset all players
            var players = FindObjectsOfType<PlayerController>();
            foreach (var p in players)
            {
                if (p.Object != null && p.Object.HasStateAuthority)
                {
                    // Reset stats
                    p.Kills = 0;
                    p.Deaths = 0;
                    p.Health = p.maxHealth;
                    p.RPC_BroadcastScore(p.PlayerName, 0);
                }
            }

            Events.RaiseSetStatusText("Match restarted!");
            Events.RaiseShowMenu(false);
        }
        else
        {
            // Client requests restart via RPC (you can implement this)
            Debug.Log("[NetworkManager] Client requesting restart...");
            Events.RaiseSetStatusText("Restart requested...");
        }
    }

    private void OnLeaveClicked()
    {
        Debug.Log("[NetworkManager] Leave clicked");

        if (runner != null)
        {
            runner.Shutdown();
        }

        CleanupRunner();
        Events.RaiseShowMenu(true);
        Events.RaiseSetStatusText("Disconnected");
    }

    // Input forwarding to network
    public void OnInput(NetworkRunner runnerRef, NetworkInput input)
    {
        var im = InputManager.Instance;
        if (im == null) return;

        var data = new NetworkInputData
        {
            moveInput = im.CurrentMoveInput,
            lookInput = im.CurrentLookDelta,
            fire = im.CurrentFire
        };

        input.Set(data);
    }

    public void OnPlayerJoined(NetworkRunner runnerRef, PlayerRef player)
    {
        Debug.Log($"[NetworkManager] Player {player.PlayerId} joined (IsServer: {runnerRef.IsServer})");

        // Spawn player only on server/state authority
        if (runnerRef.IsServer)
        {
            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;

            // Get spawn point
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                var sp = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
                pos = sp.position;

                // Calculate rotation to face center
                Vector3 dirToCenter = (Vector3.zero - pos);
                dirToCenter.y = 0;
                if (dirToCenter.sqrMagnitude > 0.001f)
                    rot = Quaternion.LookRotation(dirToCenter.normalized, Vector3.up);
                else
                    rot = sp.rotation;
            }

            var playerObj = runnerRef.Spawn(playerPrefab, pos, rot, player);
            Debug.Log($"[NetworkManager] ✓ Spawned player {player.PlayerId} at {pos}");

            // Start match when 2+ players present
            if (runnerRef.ActivePlayers.Count() >= 2)
            {
                Debug.Log("[NetworkManager] 2+ players detected - starting match");
                Events.RaiseMatchStart();
            }
        }

        Events.RaiseSetStatusText($"Players: {runnerRef.ActivePlayers.Count()}");
    }

    public void OnPlayerLeft(NetworkRunner runnerRef, PlayerRef player)
    {
        Debug.Log($"[NetworkManager] Player {player.PlayerId} left");
        Events.RaiseSetStatusText($"Players: {runnerRef.ActivePlayers.Count()}");

        // End match if less than 2 players
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
        Events.RaiseSetStatusText("Connected to server");
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"[NetworkManager] Connect failed: {reason}");
        Events.RaiseSetStatusText($"Connection failed: {reason}");
        isConnecting = false;
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.LogWarning($"[NetworkManager] Disconnected from server: {reason}");
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

    // These are handled by OnPlayerJoined/OnPlayerLeft
    public void OnPlayerConnected(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerDisconnected(NetworkRunner runner, PlayerRef player) { }
    public void OnObjectSpawned(NetworkRunner runner, NetworkObject obj) { }
    public void OnObjectDestroyed(NetworkRunner runner, NetworkObject obj) { }
}