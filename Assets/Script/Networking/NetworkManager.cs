using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Scripts.Gameplay;
using System;
using System.Threading.Tasks;

public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public NetworkRunner runnerPrefab;
    private NetworkRunner runner;

    public NetworkPrefabRef playerPrefab;
    public Transform[] spawnPoints;

    private const string GLOBAL_SESSION_NAME = "GLOBAL_ROOM";
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
        {
            runner.RemoveCallbacks(this);
        }
    }

    private void OnHostClicked() => StartRunner(true);
    private void OnQuickJoinClicked() => StartRunner(false);

    private async void StartRunner(bool makeHost)
    {
        if (isConnecting)
        {
            Debug.LogWarning("[NetworkManager] Already connecting!");
            Events.RaiseSetStatusText("Already connecting...");
            return;
        }

        if (runner != null)
        {
            Debug.LogWarning("[NetworkManager] Runner already exists!");
            Events.RaiseSetStatusText("Already in session");
            return;
        }

        isConnecting = true;
        connectionAttempts = 0;

        bool success = await TryConnect(makeHost);

        if (!success)
        {
            Events.RaiseSetStatusText("Connection failed. Try again.");
            isConnecting = false;
        }
    }

    private async Task<bool> TryConnect(bool makeHost)
    {
        while (connectionAttempts < MAX_CONNECTION_ATTEMPTS)
        {
            connectionAttempts++;
            string mode = makeHost ? "Host" : "Client";
            Events.RaiseSetStatusText($"{mode} connecting... ({connectionAttempts}/{MAX_CONNECTION_ATTEMPTS})");
            Debug.Log($"[NetworkManager] Attempt {connectionAttempts}: Starting as {mode}");

            try
            {
                // Create runner
                runner = Instantiate(runnerPrefab);
                runner.name = "NetworkRunner";
                runner.ProvideInput = true;
                runner.AddCallbacks(this);

                // Configure start arguments
                var args = new StartGameArgs()
                {
                    GameMode = makeHost ? GameMode.Host : GameMode.Client,
                    SessionName = GLOBAL_SESSION_NAME,
                    SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>(),
                    PlayerCount = 4, // Max 4 players
                };

                Debug.Log($"[NetworkManager] Starting game session: {GLOBAL_SESSION_NAME}");

                var result = await runner.StartGame(args);

                if (result.Ok)
                {
                    Debug.Log($"[NetworkManager] ✓ Connected successfully as {mode}");
                    Events.RaiseSetStatusText($"Connected as {mode}");
                    isConnecting = false;
                    return true;
                }
                else
                {
                    Debug.LogError($"[NetworkManager] Connection failed: {result.ShutdownReason}");
                    Events.RaiseSetStatusText($"Failed: {result.ShutdownReason}");

                    // Cleanup
                    if (runner != null)
                    {
                        runner.RemoveCallbacks(this);
                        Destroy(runner.gameObject);
                        runner = null;
                    }

                    // If host creation failed, try joining as client
                    if (makeHost && connectionAttempts < MAX_CONNECTION_ATTEMPTS)
                    {
                        Debug.Log("[NetworkManager] Host creation failed, trying to join as client...");
                        await Task.Delay(1000);
                        makeHost = false;
                        continue;
                    }

                    // Wait before retry
                    if (connectionAttempts < MAX_CONNECTION_ATTEMPTS)
                    {
                        await Task.Delay(2000);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[NetworkManager] Exception during connection: {ex.Message}");
                Events.RaiseSetStatusText($"Error: {ex.Message}");

                if (runner != null)
                {
                    runner.RemoveCallbacks(this);
                    Destroy(runner.gameObject);
                    runner = null;
                }

                if (connectionAttempts < MAX_CONNECTION_ATTEMPTS)
                {
                    await Task.Delay(2000);
                }
            }
        }

        Debug.LogError("[NetworkManager] All connection attempts failed");
        Events.RaiseSetStatusText("Connection failed after 3 attempts");
        return false;
    }

    private void OnRestartClicked()
    {
        Debug.Log("[NetworkManager] Restarting game...");

        if (runner != null)
        {
            runner.Shutdown();
        }

        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex
        );
    }

    private void OnLeaveClicked()
    {
        Debug.Log("[NetworkManager] Leaving session...");

        if (runner != null)
        {
            runner.Shutdown();
        }

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
            fire = im.CurrentFire
        };

        input.Set(data);
    }

    public void OnPlayerJoined(NetworkRunner runnerRef, PlayerRef player)
    {
        Debug.Log($"[NetworkManager] Player {player.PlayerId} joined");

        Events.RaiseSetStatusText($"Player joined ({runnerRef.ActivePlayers.Count()})");

        if (runnerRef.IsServer)
        {
            // Spawn player
            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;

            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                var sp = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
                pos = sp.position;
                rot = sp.rotation;
            }

            var playerObj = runnerRef.Spawn(playerPrefab, pos, rot, player);
            Debug.Log($"[NetworkManager] ✓ Spawned player {player.PlayerId} at {pos}");

            // Start match when 2+ players
            if (runnerRef.ActivePlayers.Count() >= 2)
            {
                Debug.Log("[NetworkManager] 2+ players - starting match!");
                Events.RaiseMatchStart();
            }
        }
    }

    public void OnPlayerLeft(NetworkRunner runnerRef, PlayerRef player)
    {
        Debug.Log($"[NetworkManager] Player {player.PlayerId} left");
        Events.RaiseSetStatusText($"Player left ({runnerRef.ActivePlayers.Count()})");

        if (runnerRef.ActivePlayers.Count() < 2)
        {
            Debug.Log("[NetworkManager] Less than 2 players - ending match");
            Events.RaiseMatchEnd();
        }
    }

    public void OnShutdown(NetworkRunner runnerRef, ShutdownReason shutdownReason)
    {
        Debug.Log($"[NetworkManager] Shutdown: {shutdownReason}");
        Events.RaiseSetStatusText($"Session ended: {shutdownReason}");
        Events.RaiseShowMenu(true);

        isConnecting = false;
        connectionAttempts = 0;

        if (runner != null)
        {
            runner.RemoveCallbacks(this);
            Destroy(runner.gameObject);
            runner = null;
        }
    }

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("[NetworkManager] Connected to server");
        Events.RaiseSetStatusText("Connected to server");
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"[NetworkManager] Connection failed: {reason}");
        Events.RaiseSetStatusText($"Connection failed: {reason}");
        isConnecting = false;
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.LogError($"[NetworkManager] Disconnected: {reason}");
        Events.RaiseSetStatusText($"Disconnected: {reason}");
    }

    // Empty implementations
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnPlayerConnected(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerDisconnected(NetworkRunner runner, PlayerRef player) { }
    public void OnObjectSpawned(NetworkRunner runner, NetworkObject obj) { }
    public void OnObjectDestroyed(NetworkRunner runner, NetworkObject obj) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
}