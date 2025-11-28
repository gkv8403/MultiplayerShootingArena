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

        private bool hasInitializedInput = false;

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

            // ===== FIX: Only setup camera for input authority =====
            if (Object.HasInputAuthority)
            {
                var cam = Camera.main;
                if (cam != null)
                {
                    var camController = cam.GetComponent<CameraController>();
                    if (camController == null)
                        camController = cam.gameObject.AddComponent<CameraController>();
                    camController.target = transform;
                    Debug.Log("[Player] Camera controller assigned");
                }
            }

            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
                if (characterController == null)
                    Debug.LogError("[Player] CharacterController not found!");
            }

            hasInitializedInput = true;
        }

        public override void FixedUpdateNetwork()
        {
            // ===== Only process input on input authority =====
            if (!Object.HasInputAuthority)
                return;

            if (!GetInput(out NetworkInputData data))
            {
                Debug.LogWarning("[Player] No input data received");
                return;
            }

            if (Health <= 0)
                return;

            // ===== MOVEMENT =====
            if (characterController != null && characterController.enabled)
            {
                Vector3 move = new Vector3(data.moveInput.x, 0, data.moveInput.y);
                move = transform.TransformDirection(move);
                characterController.Move(move * moveSpeed * Runner.DeltaTime);
            }

            // ===== ROTATION =====
            if (data.lookInput.sqrMagnitude > 0.01f)
            {
                float yaw = data.lookInput.x * rotationSpeed * Runner.DeltaTime;
                transform.Rotate(0, yaw, 0);
            }

            // ===== FIRE - SEND RPC =====
            if (data.fire)
            {
                if (fireTimer.ExpiredOrNotRunning(Runner))
                {
                    Debug.Log("[Player] Fire input received - calling RPC_Fire");
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
                    transform.position = sp.transform.position + Vector3.up * 1f;
                    transform.rotation = sp.transform.rotation;
                }

                RespawnTimer = TickTimer.None;
            }
        }

        // ===== RPC: FIRED BY INPUT AUTHORITY, EXECUTED ON STATE AUTHORITY =====
        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_Fire()
        {
            Debug.Log($"[RPC_Fire] Called. HasStateAuthority: {Object.HasStateAuthority}, HasInputAuthority: {Object.HasInputAuthority}");

            if (!Object.HasStateAuthority)
            {
                Debug.LogError("[RPC_Fire] ERROR: RPC executed on non-authority!");
                return;
            }

            Debug.Log("[RPC_Fire] Executing on state authority - spawning projectile");

            // ===== GET POOL =====
            var pool = FindObjectOfType<NetworkProjectilePool>();
            if (pool == null)
            {
                Debug.LogError("[RPC_Fire] ERROR: ProjectilePool not found in scene!");
                return;
            }

            Debug.Log("[RPC_Fire] Pool found");

            // ===== GET PROJECTILE =====
            var pooledProjectile = pool.GetPooledProjectile();
            if (pooledProjectile == null)
            {
                Debug.LogError("[RPC_Fire] ERROR: No projectile available from pool!");
                return;
            }

            Debug.Log($"[RPC_Fire] Got projectile from pool: {pooledProjectile.gameObject.name}");

            // ===== GET PROJECTILE COMPONENT =====
            var projectile = pooledProjectile.GetComponent<Projectile>();
            if (projectile == null)
            {
                Debug.LogError("[RPC_Fire] ERROR: Projectile component not found on pooled object!");
                return;
            }

            // ===== CHECK FIREPOINT =====
            if (firePoint == null)
            {
                Debug.LogError("[RPC_Fire] ERROR: firePoint is NULL! Cannot fire!");
                return;
            }

            Debug.Log($"[RPC_Fire] FirePoint found at position: {firePoint.position}");

            // ===== SETUP PROJECTILE =====
            pooledProjectile.transform.position = firePoint.position;
            pooledProjectile.transform.rotation = firePoint.rotation;

            Debug.Log($"[RPC_Fire] Projectile positioned at {firePoint.position}");

            // ===== ACTIVATE PROJECTILE =====
            projectile.Activate(Object.InputAuthority, transform.forward);

            Debug.Log($"[RPC_Fire] Projectile activated - will move forward");
        }

        // ===== DAMAGE RPC =====
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_TakeDamage(int dmg, PlayerRef attacker)
        {
            if (!Object.HasStateAuthority) return;

            if (Health <= 0) return;

            Health -= dmg;
            Debug.Log($"[Player] {PlayerName} took {dmg} damage. Health: {Health}");

            if (healthBar != null)
            {
                healthBar.fillAmount = (float)Health / maxHealth;
            }

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
            if (firePoint != null)
                firePoint.gameObject.SetActive(enable);
        }
    }

    public struct NetworkInputData : Fusion.INetworkInput
    {
        public Vector2 moveInput;
        public Vector2 lookInput;
        public bool fire;
    }
}