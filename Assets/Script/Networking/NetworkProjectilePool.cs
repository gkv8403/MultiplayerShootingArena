using Fusion;
using Scripts.Gameplay;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Server-side pool. Spawns a bunch of NetworkObjects and uses a networked "IsActive" flag on the Projectile component to mark usage.
/// </summary>
public class NetworkProjectilePool : MonoBehaviour
{
    public static NetworkProjectilePool Instance { get; private set; }

    public NetworkPrefabRef projectilePrefab;
    public int poolSize = 30;
    private List<NetworkObject> pool = new List<NetworkObject>();
    private NetworkRunner runner;
    private bool initialized = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        Events.OnMatchStart += OnMatchStarted;
    }

    private void OnMatchStarted()
    {
        if (initialized) return;
        Invoke(nameof(InitializePool), 0.3f);
    }

    private void InitializePool()
    {
        runner = FindObjectOfType<NetworkRunner>();
        if (runner == null)
        {
            Invoke(nameof(InitializePool), 0.5f);
            return;
        }

        if (!runner.IsServer)
        {
            Debug.Log("[Pool] Not server - pool init skipped on client");
            return;
        }

        Debug.Log($"[Pool] Creating {poolSize} projectiles on server...");
        for (int i = 0; i < poolSize; i++)
        {
            var no = runner.Spawn(projectilePrefab, Vector3.one * 9999f, Quaternion.identity, PlayerRef.None);
            if (no != null)
            {
                pool.Add(no);
                // Ensure projectile component is inactive (server will set IsActive=false)
                var proj = no.GetComponent<Projectile>();
                if (proj != null)
                    proj.ForceDeactivateNetworked();
            }
        }

        initialized = true;
        Debug.Log($"[Pool] Initialized {pool.Count}");
    }

    public NetworkObject GetPooledProjectile()
    {
        if (!initialized)
        {
            Debug.LogWarning("[Pool] Not initialized yet");
            return null;
        }

        foreach (var no in pool)
        {
            if (no == null) continue;
            var proj = no.GetComponent<Projectile>();
            if (proj != null && !proj.IsActiveNetworked)
                return no;
        }

        Debug.LogWarning("[Pool] No available projectile");
        return null;
    }

    private void OnDestroy()
    {
        Events.OnMatchStart -= OnMatchStarted;
    }
}
