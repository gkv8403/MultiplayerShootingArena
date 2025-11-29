using Fusion;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Scripts.Gameplay
{
    public class PlayerController : NetworkBehaviour
    {
        [Networked] public int Health { get; set; }
        [Networked] public int Kills { get; set; }
        [Networked] public int Deaths { get; set; }
        [Networked] public NetworkString<_16> PlayerName { get; set; }
        [Networked] public float VerticalRotation { get; set; }

        [Header("Movement")]
        public CharacterController characterController;
        public float moveSpeed = 5f;
        public float rotationSpeed = 150f;
        public float verticalLookSpeed = 100f;

        [Header("Combat")]
        public Transform firePoint;
        public float fireRate = 0.15f;
        private TickTimer fireTimer;

        [Header("UI")]
        public TextMeshProUGUI nameText;
        public Image healthBar;
        public Canvas playerCanvas;

        public int maxHealth = 100;
        [Networked] public TickTimer RespawnTimer { get; set; }

        private bool combatEnabled = false;
        public Transform headTransform;
        public Transform gunPoint;
        public override void Spawned()
        {
            Debug.Log($"[Player] Spawned at {transform.position}");

            if (Object.HasStateAuthority)
            {
                Health = maxHealth;
                Kills = 0;
                Deaths = 0;
                VerticalRotation = 0f;
                PlayerName = PlayerPrefs.GetString("PlayerName", $"Player{Object.InputAuthority.PlayerId}");
            }

            // Setup CharacterController
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
                if (characterController == null)
                {
                    characterController = gameObject.AddComponent<CharacterController>();
                    characterController.center = Vector3.up * 0.9f;
                    characterController.height = 1.8f;
                    characterController.radius = 0.3f;
                }
            }
            characterController.enabled = true;

            // Create Head
            headTransform = transform.Find("Head");
            if (headTransform == null)
            {
                GameObject headObj = new GameObject("Head");
                headTransform = headObj.transform;
                headTransform.SetParent(transform);
                headTransform.localPosition = new Vector3(0, 1.6f, 0);
                headTransform.localRotation = Quaternion.identity;
            }

            // Create FirePoint
            firePoint = transform.Find("FirePoint");
            if (firePoint == null)
            {
                GameObject fpObj = new GameObject("FirePoint");
                firePoint = fpObj.transform;
                firePoint.SetParent(gunPoint);
                firePoint.localPosition = new Vector3(0, 0, 0.5f);
                firePoint.localRotation = Quaternion.identity;
            }

            // Update UI
            if (nameText != null)
                nameText.text = PlayerName.ToString();

            if (healthBar != null)
                healthBar.fillAmount = 1f;

            Debug.Log($"[Player] Setup complete - FirePoint: {firePoint.position}");
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasInputAuthority) return;

            if (!GetInput(out NetworkInputData data)) return;

            if (Health <= 0)
            {
                if (Object.HasStateAuthority && RespawnTimer.Expired(Runner))
                {
                    Respawn();
                }
                return;
            }

            // Movement
            if (characterController != null && characterController.enabled)
            {
                Vector3 move = new Vector3(data.moveInput.x, 0, data.moveInput.y);
                move = transform.TransformDirection(move);
                move.y = 0;
                characterController.Move(move * moveSpeed * Runner.DeltaTime);
            }

            // Horizontal rotation (whole player)
            if (data.lookInput.x != 0)
            {
                float yaw = data.lookInput.x * rotationSpeed * Runner.DeltaTime;
                transform.Rotate(0, yaw, 0, Space.World);
            }

            // Vertical rotation (head only)
            if (data.lookInput.y != 0 && headTransform != null)
            {
                VerticalRotation -= data.lookInput.y * verticalLookSpeed * Runner.DeltaTime;
                VerticalRotation = Mathf.Clamp(VerticalRotation, -80f, 80f);
                headTransform.localRotation = Quaternion.Euler(VerticalRotation, 0, 0);
            }

            // Fire
            if (data.fire && combatEnabled)
            {
                if (fireTimer.ExpiredOrNotRunning(Runner))
                {
                    RPC_Fire(firePoint.position, firePoint.forward);
                    fireTimer = TickTimer.CreateFromSeconds(Runner, fireRate);
                }
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_Fire(Vector3 position, Vector3 direction)
        {
            if (!Object.HasStateAuthority) return;

            var pool = NetworkProjectilePool.Instance;
            if (pool == null)
            {
                Debug.LogError("[Player] Pool not found!");
                return;
            }

            var projectile = pool.GetPooledProjectile();
            if (projectile == null)
            {
                Debug.LogError("[Player] No projectile available!");
                return;
            }

            var proj = projectile.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.Fire(Object.InputAuthority, position, direction);
                Debug.Log($"[Player] ✓ Fired bullet from {position}");
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_TakeDamage(int dmg, PlayerRef attacker)
        {
            if (!Object.HasStateAuthority) return;
            if (Health <= 0) return;

            Health -= dmg;
            Debug.Log($"[Player] {PlayerName} took {dmg} damage. Health: {Health}");

            if (healthBar != null)
                healthBar.fillAmount = (float)Health / maxHealth;

            if (Health <= 0)
            {
                Health = 0;
                Deaths++;
                gameObject.SetActive(false);

                // Give kill to attacker
                if (Runner.TryGetPlayerObject(attacker, out NetworkObject attackerObj))
                {
                    var attackerPC = attackerObj.GetComponent<PlayerController>();
                    if (attackerPC != null)
                    {
                        attackerPC.Kills++;
                        Events.RaiseUpdateScore(attackerPC.PlayerName.ToString(), attackerPC.Kills);
                        Debug.Log($"[Player] {attackerPC.PlayerName} got kill #{attackerPC.Kills}");
                    }
                }

                RespawnTimer = TickTimer.CreateFromSeconds(Runner, 3f);
            }
        }

        private void Respawn()
        {
            Health = maxHealth;
            VerticalRotation = 0f;
            gameObject.SetActive(true);

            var sps = FindObjectsOfType<SpawnPoint>();
            if (sps.Length > 0)
            {
                var sp = sps[Random.Range(0, sps.Length)];

                if (characterController != null)
                    characterController.enabled = false;

                transform.position = sp.transform.position;
                transform.rotation = sp.transform.rotation;

                if (headTransform != null)
                    headTransform.localRotation = Quaternion.identity;

                if (characterController != null)
                    characterController.enabled = true;

                Debug.Log($"[Player] Respawned at {sp.name}");
            }

            if (healthBar != null)
                healthBar.fillAmount = 1f;

            RespawnTimer = TickTimer.None;
        }

        public void EnableCombat(bool enable)
        {
            combatEnabled = enable;
            Debug.Log($"[Player] Combat: {enable}");
        }
    }

    public struct NetworkInputData : INetworkInput
    {
        public Vector2 moveInput;
        public Vector2 lookInput;
        public bool fire;
    }
}