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
    private bool lastActiveState = false;

    // ✅ NEW: More explicit visibility control
    private bool isVisibleNow = false;
    private int invisibleFrames = 0; // Track frames to stay invisible

    private void Awake()
    {
        meshRenderer = GetComponentInChildren<MeshRenderer>();
        trail = GetComponentInChildren<TrailRenderer>();

        if (meshRenderer == null)
        {
            Debug.LogError("[Projectile] ⚠️ NO MESHRENDERER FOUND! Check your prefab!");
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

        // ✅ CRITICAL: Force invisible immediately
        ForceInvisible();
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

        // ✅ CRITICAL: Force invisible on spawn
        ForceInvisible();
    }

    // ✅ NEW: More aggressive invisibility
    private void ForceInvisible()
    {
        isVisibleNow = false;
        invisibleFrames = 2; // Stay invisible for at least 2 frames

        // Disable ALL renderers
        var allRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in allRenderers)
        {
            if (r != null)
            {
                r.enabled = false;
            }
        }

        if (meshRenderer != null)
        {
            meshRenderer.enabled = false;
        }

        if (trail != null)
        {
            trail.enabled = false;
            trail.Clear();
        }
    }

    public void ForceDeactivateNetworked()
    {
        if (!Object.HasStateAuthority) return;

        IsActiveNetworked = false;
        Owner = PlayerRef.None;
        NetworkVelocity = Vector3.zero;
        LifeTimer = TickTimer.None;
        NetworkPosition = Vector3.one * 9999f;

        // ✅ Move to hidden position FIRST
        transform.position = NetworkPosition;

        // ✅ THEN force invisible
        ForceInvisible();
    }

    public void Fire(PlayerRef owner, Vector3 position, Vector3 direction)
    {
        if (!Object.HasStateAuthority) return;

        Debug.Log($"[Projectile] 🔫 SERVER: Firing from {position} for Player{owner.PlayerId}");

        // ✅ CRITICAL: Force invisible FIRST
        ForceInvisible();

        // ✅ CRITICAL: Set position IMMEDIATELY (before any network update)
        transform.position = position;
        NetworkPosition = position;

        Owner = owner;
        NetworkVelocity = direction.normalized * speed;
        LifeTimer = TickTimer.CreateFromSeconds(Runner, lifeSeconds);
        IsActiveNetworked = true;

        // ✅ Wait 1 frame before showing (let position settle)
        invisibleFrames = 1;

        Debug.Log($"[Projectile] ✅ SERVER: Projectile setup at {position}, will show next frame");
    }

    public override void FixedUpdateNetwork()
    {
        if (!isInitialized) return;

        // ✅ CRITICAL: Handle invisibility countdown
        if (invisibleFrames > 0)
        {
            invisibleFrames--;
            if (invisibleFrames == 0 && IsActiveNetworked)
            {
                // Now we can show it
                MakeVisible();
            }
            else
            {
                // Keep it invisible
                ForceInvisible();
                return; // Don't do any other updates
            }
        }

        // Check for state changes
        if (lastActiveState != IsActiveNetworked)
        {
            string role = Object.HasStateAuthority ? "SERVER" : "CLIENT";
            Debug.Log($"[Projectile] 🔄 {role}: State changed {lastActiveState} → {IsActiveNetworked}");

            if (IsActiveNetworked)
            {
                // Wait 1 frame before showing on clients too
                if (!Object.HasStateAuthority)
                {
                    transform.position = NetworkPosition;
                    invisibleFrames = 1;
                }
            }
            else
            {
                ForceInvisible();
            }

            lastActiveState = IsActiveNetworked;
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

    // ✅ NEW: Explicit method to make visible
    private void MakeVisible()
    {
        if (isVisibleNow) return; // Already visible

        string role = Object != null && Object.HasStateAuthority ? "SERVER" : "CLIENT";
        Debug.Log($"[Projectile] 👁️ {role}: Making visible at {transform.position}");

        isVisibleNow = true;

        // Enable all non-trail renderers
        var allRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in allRenderers)
        {
            if (r != null && !(r is TrailRenderer))
            {
                r.enabled = true;
            }
        }

        if (meshRenderer != null)
        {
            meshRenderer.enabled = true;
        }

        // Enable trail
        if (trail != null)
        {
            trail.Clear();
            trail.enabled = true;
        }

        // ✅ Broadcast to clients if server
        if (Object.HasStateAuthority)
        {
            RPC_MakeVisible();
        }
    }

    // ✅ NEW: RPC to make visible on clients
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_MakeVisible()
    {
        if (Object.HasStateAuthority) return; // Already handled on server

        Debug.Log($"[Projectile] 🔄 CLIENT: RPC_MakeVisible at {NetworkPosition}");
        transform.position = NetworkPosition;
        invisibleFrames = 0;
        MakeVisible();
    }

    private void ServerSimulation()
    {
        if (!IsActiveNetworked) return;

        // ✅ Don't update if still invisible
        if (invisibleFrames > 0) return;

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
        if (!IsActiveNetworked) return;

        // ✅ Don't interpolate if invisible
        if (invisibleFrames > 0) return;

        // Smooth interpolation
        transform.position = Vector3.Lerp(transform.position, NetworkPosition, 25f * Runner.DeltaTime);
    }

    private void Deactivate()
    {
        if (!Object.HasStateAuthority) return;
        Debug.Log($"[Projectile] 🔴 SERVER: Deactivating");

        // ✅ Immediate deactivation
        RPC_ForceDeactivate();
        ForceDeactivateNetworked();
    }

    // ✅ RPC to force deactivation on all clients
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ForceDeactivate()
    {
        string role = Object.HasStateAuthority ? "SERVER" : "CLIENT";
        Debug.Log($"[Projectile] 🔴 {role}: RPC_ForceDeactivate");

        ForceInvisible();
    }

    // ✅ Force visibility check every frame (backup)
    private void LateUpdate()
    {
        // If we're supposed to be invisible, enforce it every frame
        if (!IsActiveNetworked || invisibleFrames > 0)
        {
            if (isVisibleNow)
            {
                ForceInvisible();
            }
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

        // Show invisibility state
        if (invisibleFrames > 0)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawWireCube(transform.position, Vector3.one * 0.3f);
        }
    }
}