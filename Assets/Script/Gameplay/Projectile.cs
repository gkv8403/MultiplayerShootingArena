using Fusion;
using Scripts.Gameplay;
using UnityEngine;

/// <summary>
/// Projectile: networked behavior. The networked flag IsActiveNetworked controls lifecycle.
/// Movement is simulated server-side (StateAuthority).
/// </summary>
public class Projectile : NetworkBehaviour
{
    [Networked] public bool IsActiveNetworked { get; set; }
    [Networked] public PlayerRef Owner { get; set; }
    [Networked] public Vector3 Velocity { get; set; }

    public float speed = 40f;
    public int damage = 25;
    public float lifeSeconds = 4f;
    private TickTimer lifeTimer;
    public bool IsActive => IsActiveNetworked;

    // Called by pool on spawn to ensure inactive on start
    public void ForceDeactivateNetworked()
    {
        if (!Object.HasStateAuthority) return;
        IsActiveNetworked = false;
        Owner = PlayerRef.None;
        Velocity = Vector3.zero;
        lifeTimer = TickTimer.None;
        transform.position = Vector3.one * 9999f;
    }

    // Fire called on StateAuthority (server)
    public void Fire(PlayerRef owner, Vector3 position, Vector3 direction)
    {
        if (!Object.HasStateAuthority) return;

        Owner = owner;
        IsActiveNetworked = true;
        lifeTimer = TickTimer.CreateFromSeconds(Runner, lifeSeconds);
        transform.position = position;
        Velocity = direction.normalized * speed;
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority) return;

        if (!IsActiveNetworked) return;

        transform.position += Velocity * Runner.DeltaTime;

        // life timeout
        if (lifeTimer.Expired(Runner))
        {
            Deactivate();
            return;
        }

        // basic world bounds check
        if (transform.position.magnitude > 2000f)
        {
            Deactivate();
            return;
        }

        // simple hit detection - cast a small sphere forward
        var hit = Physics.SphereCast(transform.position - (Velocity.normalized * 0.1f), 0.12f, Velocity.normalized, out RaycastHit rh, Velocity.magnitude * Runner.DeltaTime);
        if (hit)
        {
            var hitObj = rh.collider.GetComponentInParent<NetworkObject>();
            if (hitObj != null)
            {
                var pc = hitObj.GetComponent<PlayerController>();
                if (pc != null)
                {
                    // Apply damage on state authority via RPC so all clients update
                    pc.RPC_TakeDamage(damage, Owner);
                }
            }

            Deactivate();
        }
    }

    private void Deactivate()
    {
        if (!Object.HasStateAuthority) return;
        ForceDeactivateNetworked();
    }
}
