using Fusion;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
        Debug.Log("[Pool] Instance created");
    }

    private void Start()
    {
        Events.OnMatchStart += OnMatchStarted;
    }

    private void OnMatchStarted()
    {
        if (initialized) return;

        Debug.Log("[Pool] Match started - initializing...");
        Invoke(nameof(InitializePool), 0.5f);
    }

    private void InitializePool()
    {
        runner = FindObjectOfType<NetworkRunner>();

        if (runner == null)
        {
            Debug.LogError("[Pool] Runner not found!");
            Invoke(nameof(InitializePool), 0.5f);
            return;
        }

        if (!runner.IsServer)
        {
            Debug.Log("[Pool] Not server, skipping pool init");
            return;
        }

        Debug.Log($"[Pool] Creating {poolSize} projectiles...");

        for (int i = 0; i < poolSize; i++)
        {
            // Spawn at origin
            var no = runner.Spawn(
                projectilePrefab,
                Vector3.zero,
                Quaternion.identity,
                PlayerRef.None
            );

            if (no != null)
            {
                pool.Add(no);

                // Disable the GameObject immediately
                no.gameObject.SetActive(false);

                Debug.Log($"[Pool] Created projectile {i + 1}/{poolSize}");
            }
        }

        initialized = true;
        Debug.Log($"[Pool] ✓ Pool ready with {pool.Count} projectiles");
    }

    public NetworkObject GetPooledProjectile()
    {
        if (!initialized)
        {
            Debug.LogError("[Pool] Not initialized!");
            return null;
        }

        // Find first inactive projectile
        foreach (var no in pool)
        {
            if (no == null) continue;

            // Check if GameObject is inactive
            if (!no.gameObject.activeSelf)
            {
                return no;
            }

            // Or check via Projectile component
            var proj = no.GetComponent<Scripts.Gameplay.Projectile>();
            if (proj != null && !proj.IsActive)
            {
                return no;
            }
        }

        Debug.LogWarning("[Pool] No inactive projectile! Increase pool size.");
        return null;
    }

    private void OnDestroy()
    {
        Events.OnMatchStart -= OnMatchStarted;
    }

    private void OnGUI()
    {
        if (!initialized) return;

        int active = pool.Count(p => p != null && p.gameObject.activeSelf);

        GUILayout.BeginArea(new Rect(10, 100, 200, 100));
        GUILayout.Label($"Pool Total: {pool.Count}");
        GUILayout.Label($"Pool Active: {active}");
        GUILayout.Label($"Pool Available: {pool.Count - active}");
        GUILayout.EndArea();
    }
}