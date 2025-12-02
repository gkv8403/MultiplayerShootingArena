using Fusion;
using Scripts.Gameplay;
using UnityEngine;

public class Projectile : NetworkBehaviour
{
    [Networked] public bool IsActiveNetworked { get; set; }
    [Networked] public PlayerRef Owner { get; set; }
    [Networked] public Vector3 NetworkPosition { get; set; }
    [Networked] public Vector3 NetworkVelocity { get; set; }
    [Networked] public TickTimer LifeTimer { get; set; }

    public float speed = 40f;
    public int damage = 20;
    public float lifeSeconds = 4f;
    public bool IsActive => IsActiveNetworked;

    private TrailRenderer trail;
    private MeshRenderer meshRenderer;
    private bool isInitialized = false;

    // ✅ Track state changes manually
    private bool lastActiveState = false;

    private void Awake()
    {
        meshRenderer = GetComponentInChildren<MeshRenderer>();
        trail = GetComponentInChildren<TrailRenderer>();

        if (meshRenderer == null)
        {
            Debug.LogError("[Projectile] ⚠️ NO MESHRENDERER FOUND! Check your prefab!");

            // Try to find it more aggressively
            var allRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in allRenderers)
            {
                if (r is MeshRenderer)
                {
                    meshRenderer = (MeshRenderer)r;
                    Debug.Log($"[Projectile] ✓ Found MeshRenderer: {meshRenderer.gameObject.name}");
                    break;
                }
            }
        }
        else
        {
            Debug.Log($"[Projectile] ✓ MeshRenderer found: {meshRenderer.gameObject.name}");
        }

