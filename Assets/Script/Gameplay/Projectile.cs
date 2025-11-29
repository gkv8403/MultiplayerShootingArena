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

    // Visual component (optional)
    private TrailRenderer trail;

    private void Awake()
    {
        trail = GetComponent<TrailRenderer>();
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

        // Clear trail
        if (trail != null)
            trail.Clear();
    }

    // FIX 1: Fire now properly sets position and direction
    public void Fire(PlayerRef owner, Vector3 position, Vector3 direction)
    {
        if (!Object.HasStateAuthority) return;

        Debug.Log($"[Projectile] Firing from {position} direction {direction}");

        Owner = owner;
        IsActiveNetworked = true;
        lifeTimer = TickTimer.CreateFromSeconds(Runner, lifeSeconds);

        // FIX 2: Set both local and networked position
        transform.position = position;
        NetworkPosition = position;

        Velocity = direction.normalized * speed;

        // FIX 3: Notify clients to show projectile
        RPC_OnProjectileFired(position, direction);
    }

    // FIX 4: RPC to sync visual on all clients
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_OnProjectileFired(Vector3 position, Vector3 direction)
    {
        transform.position = position;
        if (trail != null)
        {
            trail.Clear();
            trail.enabled = true;
        }
        Debug.Log($"[Projectile] Visual synced at {position}");
    }

    public override void FixedUpdateNetwork()
    {
        // FIX 5: Server simulates, clients interpolate
        if (Object.HasStateAuthority)
        {
            SimulateProjectile();
        }
        else
        {
            InterpolateProjectile();
        }
    }

    private void SimulateProjectile()
    {
        if (!IsActiveNetworked) return;

        // Move projectile
        Vector3 movement = Velocity * Runner.DeltaTime;
        transform.position += movement;
        NetworkPosition = transform.position;

        // Check lifetime
        if (lifeTimer.Expired(Runner))
        {
            Deactivate();
            return;
        }

        // World bounds check
        if (transform.position.magnitude > 2000f)
        {
            Deactivate();
            return;
        }

        // Hit detection - use raycast for accuracy
        Ray ray = new Ray(transform.position - movement, movement.normalized);
        if (Physics.Raycast(ray, out RaycastHit hit, movement.magnitude + 0.5f))
        {
            // Check if we hit a player
            var hitObj = hit.collider.GetComponentInParent<NetworkObject>();
            if (hitObj != null)
            {
                var pc = hitObj.GetComponent<PlayerController>();
                if (pc != null && pc.Object.InputAuthority != Owner)
                {
                    // Apply damage
                    pc.RPC_TakeDamage(damage, Owner);
                    Debug.Log($"[Projectile] Hit {pc.PlayerName}!");
                }
            }

            Deactivate();
        }
    }

    // FIX 6: Smooth interpolation for clients
    private void InterpolateProjectile()
    {
        if (!IsActiveNetworked)
        {
            // Hide inactive projectiles
            if (transform.position.magnitude < 9000f)
            {
                transform.position = Vector3.one * 9999f;
            }
            return;
        }

        // Interpolate to network position
        transform.position = Vector3.Lerp(transform.position, NetworkPosition, 30f * Runner.DeltaTime);
    }

    private void Deactivate()
    {
        if (!Object.HasStateAuthority) return;
        Debug.Log("[Projectile] Deactivating");
        ForceDeactivateNetworked();
    }

    // FIX 7: Visual debugging
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