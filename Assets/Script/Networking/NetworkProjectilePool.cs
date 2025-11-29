using Fusion;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class NetworkProjectilePool : MonoBehaviour
{
    public NetworkPrefabRef projectilePrefab;
    public int poolSize = 20;
    private List<NetworkObject> pool = new List<NetworkObject>();
    private NetworkRunner runner;
    private bool initialized = false;

    private void Start()
    {
        Debug.Log("[Pool] Start() called - waiting for runner...");

        // Wait for runner to be spawned
        if (GameStateManager.Instance != null)
        {
            GameStateManager.Instance.OnRunnerSpawned += OnRunnerSpawned;
            Debug.Log("[Pool] Subscribed to OnRunnerSpawned event");
        }
        else
        {
            Debug.LogError("[Pool] GameStateManager.Instance not found!");
        }
    }

    private void OnRunnerSpawned(NetworkRunner newRunner)
    {
        if (initialized)
        {
            Debug.LogWarning("[Pool] Already initialized!");
            return;
        }

        Debug.Log("[Pool] OnRunnerSpawned callback received");
        runner = newRunner;

        if (runner == null)
        {
            Debug.LogError("[Pool] Runner is null!");
            return;
        }

        Debug.Log($"[Pool] NetworkRunner found. IsServer: {runner.IsServer}");

        if (!runner.IsServer)
        {
            Debug.LogWarning("[Pool] Not server, skipping pool initialization");
            return;
        }

        InitializePool();
    }

    private void InitializePool()
    {
        Debug.Log($"[Pool] Creating pool with {poolSize} projectiles...");

        for (int i = 0; i < poolSize; i++)
        {
            try
            {
                var no = runner.Spawn(projectilePrefab, Vector3.zero, Quaternion.identity, PlayerRef.Invalid);
                pool.Add(no);
                Debug.Log($"[Pool] Spawned projectile {i}: {no.gameObject.name}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Pool] Failed to spawn projectile {i}: {e.Message}");
            }
        }

        Debug.Log($"[Pool] Pool initialized with {pool.Count} projectiles");
        initialized = true;
    }

    public NetworkObject GetPooledProjectile()
    {
        if (!initialized)
        {
            Debug.LogError("[Pool] Pool not initialized yet!");
            return null;
        }

        if (runner == null || !runner.IsServer)
        {
            Debug.LogError("[Pool] Not server or runner null!");
            return null;
        }

        // Find first inactive projectile
        var projObj = pool.FirstOrDefault(n =>
        {
            if (n == null)
            {
                Debug.LogWarning("[Pool] Found null in pool!");
                return false;
            }

            var pb = n.GetComponent<Scripts.Gameplay.Projectile>();
            if (pb == null)
            {
                Debug.LogError($"[Pool] Projectile component missing on {n.gameObject.name}!");
                return false;
            }

            return !pb.IsActive;
        });

        if (projObj != null)
        {
            Debug.Log($"[Pool] Returning pooled projectile: {projObj.gameObject.name}");
            return projObj;
        }

        Debug.LogWarning("[Pool] No inactive projectile found!");
        return null;
    }

    private void OnDestroy()
    {
        if (GameStateManager.Instance != null)
            GameStateManager.Instance.OnRunnerSpawned -= OnRunnerSpawned;
    }

    private void OnGUI()
    {
        if (!initialized) return;

        GUILayout.Label($"[Pool] Total: {pool.Count}");
        int activeCount = pool.Count(p => p != null && p.GetComponent<Scripts.Gameplay.Projectile>().IsActive);
        GUILayout.Label($"[Pool] Active: {activeCount}");
    }
}
