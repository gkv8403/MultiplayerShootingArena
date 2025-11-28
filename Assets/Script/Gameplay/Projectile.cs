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

        private Rigidbody rb;

        public override void Spawned()
        {
            rb = GetComponent<Rigidbody>();
            // Ensure not active on spawn
            IsActive = false;
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;

            if (!IsActive) return;

            // Move forward by setting transform on server
            transform.position += transform.forward * speed * Runner.DeltaTime;

            if (LifeTimer.Expired(Runner))
            {
                Deactivate();
            }
        }

        // Called by server when allocating pooled projectile
        public void Activate(PlayerRef owner, Vector3 direction)
        {
            if (!Runner.IsServer) return;
            Owner = owner;
            IsActive = true;
            LifeTimer = TickTimer.CreateFromSeconds(Runner, lifetime);

            // align forward to given direction
            if (direction.sqrMagnitude > 0.001f)
                transform.forward = direction;
        }

        private void Deactivate()
        {
            if (!Runner.IsServer) return;
            IsActive = false;
            Owner = PlayerRef.Invalid;
            // move it out of the way
            transform.position = Vector3.zero;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!Runner.IsServer) return;
            if (!IsActive) return;

            if (other.TryGetComponent<PlayerController>(out var pc))
            {
                // don't hit owner
                if (pc.Object.InputAuthority != Owner)
                {
                    pc.RPC_TakeDamage(damage, Owner);
                    Deactivate();
                }
            }
            else if (other.CompareTag("Environment"))
            {
                Deactivate();
            }
        }
    }
}