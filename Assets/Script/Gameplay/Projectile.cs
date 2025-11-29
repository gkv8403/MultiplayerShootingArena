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
    public int damage = 25;
    public float lifeSeconds = 4f;
    private TickTimer lifeTimer;
    public bool IsActive => IsActiveNetworked;

    private TrailRenderer trail;
    private Renderer projectileRenderer;

    private void Awake()
    {
        trail = GetComponent<TrailRenderer>();
        projectileRenderer = GetComponent<Renderer>();

        // Start hidden
        if (projectileRenderer != null)
            projectileRenderer.enabled = false;
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

        if (trail != null)
            trail.Clear();

        // Tell all clients to hide this projectile
        RPC_HideProjectile();
    }

    public void Fire(PlayerRef owner, Vector3 position, Vector3 direction)
    {
        if (!Object.HasStateAuthority) return;

        Debug.Log($"[Projectile] SERVER firing from {position} direction {direction} for player {owner}");

        Owner = owner;
        IsActiveNetworked = true;
        lifeTimer = TickTimer.CreateFromSeconds(Runner, lifeSeconds);

        transform.position = position;
        NetworkPosition = position;
        Velocity = direction.normalized * speed;

        // Show on server
        if (projectileRenderer != null)
            projectileRenderer.enabled = true;

        if (trail != null)
        {
            trail.Clear();
            trail.enabled = true;
        }

        // FIX: Tell ALL clients to show and move this projectile
        RPC_ShowProjectile(position, direction.normalized * speed);
    }

    // FIX: This RPC makes projectile visible on all clients
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ShowProjectile(Vector3 startPos, Vector3 velocity)
    {
        Debug.Log($"[Projectile] CLIENT showing projectile at {startPos}");

        transform.position = startPos;

        if (projectileRenderer != null)
            projectileRenderer.enabled = true;

        if (trail != null)
        {
            trail.Clear();
            trail.enabled = true;
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_HideProjectile()
    {
        if (projectileRenderer != null)
            projectileRenderer.enabled = false;

        if (trail != null)
        {
            trail.Clear();
            trail.enabled = false;
        }
    }

    public override void FixedUpdateNetwork()
    {
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
            if (projectileRenderer != null)
                projectileRenderer.enabled = false;
            return;
        }

        // Move on server
        Vector3 movement = Velocity * Runner.DeltaTime;
        transform.position += movement;
        NetworkPosition = transform.position;

        // Lifetime check
        if (lifeTimer.Expired(Runner))
        {
            Debug.Log("[Projectile] Lifetime expired");
            Deactivate();
            return;
        }

        // Bounds check
        if (transform.position.magnitude > 2000f)
        {
            Debug.Log("[Projectile] Out of bounds");
            Deactivate();
            return;
        }

        // Hit detection
        Ray ray = new Ray(transform.position - movement, movement.normalized);
        if (Physics.Raycast(ray, out RaycastHit hit, movement.magnitude + 0.5f))
        {
            Debug.Log($"[Projectile] Hit something: {hit.collider.name}");

            var hitObj = hit.collider.GetComponentInParent<NetworkObject>();
            if (hitObj != null)
            {
                var pc = hitObj.GetComponent<PlayerController>();
                if (pc != null && pc.Object.InputAuthority != Owner)
                {
                    Debug.Log($"[Projectile] Hit player {pc.PlayerName}! Applying damage...");
                    pc.RPC_TakeDamage(damage, Owner);
                }
            }

            Deactivate();
        }
    }

    private void RenderProjectile()
    {
        if (!IsActiveNetworked)
        {
            if (projectileRenderer != null && projectileRenderer.enabled)
                projectileRenderer.enabled = false;
            return;
        }

        // Show projectile
        if (projectileRenderer != null && !projectileRenderer.enabled)
            projectileRenderer.enabled = true;

        // Smooth movement to network position
        transform.position = Vector3.Lerp(transform.position, NetworkPosition, 30f * Runner.DeltaTime);
    }

    private void Deactivate()
    {
        if (!Object.HasStateAuthority) return;
        Debug.Log("[Projectile] Deactivating");
        ForceDeactivateNetworked();
    }

    private void OnDrawGizmos()
    {
        if (IsActiveNetworked)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.12f);
            Gizmos.DrawRay(transform.position, Velocity.normalized * 0.5f);
        }
    }
}