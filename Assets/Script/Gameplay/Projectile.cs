using Fusion;
using UnityEngine;

namespace Scripts.Gameplay
{
    public class Projectile : NetworkBehaviour
    {
        [Networked] public bool IsActive { get; set; }
        [Networked] public PlayerRef Owner { get; set; }
        [Networked] public TickTimer LifeTimer { get; set; }

        public float speed = 30f;
        public int damage = 20;
        public float lifetime = 3f;

        private Rigidbody rb;
        private MeshRenderer meshRenderer;
        private TrailRenderer trail;
        private SphereCollider col;

        public override void Spawned()
        {
            // Setup Rigidbody
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                rb = gameObject.AddComponent<Rigidbody>();
            }
            rb.useGravity = false;
            rb.isKinematic = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

            // Setup Collider
            col = GetComponent<SphereCollider>();
            if (col == null)
            {
                col = gameObject.AddComponent<SphereCollider>();
            }
            col.radius = 0.1f;
            col.isTrigger = true;

            // Setup Visual
            meshRenderer = GetComponent<MeshRenderer>();
            if (meshRenderer == null)
            {
                var filter = gameObject.AddComponent<MeshFilter>();
                filter.mesh = CreateSphereMesh();
                meshRenderer = gameObject.AddComponent<MeshRenderer>();
                meshRenderer.material = new Material(Shader.Find("Standard"));
                meshRenderer.material.color = Color.yellow;
            }

            // Setup Trail
            trail = GetComponent<TrailRenderer>();
            if (trail == null)
            {
                trail = gameObject.AddComponent<TrailRenderer>();
                trail.time = 0.2f;
                trail.startWidth = 0.15f;
                trail.endWidth = 0.05f;
                trail.material = new Material(Shader.Find("Sprites/Default"));
                trail.startColor = Color.yellow;
                trail.endColor = Color.red;
            }

            // Start disabled
            IsActive = false;
            gameObject.SetActive(false);

            Debug.Log("[Projectile] Spawned and disabled");
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;
            if (!IsActive) return;

            // Move forward using velocity
            rb.linearVelocity = transform.forward * speed;

            // Check lifetime
            if (LifeTimer.Expired(Runner))
            {
                Deactivate();
            }
        }

        public void Fire(PlayerRef owner, Vector3 startPos, Vector3 direction)
        {
            if (!Runner.IsServer)
            {
                Debug.LogError("[Projectile] Fire called on non-server!");
                return;
            }

            // Set networked properties
            Owner = owner;
            IsActive = true;
            LifeTimer = TickTimer.CreateFromSeconds(Runner, lifetime);

            // Set position and rotation
            transform.position = startPos;
            transform.forward = direction.normalized;

            // Enable GameObject
            gameObject.SetActive(true);

            // Reset physics
            rb.linearVelocity = direction.normalized * speed;
            rb.angularVelocity = Vector3.zero;

            // Enable visuals
            if (meshRenderer != null) meshRenderer.enabled = true;
            if (col != null) col.enabled = true;
            if (trail != null)
            {
                trail.enabled = true;
                trail.Clear();
            }

            Debug.Log($"[Projectile] ✓ Fired from {startPos} direction {direction}");
        }

        private void Deactivate()
        {
            if (!Runner.IsServer) return;

            IsActive = false;
            Owner = PlayerRef.None;

            // Disable GameObject
            gameObject.SetActive(false);

            // Stop physics
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            // Disable visuals
            if (meshRenderer != null) meshRenderer.enabled = false;
            if (col != null) col.enabled = false;
            if (trail != null)
            {
                trail.enabled = false;
                trail.Clear();
            }

            Debug.Log("[Projectile] Deactivated");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!Runner.IsServer) return;
            if (!IsActive) return;

            Debug.Log($"[Projectile] Hit: {other.gameObject.name}");

            // Check if hit a player
            var pc = other.GetComponent<PlayerController>();
            if (pc != null)
            {
                if (pc.Object.InputAuthority != Owner)
                {
                    Debug.Log($"[Projectile] ✓ Damaged player: {pc.PlayerName}");
                    pc.RPC_TakeDamage(damage, Owner);
                    Deactivate();
                }
                return;
            }

            // Check environment
            if (other.CompareTag("Environment") || other.gameObject.layer == 0)
            {
                Debug.Log("[Projectile] Hit environment");
                Deactivate();
            }
        }

        private Mesh CreateSphereMesh()
        {
            GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            temp.transform.localScale = Vector3.one * 0.2f;
            Mesh mesh = temp.GetComponent<MeshFilter>().mesh;
            Destroy(temp);
            return mesh;
        }
    }
}