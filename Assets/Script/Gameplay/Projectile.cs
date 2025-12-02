using Fusion;
using Scripts.Gameplay;
using UnityEngine;

/// <summary>
/// Networked projectile with pooling support.
/// Handles shooting, movement, collision detection, and visual synchronization.
/// </summary>
public class Projectile : NetworkBehaviour
{
    #region Networked Properties
    [Networked] public bool IsActiveNetworked { get; set; }
    [Networked] public PlayerRef Owner { get; set; }
    [Networked] public Vector3 NetworkPosition { get; set; }
    [Networked] public Vector3 NetworkVelocity { get; set; }
    [Networked] public TickTimer LifeTimer { get; set; }
    #endregion

    #region Configuration
    [Header("Projectile Settings")]
    public float speed = 40f;
    public int damage = 20;
    public float lifeSeconds = 4f;
    #endregion

    public bool IsActive => IsActiveNetworked;

    private TrailRenderer trail;
    private MeshRenderer meshRenderer;
    private bool isInitialized = false;
    private bool lastActiveState = false;
    private bool isVisibleNow = false;
    private int invisibleFrames = 0;

    #region Initialization
    private void Awake()
    {
        InitializeComponents();
        ForceInvisible();
    }

    /// <summary>
    /// Find and cache renderer components
    /// </summary>
    private void InitializeComponents()
    {
        meshRenderer = GetComponentInChildren<MeshRenderer>();
        trail = GetComponentInChildren<TrailRenderer>();

        if (meshRenderer == null)
        {
            var allRenderers = GetComponentsInChildren<Renderer>(true);
            foreach (var r in allRenderers)
            {
                if (r is MeshRenderer)
                {
                    meshRenderer = (MeshRenderer)r;
                    break;
                }
            }
        }
    }

    public override void Spawned()
    {
        base.Spawned();
        isInitialized = true;

        if (Object.HasStateAuthority)
        {
            ForceDeactivateNetworked();
        }

        lastActiveState = IsActiveNetworked;
        ForceInvisible();
    }
    #endregion

    #region Visibility Control
    /// <summary>
    /// Make projectile invisible (used during pooling)
    /// </summary>
    private void ForceInvisible()
    {
        isVisibleNow = false;

        if (meshRenderer != null)
            meshRenderer.enabled = false;

        var allRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in allRenderers)
        {
            if (r != null)
                r.enabled = false;
        }

        if (trail != null)
        {
            trail.enabled = false;
        }
    }

    /// <summary>
    /// Make projectile visible and clear trail
    /// </summary>
    private void MakeVisible()
    {
        if (isVisibleNow) return;

        isVisibleNow = true;

        // Enable renderers
        var allRenderers = GetComponentsInChildren<Renderer>(true);
        foreach (var r in allRenderers)
        {
            if (r != null && !(r is TrailRenderer))
                r.enabled = true;
        }

        if (meshRenderer != null)
            meshRenderer.enabled = true;

        // Enable trail and clear old positions
        if (trail != null)
        {
            trail.enabled = true;
            trail.Clear();
        }

        if (Object.HasStateAuthority)
        {
            RPC_MakeVisible();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_MakeVisible()
    {
        if (Object.HasStateAuthority) return;

        transform.position = NetworkPosition;
        invisibleFrames = 0;
        MakeVisible();
    }
    #endregion

    #region Firing
    /// <summary>
    /// Fire projectile from owner with given position and direction
    /// </summary>
    public void Fire(PlayerRef owner, Vector3 position, Vector3 direction)
    {
        if (!Object.HasStateAuthority) return;

        // Force invisible first
        ForceInvisible();

        // Set position and clear trail
        transform.position = position;
        if (trail != null)
        {
            trail.Clear();
        }

        // Setup network data
        NetworkPosition = position;
        Owner = owner;
        NetworkVelocity = direction.normalized * speed;
        LifeTimer = TickTimer.CreateFromSeconds(Runner, lifeSeconds);
        IsActiveNetworked = true;

        // Wait 1 frame before showing
        invisibleFrames = 1;
    }
    #endregion

    #region Network Updates
    /// <summary>
    /// Smooth visual interpolation (runs every render frame)
    /// </summary>
    public override void Render()
    {
        if (!isInitialized || !IsActiveNetworked) return;
        if (invisibleFrames > 0) return;

        // Smooth interpolation to network position
        if (Vector3.Distance(transform.position, NetworkPosition) > 10f)
        {
            // Snap if too far (teleport case)
            transform.position = NetworkPosition;
        }
        else
        {
            // Smooth lerp
            transform.position = Vector3.Lerp(transform.position, NetworkPosition, 25f * Runner.DeltaTime);
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!isInitialized) return;

        // Handle invisibility countdown
        if (invisibleFrames > 0)
        {
            invisibleFrames--;
            if (invisibleFrames == 0 && IsActiveNetworked)
            {
                MakeVisible();
            }
            else
            {
                transform.position = NetworkPosition;
                return;
            }
        }

        // State change detection
        if (lastActiveState != IsActiveNetworked)
        {
            if (IsActiveNetworked)
            {
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

        // Server simulation
        if (Object.HasStateAuthority)
        {
            ServerSimulation();
        }
    }
    #endregion

    #region Server Simulation
    /// <summary>
    /// Server-side projectile physics and collision detection
    /// </summary>
    private void ServerSimulation()
    {
        if (!IsActiveNetworked || invisibleFrames > 0) return;

        // Check lifetime
        if (LifeTimer.Expired(Runner))
        {
            Deactivate();
            return;
        }

        // Calculate movement
        Vector3 movement = NetworkVelocity * Runner.DeltaTime;
        Vector3 nextPosition = NetworkPosition + movement;

        // Bounds check
        if (nextPosition.magnitude > 2000f)
        {
            Deactivate();
            return;
        }

        // Raycast for collision
        Vector3 direction = NetworkVelocity.normalized;
        float distance = movement.magnitude;

        if (Physics.Raycast(NetworkPosition, direction, out RaycastHit hit, distance + 0.5f))
        {
            // Check if hit a player
            var hitObj = hit.collider.GetComponentInParent<NetworkObject>();
            if (hitObj != null)
            {
                var pc = hitObj.GetComponent<PlayerController>();
                if (pc != null && pc.Object != null)
                {
                    // Don't hit yourself, only hit living enemies
                    if (pc.Object.InputAuthority != Owner && !pc.IsDead)
                    {
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
    #endregion

    #region Deactivation
    /// <summary>
    /// Deactivate projectile and return to pool
    /// </summary>
    public void ForceDeactivateNetworked()
    {
        if (!Object.HasStateAuthority) return;

        IsActiveNetworked = false;
        Owner = PlayerRef.None;
        NetworkVelocity = Vector3.zero;
        LifeTimer = TickTimer.None;
        NetworkPosition = Vector3.one * 9999f;
        transform.position = NetworkPosition;
        ForceInvisible();
    }

    private void Deactivate()
    {
        if (!Object.HasStateAuthority) return;

        RPC_ForceDeactivate();
        ForceDeactivateNetworked();
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ForceDeactivate()
    {
        ForceInvisible();
    }
    #endregion
}