        // Start hidden
        SetVisibility(false);
    }

    public override void Spawned()
    {
        base.Spawned();
        isInitialized = true;

        Debug.Log($"[Projectile] Spawned - Authority: {Object.HasStateAuthority}, ID: {Object.Id}");

        if (Object.HasStateAuthority)
        {
            ForceDeactivateNetworked();
        }

        lastActiveState = IsActiveNetworked;
        SetVisibility(IsActiveNetworked);
    }

    public void ForceDeactivateNetworked()
    {
        if (!Object.HasStateAuthority) return;

        IsActiveNetworked = false;
        Owner = PlayerRef.None;
        NetworkVelocity = Vector3.zero;
        LifeTimer = TickTimer.None;
        NetworkPosition = Vector3.one * 9999f;

        transform.position = NetworkPosition;
        SetVisibility(false);
    }

    public void Fire(PlayerRef owner, Vector3 position, Vector3 direction)
    {
        if (!Object.HasStateAuthority) return;

        Debug.Log($"[Projectile] 🔫 SERVER: Firing from {position} for Player{owner.PlayerId}");

        Owner = owner;
        NetworkPosition = position;
        NetworkVelocity = direction.normalized * speed;
        LifeTimer = TickTimer.CreateFromSeconds(Runner, lifeSeconds);

        transform.position = position;

        // Set active and show
        IsActiveNetworked = true;
        SetVisibility(true);

        Debug.Log($"[Projectile] ✅ SERVER: Projectile activated for Player{owner.PlayerId}");
    }

    public override void FixedUpdateNetwork()
    {
        if (!isInitialized) return;

        // ✅ CRITICAL: Check for state changes FIRST on all machines
        if (lastActiveState != IsActiveNetworked)
        {
            string role = Object.HasStateAuthority ? "SERVER" : "CLIENT";
            Debug.Log($"[Projectile] 🔄 {role}: State changed {lastActiveState} → {IsActiveNetworked}");

            SetVisibility(IsActiveNetworked);
            lastActiveState = IsActiveNetworked;

            // Force update on client
            if (!Object.HasStateAuthority && IsActiveNetworked)
            {
                Debug.Log($"[Projectile] 🔥 CLIENT: Projectile activated at {NetworkPosition}");
            }
        }

        if (Object.HasStateAuthority)
        {
            ServerSimulation();
        }
        else
        {
            ClientSimulation();
        }
    }

    private void ServerSimulation()
    {
        if (!IsActiveNetworked)
        {
            return;
        }

        // Check lifetime
        if (LifeTimer.Expired(Runner))
        {
            Debug.Log("[Projectile] ⏱️ SERVER: Expired");
            Deactivate();
            return;
        }

        // Calculate movement
        Vector3 movement = NetworkVelocity * Runner.DeltaTime;
        Vector3 nextPosition = NetworkPosition + movement;

        // Bounds check
        if (nextPosition.magnitude > 2000f)
        {
            Debug.Log("[Projectile] 🚫 SERVER: Out of bounds");
            Deactivate();
            return;
        }

        // Hit detection
        Vector3 direction = NetworkVelocity.normalized;
        float distance = movement.magnitude;

        if (Physics.Raycast(NetworkPosition, direction, out RaycastHit hit, distance + 0.5f))
        {
            Debug.Log($"[Projectile] 💥 SERVER: Hit {hit.collider.name}");

            var hitObj = hit.collider.GetComponentInParent<NetworkObject>();
            if (hitObj != null)
            {
                var pc = hitObj.GetComponent<PlayerController>();
                if (pc != null && pc.Object != null)
                {
                    if (pc.Object.InputAuthority != Owner && !pc.IsDead)
                    {
                        Debug.Log($"[Projectile] 🎯 SERVER: Hit player {pc.PlayerName} | Attacker: Player{Owner.PlayerId}");
                        pc.ApplyDamageServerSide(damage, Owner);
                    }
                    else if (pc.Object.InputAuthority == Owner)
                    {
                        Debug.Log($"[Projectile] ⚠️ SERVER: Can't hit self");
                    }
                }
            }

            Deactivate();
            return;
        }

        // Update position
        NetworkPosition = nextPosition;
        transform.position = nextPosition;
    }

    private void ClientSimulation()
    {
        if (!IsActiveNetworked)
        {
            return;
        }

        // Smooth interpolation
        transform.position = Vector3.Lerp(transform.position, NetworkPosition, 20f * Runner.DeltaTime);
    }

    // ✅ CRITICAL FIX: More aggressive visibility control
    private void SetVisibility(bool visible)
    {
        string role = Object != null && Object.HasStateAuthority ? "SERVER" : "CLIENT";

        Debug.Log($"[Projectile] 👁️ {role}: SetVisibility({visible}) called");

        // Method 1: Force specific MeshRenderer
        if (meshRenderer != null)
        {
            meshRenderer.enabled = visible;
            Debug.Log($"[Projectile] 👁️ {role}: MeshRenderer.enabled = {visible} on {meshRenderer.gameObject.name}");
        }
        else
        {
            Debug.LogError($"[Projectile] ❌ {role}: MeshRenderer is NULL!");
        }

        // Method 2: Force ALL MeshRenderers (redundant but safe)
        var allMeshRenderers = GetComponentsInChildren<MeshRenderer>(true);
        Debug.Log($"[Projectile] 👁️ {role}: Found {allMeshRenderers.Length} MeshRenderers");

        foreach (var mr in allMeshRenderers)
        {
            if (mr != null)
            {
                mr.enabled = visible;
                Debug.Log($"[Projectile] 👁️ {role}: Set {mr.gameObject.name} MeshRenderer to {visible}");
            }
        }

        // Method 3: Force all non-trail renderers
        var allRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in allRenderers)
        {
            if (r != null && !(r is TrailRenderer))
            {
                r.enabled = visible;
            }
        }

        // Handle trail
        if (trail != null)
        {
            if (visible && !trail.enabled)
            {
                trail.Clear();
                trail.enabled = true;
            }
            else if (!visible && trail.enabled)
            {
                trail.enabled = false;
            }
        }

        // Final verification
        if (meshRenderer != null)
        {
            Debug.Log($"[Projectile] ✓ {role}: Final MeshRenderer state = {meshRenderer.enabled}");
        }
    }

    private void Deactivate()
    {
        if (!Object.HasStateAuthority) return;
        Debug.Log($"[Projectile] 🔴 SERVER: Deactivating");
        ForceDeactivateNetworked();
    }

    // ✅ DIAGNOSTIC: Visual indicator
    private void OnGUI()
    {
        if (!Application.isPlaying || !IsActiveNetworked) return;
        if (Camera.main == null) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
        if (screenPos.z > 0 && screenPos.x > 0 && screenPos.x < Screen.width && screenPos.y > 0 && screenPos.y < Screen.height)
        {
            string role = Object.HasStateAuthority ? "S" : "C";
            string meshStatus = meshRenderer != null && meshRenderer.enabled ? "✓" : "✗";

            GUI.color = Object.HasStateAuthority ? Color.green : Color.yellow;
            GUI.Label(new Rect(screenPos.x - 20, Screen.height - screenPos.y - 10, 100, 20),
                $"{role} {meshStatus}");
        }
    }

    private void OnDrawGizmos()
    {
        if (!Application.isPlaying || !IsActiveNetworked) return;

        Gizmos.color = Object != null && Object.HasStateAuthority ? Color.green : Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.15f);

        if (NetworkVelocity != Vector3.zero)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position, NetworkVelocity.normalized * 1f);
        }
    }
}