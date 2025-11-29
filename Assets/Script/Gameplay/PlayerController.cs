using Fusion;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;

namespace Scripts.Gameplay
{
    public class PlayerController : NetworkBehaviour
    {
        [Networked] public int Health { get; set; }
        [Networked] public int Kills { get; set; }
        [Networked] public int Deaths { get; set; }
        [Networked] public NetworkString<_16> PlayerName { get; set; }

        [Header("Movement")]
        public CharacterController characterController;
        public float moveSpeed = 5f;
        public float rotationSpeed = 200f;

        [Header("Combat")]
        public NetworkPrefabRef projectilePrefab;
        public Transform firePoint;
        public float fireRate = 0.5f;
        private TickTimer fireTimer;

        [Header("UI")]
        public TextMeshProUGUI nameText;
        public Image healthBar;
        public Canvas playerCanvas;

        public int maxHealth = 100;
        [Networked] public TickTimer RespawnTimer { get; set; }

        private bool combatEnabled = false;

        public override void Spawned()
        {
            Debug.Log($"[Player] Spawned - HasStateAuthority: {Object.HasStateAuthority}, HasInputAuthority: {Object.HasInputAuthority}");

            if (Object.HasStateAuthority)
            {
                Health = maxHealth;
                Kills = 0;
                Deaths = 0;
                PlayerName = PlayerPrefs.GetString("PlayerName", $"P{Object.InputAuthority.PlayerId}");
                Debug.Log($"[Player] Initialized with name: {PlayerName}");
            }

            if (nameText != null)
                nameText.text = PlayerName.ToString();

            // Get or create CharacterController
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
                if (characterController == null)
                {
                    characterController = gameObject.AddComponent<CharacterController>();
                    characterController.center = Vector3.up * 0.6f;
                    characterController.height = 1.8f;
                    characterController.radius = 0.3f;
                    characterController.slopeLimit = 45f;
                    characterController.stepOffset = 0.3f;
                }
            }

            if (characterController != null)
            {
                characterController.enabled = true;
                Debug.Log("[Player] CharacterController ready");
            }

            // ===== CAMERA WILL AUTO-FIND THIS PLAYER =====
            // No need to manually setup - CameraController.FindLocalPlayer() will find it
            if (Object.HasInputAuthority)
            {
                Debug.Log("[Player] This is the local player - camera will auto-find");
            }

            Debug.Log("[Player] Spawned complete");
        }

        public override void FixedUpdateNetwork()
        {
            // Only process input on input authority (local player)
            if (!Object.HasInputAuthority)
                return;

            if (!GetInput(out NetworkInputData data))
            {
                Debug.LogWarning("[Player] No input data received");
                return;
            }

            if (Health <= 0)
                return;

            // ===== MOVEMENT - Only XZ, never modify Y =====
            if (characterController != null && characterController.enabled)
            {
                Vector3 move = new Vector3(data.moveInput.x, 0, data.moveInput.y);
                move = transform.TransformDirection(move);
                move.y = 0; // Force Y to zero - no vertical movement
                characterController.Move(move * moveSpeed * Runner.DeltaTime);
            }

            // ===== ROTATION - Horizontal look only =====
            if (data.lookInput.x != 0)
            {
                float yaw = data.lookInput.x * rotationSpeed * Runner.DeltaTime;
                transform.Rotate(0, yaw, 0);
            }

            // ===== FIRE =====
            if (data.fire && combatEnabled)
            {
                if (fireTimer.ExpiredOrNotRunning(Runner))
                {
                    Debug.Log("[Player] Fire input - calling RPC_Fire");
                    RPC_Fire();
                    fireTimer = TickTimer.CreateFromSeconds(Runner, fireRate);
                }
            }

            // ===== RESPAWN =====
            if (Health <= 0 && Object.HasStateAuthority && RespawnTimer.Expired(Runner))
            {
                Health = maxHealth;
                gameObject.SetActive(true);

                var sps = FindObjectsOfType<SpawnPoint>();
                if (sps.Length > 0)
                {
                    var sp = sps[Random.Range(0, sps.Length)];

                    // Reset controller
                    if (characterController != null)
                        characterController.enabled = false;

                    transform.position = sp.transform.position;
                    transform.rotation = sp.transform.rotation;

                    if (characterController != null)
                        characterController.enabled = true;
                }

                RespawnTimer = TickTimer.None;
                Debug.Log("[Player] Respawned");
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_Fire()
        {
            Debug.Log($"[RPC_Fire] Called on authority: {Object.HasStateAuthority}");

            if (!Object.HasStateAuthority)
            {
                Debug.LogError("[RPC_Fire] ERROR: Not state authority!");
                return;
            }

            if (firePoint == null)
            {
                Debug.LogError("[RPC_Fire] ERROR: firePoint is NULL!");
                return;
            }

            var pool = FindObjectOfType<NetworkProjectilePool>();
            if (pool == null)
            {
                Debug.LogError("[RPC_Fire] ERROR: Pool not found!");
                return;
            }

            var pooledProjectile = pool.GetPooledProjectile();
            if (pooledProjectile == null)
            {
                Debug.LogError("[RPC_Fire] ERROR: No projectile from pool!");
                return;
            }

            var projectile = pooledProjectile.GetComponent<Projectile>();
            if (projectile == null)
            {
                Debug.LogError("[RPC_Fire] ERROR: Projectile component missing!");
                return;
            }

            pooledProjectile.transform.position = firePoint.position;
            pooledProjectile.transform.rotation = firePoint.rotation;
            projectile.Activate(Object.InputAuthority, firePoint.forward);

            Debug.Log($"[RPC_Fire] Projectile fired from {firePoint.position}");
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
                Debug.Log($"[Player] {PlayerName} died");

                if (Runner.TryGetPlayerObject(attacker, out NetworkObject attackerObj))
                {
                    var attackerPC = attackerObj.GetComponent<PlayerController>();
                    if (attackerPC != null)
                    {
                        attackerPC.Kills++;
                        Debug.Log($"[Player] {attackerPC.PlayerName} got a kill!");
                        Events.RaiseUpdateScore(attackerPC.PlayerName.ToString(), attackerPC.Kills);
                    }
                }

                RespawnTimer = TickTimer.CreateFromSeconds(Runner, 2f);
            }
        }

        public void EnableCombat(bool enable)
        {
            combatEnabled = enable;
            if (firePoint != null)
                firePoint.gameObject.SetActive(enable);
            Debug.Log($"[Player] Combat enabled: {enable}");
        }
    }

    public struct NetworkInputData : Fusion.INetworkInput
    {
        public Vector2 moveInput;
        public Vector2 lookInput;
        public bool fire;
    }
}