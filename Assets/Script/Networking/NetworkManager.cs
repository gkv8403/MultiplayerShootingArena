using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Scripts.Gameplay;

// ===== FIXED V2: Ensures client UI updates correctly =====
public class NetworkManager : MonoBehaviour, INetworkRunnerCallbacks
{
    public NetworkRunner runnerPrefab;
    private NetworkRunner runner;

    public NetworkPrefabRef playerPrefab;
    public Transform[] spawnPoints;

    private const string GLOBAL_SESSION_NAME = "GLOBAL_ROOM";
    private bool isConnecting = false;
    private int playerCount = 0;

    private void Start()
    {
        Events.OnHostClicked += OnHostClicked;
        Events.OnQuickJoinClicked += OnQuickJoinClicked;
        Events.OnRestartClicked += OnRestartClicked;
        Events.OnLeaveClicked += OnLeaveClicked;

        // ===== FIX: Start with menu visible =====
        Events.RaiseShowMenu(true);
        Events.RaiseSetStatusText("Ready");
        Debug.Log("[NetworkManager] Started, showing menu");
    }

    private void OnDestroy()
    {
        Events.OnHostClicked -= OnHostClicked;
        Events.OnQuickJoinClicked -= OnQuickJoinClicked;
        Events.OnRestartClicked -= OnRestartClicked;
        Events.OnLeaveClicked -= OnLeaveClicked;
    }

    private void OnHostClicked() => StartRunner(true);
    private void OnQuickJoinClicked() => StartRunner(false);

    private async void StartRunner(bool makeHost)
    {
        if (isConnecting || runner != null)
        {
            Debug.LogWarning("[NetworkManager] Already connecting!");
            return;
        }

        isConnecting = true;
        Events.RaiseSetStatusText(makeHost ? "Creating session..." : "Joining session...");
        Debug.Log($"[NetworkManager] Starting as {(makeHost ? "Host" : "Client")}");

        runner = Instantiate(runnerPrefab);
        runner.ProvideInput = true;
        runner.AddCallbacks(this);

        var args = new StartGameArgs()
        {
            GameMode = makeHost ? GameMode.Host : GameMode.Client,
            SessionName = GLOBAL_SESSION_NAME,
            SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
        };

        var res = await runner.StartGame(args);

        if (!res.Ok)
        {
            Debug.LogError($"[NetworkManager] Failed to start game: {res.ShutdownReason}");
            Events.RaiseSetStatusText($"Failed: {res.ShutdownReason}");
            Destroy(runner.gameObject);
            runner = null;
            isConnecting = false;
            return;
        }

        isConnecting = false;
        Events.RaiseSetStatusText(makeHost ? "Host started" : "Joined match");
        Debug.Log("[NetworkManager] Session started successfully");
    }

    private void OnRestartClicked()
    {
        if (runner != null) runner.Shutdown();
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    private void OnLeaveClicked()
    {
        if (runner != null) runner.Shutdown();
    }

    // ===== INetworkRunnerCallbacks =====
    public void OnInput(NetworkRunner runnerRef, NetworkInput input)
    {
        var im = InputManager.Instance;
        if (im == null) return;

        var data = new Scripts.Gameplay.NetworkInputData
        {
            moveInput = im.CurrentMoveInput,
            lookInput = im.CurrentLookDelta,
            fire = im.CurrentFire
        };

        input.Set(data);
    }

    public void OnPlayerJoined(NetworkRunner runnerRef, PlayerRef player)
    {
        Debug.Log($"[NetworkManager] Player joined: {player.PlayerId}");
        playerCount++;

        if (runnerRef.IsServer)
        {
            Vector3 pos = Vector3.zero;
            Quaternion rot = Quaternion.identity;

            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                var sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
                pos = sp.position + Vector3.up * 0.5f;
                rot = sp.rotation;
                Debug.Log($"[NetworkManager] Spawning at: {sp.name} ({pos})");
            }

            var no = runnerRef.Spawn(playerPrefab, pos, rot, player);
            Debug.Log($"[NetworkManager] Spawned player {player.PlayerId}");

            // ===== FIX V2: Check if enough players and start match =====
            if (runnerRef.ActivePlayers.Count() >= 2)
            {
                Debug.Log("[NetworkManager] 2+ players reached - starting match");
                Events.RaiseMatchStart();
            }
        }
        else
        {
            // ===== FIX V2: For CLIENT - also check and trigger match start =====
            Debug.Log($"[NetworkManager] Client sees {runnerRef.ActivePlayers.Count()} active players");
            if (runnerRef.ActivePlayers.Count() >= 2)
            {
                Debug.Log("[NetworkManager] CLIENT: 2+ players - should start match");
                Events.RaiseMatchStart();
            }
        }
    }

    public void OnPlayerLeft(NetworkRunner runnerRef, PlayerRef player)
    {
        playerCount--;
        Events.RaiseSetStatusText($"Player left ({runnerRef.ActivePlayers.Count()})");
        if (runnerRef.ActivePlayers.Count() < 2)
        {
            Events.RaiseMatchEnd();
            Debug.Log("[NetworkManager] Match ended (less than 2 players)");
        }
    }

    public void OnShutdown(NetworkRunner runnerRef, ShutdownReason shutdownReason)
    {
        Debug.Log($"[NetworkManager] Shutdown: {shutdownReason}");
        Events.RaiseSetStatusText($"Session ended: {shutdownReason}");
        Events.RaiseShowMenu(true);
        isConnecting = false;
        playerCount = 0;

        if (runner != null)
        {
            runner.RemoveCallbacks(this);
            Destroy(runner.gameObject);
            runner = null;
        }
    }

    // Stubbed callbacks
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Events.RaiseSetStatusText($"Connect failed: {reason}");
        Debug.LogError($"[NetworkManager] Connection failed: {reason}");
        isConnecting = false;
    }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSessionListUpdated(NetworkRunner runner, System.Collections.Generic.List<SessionInfo> sessionList) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, System.Collections.Generic.Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, System.ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnPlayerConnected(NetworkRunner runner, PlayerRef player) { }
    public void OnPlayerDisconnected(NetworkRunner runner, PlayerRef player) { }
    public void OnObjectSpawned(NetworkRunner runner, NetworkObject obj) { }
    public void OnObjectDestroyed(NetworkRunner runner, NetworkObject obj) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnConnectedToServer(NetworkRunner runner, NetAddress remoteAddress) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Events.RaiseSetStatusText($"Disconnected: {reason}");
        Debug.LogError($"[NetworkManager] Disconnected: {reason}");
    }
}