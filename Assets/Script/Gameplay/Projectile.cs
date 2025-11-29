using Fusion;
using Scripts.Gameplay;
using UnityEngine;

public class Projectile : NetworkBehaviour
{
    [Networked] public bool IsActiveNetworked { get; set; }
    [Networked] public PlayerRef Owner { get; set; }
    [Networked] public Vector3 NetworkPosition { get; set; }
    [Networked] public Vector3 Velocity { get; set; }

    public float speed = 40f;
    public int damage = 20;
    public float lifeSeconds = 4f;
    private TickTimer lifeTimer;
    public bool IsActive => IsActiveNetworked;

    private TrailRenderer trail;
    private Renderer projectileRenderer;
    private bool isInitialized = false;

    private void Awake()
    {
        trail = GetComponent<TrailRenderer>();
        projectileRenderer = GetComponent<Renderer>();
        SetVisibility(false);
    }

    public override void Spawned()
    {
        base.Spawned();
        isInitialized = true;

        if (Object.HasStateAuthority)
        {
            ForceDeactivateNetworked();
        }
    }

    public void ForceDeactivateNetworked()
    {
        if (!Object.HasStateAuthority) return;

        IsActiveNetworked = false;
        Owner = PlayerRef.None;
        Velocity = Vector3.zero;
        lifeTimer = TickTimer.None;
        NetworkPosition = Vector3.one * 9999f;
        transform.position = NetworkPosition;

        SetVisibility(false);
        RPC_HideProjectile();
    }

    public void Fire(PlayerRef owner, Vector3 position, Vector3 direction)
    {
        if (!Object.HasStateAuthority) return;

        Debug.Log($"[Projectile] 🔫 SERVER firing from {position} dir {direction} for {owner}");

        Owner = owner;
        IsActiveNetworked = true;
        lifeTimer = TickTimer.CreateFromSeconds(Runner, lifeSeconds);

        transform.position = position;
        NetworkPosition = position;
        Velocity = direction.normalized * speed;

        SetVisibility(true);

        // Show on all clients immediately
        RPC_ShowProjectile(position, direction.normalized * speed);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowProjectile(Vector3 startPos, Vector3 velocity)
    {
        Debug.Log($"[Projectile] 🎯 CLIENT showing projectile at {startPos}");

        transform.position = startPos;
        SetVisibility(true);

        if (trail != null)
        {
            trail.Clear();
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_HideProjectile()
    {
        SetVisibility(false);
    }

    private void SetVisibility(bool visible)
    {
        if (projectileRenderer != null)
            projectileRenderer.enabled = visible;

        if (trail != null)
        {
            if (visible)
            {
                trail.Clear();
                trail.enabled = true;
            }
            else
            {
                trail.enabled = false;
            }
        }
    }

    public override void FixedUpdateNetwork()
    {
        if (!isInitialized) return;

        if (Object.HasStateAuthority)
        {
            SimulateProjectile();
        }
        else
        {
            RenderProjectile();
        }
    }

    private void SimulateProjectile()
    {
        if (!IsActiveNetworked)
        {
            SetVisibility(false);
            return;
        }

        // Calculate movement
        Vector3 movement = Velocity * Runner.DeltaTime;
        Vector3 nextPosition = transform.position + movement;

        Vector3 direction = movement.normalized;
        float distance = movement.magnitude;

        // Lifetime check
        if (lifeTimer.Expired(Runner))
        {
            Debug.Log("[Projectile] ⏱️ Expired");
            Deactivate();
            return;
        }

        // Bounds check
        if (nextPosition.magnitude > 2000f)
        {
            Debug.Log("[Projectile] 🚫 Out of bounds");
            Deactivate();
            return;
        }

        // ✅ HIT DETECTION - This runs ONLY on server
        if (Physics.Raycast(transform.position, direction, out RaycastHit hit, distance + 0.5f))
        {
            Debug.Log($"[Projectile] 💥💥💥 SERVER HIT: {hit.collider.name} at {hit.point}");

            var hitObj = hit.collider.GetComponentInParent<NetworkObject>();
            if (hitObj != null)
            {
                var pc = hitObj.GetComponent<PlayerController>();
                if (pc != null && pc.Object.InputAuthority != Owner && !pc.IsDead)
                {
                    Debug.Log($"[Projectile] 🎯🎯🎯 SERVER: Hit player {pc.PlayerName}! Dealing {damage} damage!");

                    // ✅ Call server-side damage method directly
                    pc.ApplyDamageServerSide(damage, Owner);
                }
                else
                {
                    if (pc != null && pc.Object.InputAuthority == Owner)
                        Debug.Log("[Projectile] Hit own player, ignoring");
                    else if (pc != null && pc.IsDead)
                        Debug.Log("[Projectile] Hit dead player, ignoring");
                }
            }
            else
            {
                Debug.Log("[Projectile] Hit non-networked object");
            }

            Deactivate();
            return;
        }

        // Update position
        transform.position = nextPosition;
        NetworkPosition = nextPosition;
    }

    private void RenderProjectile()
    {
        if (!IsActiveNetworked)
        {
            SetVisibility(false);
            return;
        }

        if (projectileRenderer != null && !projectileRenderer.enabled)
            SetVisibility(true);

        // Smooth interpolation
        transform.position = Vector3.Lerp(transform.position, NetworkPosition, 30f * Runner.DeltaTime);

        // Prediction
        if (Velocity != Vector3.zero)
        {
            transform.position += Velocity * Runner.DeltaTime * 0.5f;
        }
    }

    private void Deactivate()
    {
        if (!Object.HasStateAuthority) return;
        Debug.Log("[Projectile] 🔴 Deactivating");
        ForceDeactivateNetworked();
    }

    private void OnDrawGizmos()
    {
        if (IsActiveNetworked)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.12f);
            if (Velocity != Vector3.zero)
            {
                Gizmos.DrawRay(transform.position, Velocity.normalized * 0.5f);
            }
        }
    }
}