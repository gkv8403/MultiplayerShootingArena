using Fusion;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// ===== FIXED: Debug all pool operations =====
public class NetworkProjectilePool : MonoBehaviour
{
    public NetworkPrefabRef projectilePrefab;
    public int poolSize = 20;
    private List<NetworkObject> pool = new List<NetworkObject>();
    private NetworkRunner runner;

    private void Start()
    {
        Debug.Log("[Pool] Start() called");

        runner = FindObjectsOfType<NetworkRunner>().FirstOrDefault();
        if (runner == null)
        {
            Debug.LogError("[Pool] No NetworkRunner found in scene!");
            return;
        }

        Debug.Log($"[Pool] NetworkRunner found. IsServer: {runner.IsServer}");

        if (!runner.IsServer)
        {
            Debug.LogWarning("[Pool] Not server, skipping pool initialization");
            return;
        }

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
    }

    /// ===== Get projectile from pool =====
    public NetworkObject GetPooledProjectile()
    {
        if (runner == null || !runner.IsServer)
        {
            Debug.LogError("[Pool] GetPooledProjectile called but not server!");
            return null;
        }

        Debug.Log($"[Pool] GetPooledProjectile called. Pool size: {pool.Count}");

        // Find inactive projectile
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

            bool isInactive = !pb.IsActive;
            if (isInactive)
                Debug.Log($"[Pool] Found inactive projectile: {n.gameObject.name}");

            return isInactive;
        });

        if (projObj != null)
        {
            Debug.Log($"[Pool] Returning pooled projectile: {projObj.gameObject.name}");
            return projObj;
        }

        Debug.LogWarning("[Pool] No inactive projectile found, expanding pool...");

        // Expand pool
        if (pool.Count < poolSize * 2)
        {
            try
            {
                var no = runner.Spawn(projectilePrefab, Vector3.zero, Quaternion.identity, PlayerRef.Invalid);
                pool.Add(no);
                Debug.Log($"[Pool] Expanded pool, new projectile: {no.gameObject.name}");
                return no;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Pool] Failed to expand pool: {e.Message}");
            }
        }

        Debug.LogError("[Pool] No projectile available and pool at max size!");
        return null;
    }

    private void OnGUI()
    {
        // Display pool status on screen
        GUILayout.Label($"Pool Size: {pool.Count}");
        int activeCount = pool.Count(p => p != null && p.GetComponent<Scripts.Gameplay.Projectile>().IsActive);
        GUILayout.Label($"Active: {activeCount}");
    }
}