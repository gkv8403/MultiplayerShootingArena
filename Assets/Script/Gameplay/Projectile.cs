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
    private Renderer projectileRenderer;
    private bool isInitialized = false;

    // ✅ Client-side prediction variables
    private Vector3 clientPosition;
    private Vector3 clientVelocity;
    private bool clientActive = false;

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

        Debug.Log($"[Projectile] Spawned - Authority: {Object.HasStateAuthority}");

        if (Object.HasStateAuthority)
        {
            // Server initializes as inactive
            ForceDeactivateNetworked();
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

        transform.position = NetworkPosition;
        SetVisibility(false);
    }

    public void Fire(PlayerRef owner, Vector3 position, Vector3 direction)
    {
        if (!Object.HasStateAuthority) return;

        Debug.Log($"[Projectile] 🔫 SERVER: Firing from {position} dir {direction} for {owner}");

        Owner = owner;
        IsActiveNetworked = true;
        LifeTimer = TickTimer.CreateFromSeconds(Runner, lifeSeconds);

        // ✅ Set position and velocity on server
        NetworkPosition = position;
        NetworkVelocity = direction.normalized * speed;

        transform.position = position;
        SetVisibility(true);

        Debug.Log($"[Projectile] ✅ SERVER: Set velocity {NetworkVelocity}, pos {NetworkPosition}");
    }

    // ✅ This is called EVERY network tick (better than RPC for continuous sync)
    public override void FixedUpdateNetwork()
    {
        if (!isInitialized) return;

        if (Object.HasStateAuthority)
        {
            // SERVER: Simulate physics and hit detection
            ServerSimulation();
        }
        else
        {
            // CLIENT: Render based on networked state
            ClientSimulation();
        }
    }

    private void ServerSimulation()
    {
        if (!IsActiveNetworked)
        {
            SetVisibility(false);
            return;
        }

        SetVisibility(true);

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

        // ✅ Hit detection with raycast
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
                    // Don't hit yourself or dead players
                    if (pc.Object.InputAuthority != Owner && !pc.IsDead)
                    {
                        Debug.Log($"[Projectile] 🎯 SERVER: Hit player {pc.PlayerName}!");
                        pc.ApplyDamageServerSide(damage, Owner);
                    }
                }
            }

            Deactivate();
            return;
        }

        // ✅ Update position every frame for smooth client sync
        NetworkPosition = nextPosition;
        transform.position = nextPosition;
    }

    private void ClientSimulation()
    {
        // ✅ CLIENT: Always sync from networked state

        if (!IsActiveNetworked)
        {
            SetVisibility(false);
            clientActive = false;
            return;
        }

        // Show projectile
        if (!clientActive)
        {
            clientActive = true;
            clientPosition = NetworkPosition;
            clientVelocity = NetworkVelocity;
            transform.position = NetworkPosition;
            SetVisibility(true);

            if (trail != null)
                trail.Clear();

            Debug.Log($"[Projectile] 🎯 CLIENT: Activated at {NetworkPosition} vel {NetworkVelocity}");
        }

        // ✅ Smooth interpolation with prediction
        if (NetworkVelocity.sqrMagnitude > 0.01f)
        {
            // Predict position based on velocity
            Vector3 predictedPos = transform.position + (NetworkVelocity * Runner.DeltaTime);

            // Blend between prediction and network state (80% network, 20% prediction)
            Vector3 targetPos = Vector3.Lerp(NetworkPosition, predictedPos, 0.2f);
            transform.position = Vector3.Lerp(transform.position, targetPos, 30f * Runner.DeltaTime);
        }
        else
        {
            // Fallback: just lerp to network position
            transform.position = Vector3.Lerp(transform.position, NetworkPosition, 20f * Runner.DeltaTime);
        }
    }

    private void SetVisibility(bool visible)
    {
        if (projectileRenderer != null)
            projectileRenderer.enabled = visible;

        if (trail != null)
        {
            if (visible)
            {
                if (!trail.enabled)
                {
                    trail.Clear();
                    trail.enabled = true;
                }
            }
            else
            {
                trail.enabled = false;
            }
        }
    }

    private void Deactivate()
    {
        if (!Object.HasStateAuthority) return;

        Debug.Log("[Projectile] 🔴 SERVER: Deactivating");
        ForceDeactivateNetworked();
    }

    private void OnDrawGizmos()
    {
        if (Application.isPlaying && IsActiveNetworked)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, 0.12f);

            if (NetworkVelocity != Vector3.zero)
            {
                Gizmos.color = Color.red;
                Gizmos.DrawRay(transform.position, NetworkVelocity.normalized * 1f);
            }
        }
    }

    // ✅ Debug info
    private void OnGUI()
    {
        if (!Debug.isDebugBuild || !IsActiveNetworked) return;

        Vector3 screenPos = Camera.main.WorldToScreenPoint(transform.position);
        if (screenPos.z > 0 && screenPos.x > 0 && screenPos.x < Screen.width)
        {
            GUI.color = Object.HasStateAuthority ? Color.green : Color.yellow;
            GUI.Label(new Rect(screenPos.x, Screen.height - screenPos.y, 200, 20),
                $"Proj: {(Object.HasStateAuthority ? "SERVER" : "CLIENT")}");
        }
    }
}