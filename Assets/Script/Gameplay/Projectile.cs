using Fusion;
using UnityEngine;

namespace Scripts.Gameplay
{
    public class Projectile : NetworkBehaviour
    {
        [Networked] public bool IsActive { get; private set; } = false;
        [Networked] public PlayerRef Owner { get; private set; }
        [Networked] public TickTimer LifeTimer { get; private set; }

        public float speed = 20f;
        public int damage = 20;
        public float lifetime = 3f;

        public override void Spawned()
        {
            IsActive = false;
            Debug.Log("[Projectile] Spawned");
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;
            if (!IsActive) return;

            // Move forward
            transform.position += transform.forward * speed * Runner.DeltaTime;

            if (LifeTimer.Expired(Runner))
            {
                Deactivate();
            }
        }

        public void Activate(PlayerRef owner, Vector3 direction)
        {
            if (!Runner.IsServer) return;

            Owner = owner;
            IsActive = true;
            LifeTimer = TickTimer.CreateFromSeconds(Runner, lifetime);

            if (direction.sqrMagnitude > 0.001f)
                transform.forward = direction;

            Debug.Log("[Projectile] Activated");
        }

        private void Deactivate()
        {
            if (!Runner.IsServer) return;
            IsActive = false;
            Owner = PlayerRef.Invalid;
            transform.position = Vector3.zero;
            Debug.Log("[Projectile] Deactivated");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!Runner.IsServer) return;
            if (!IsActive) return;

            if (other.TryGetComponent<PlayerController>(out var pc))
            {
                if (pc.Object.InputAuthority != Owner)
                {
                    pc.RPC_TakeDamage(damage, Owner);
                    Deactivate();
                    Debug.Log("[Projectile] Hit player!");
                }
            }
            else if (other.CompareTag("Environment"))
            {
                Deactivate();
                Debug.Log("[Projectile] Hit environment!");
            }
        }
    }
}

