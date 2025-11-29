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
        [Networked] public Vector3 NetPosition { get; set; }
        [Networked] public Quaternion NetRotation { get; set; }
        [Networked] public TickTimer RespawnTimer { get; set; }
        [Networked] public bool IsDead { get; set; }

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
        public TextMeshProUGUI respawnCountdownText;

        public int maxHealth = 100;
        private bool combatEnabled = false;
        public Transform headTransform;
        public Transform gunPoint;

        private Renderer[] renderers;
        private Collider[] colliders;

        // Interpolation for smooth movement
        private Vector3 smoothPosition;
        private Quaternion smoothRotation;
        private const float POSITION_THRESHOLD = 0.01f;

        public override void Spawned()
        {
            Debug.Log($"[Player] Spawned (HasInputAuth:{Object.HasInputAuthority}, HasStateAuth:{Object.HasStateAuthority})");

            // Initialize components
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

            // Setup head transform
            headTransform = transform.Find("Head");
            if (headTransform == null)
            {
                GameObject headObj = new GameObject("Head");
                headTransform = headObj.transform;
                headTransform.SetParent(transform);
                headTransform.localPosition = new Vector3(0, 1.6f, 0);
                headTransform.localRotation = Quaternion.identity;
            }

            // Setup fire point
            if (firePoint == null)
            {
                var fp = transform.Find("FirePoint");
                if (fp == null && gunPoint != null)
                {
                    GameObject fpObj = new GameObject("FirePoint");
                    fpObj.transform.SetParent(gunPoint);
                    fpObj.transform.localPosition = new Vector3(0, 0, 0.5f);
                    fpObj.transform.localRotation = Quaternion.identity;
                    firePoint = fpObj.transform;
                }
                else if (fp != null)
                    firePoint = fp;
            }

            // Cache renderers and colliders
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);

            // Hide respawn countdown initially
            if (respawnCountdownText != null)
                respawnCountdownText.gameObject.SetActive(false);

            // Initialize on State Authority (Server/Host)
            if (Object.HasStateAuthority)
            {
                Health = maxHealth;
                Kills = 0;
                Deaths = 0;
                VerticalRotation = 0f;
                IsDead = false;
                PlayerName = PlayerPrefs.GetString("PlayerName", $"Player{Object.InputAuthority.PlayerId}");

                // Spawn at spawn point
                var sps = FindObjectsOfType<SpawnPoint>();
                if (sps.Length > 0)
                {
                    var sp = sps[Random.Range(0, sps.Length)];

                    // Disable character controller temporarily
                    characterController.enabled = false;

                    transform.position = sp.transform.position;

                    // Face towards center (0,0,0)
                    Vector3 dirToCenter = (Vector3.zero - transform.position);
                    dirToCenter.y = 0; // Keep on horizontal plane
                    if (dirToCenter.sqrMagnitude > 0.001f)
                    {
                        transform.rotation = Quaternion.LookRotation(dirToCenter.normalized, Vector3.up);
                    }

                    // Re-enable character controller
                    characterController.enabled = true;
                }

                // Initialize networked transform
                NetPosition = transform.position;
                NetRotation = transform.rotation;
            }

            // Initialize smooth interpolation
            smoothPosition = transform.position;
            smoothRotation = transform.rotation;

            // Update UI
            if (nameText != null)
                nameText.text = PlayerName.ToString();

            if (healthBar != null)
                healthBar.fillAmount = 1f;

            // Broadcast initial score
            if (Object.HasStateAuthority)
            {
                RPC_BroadcastScore(PlayerName.ToString(), Kills);
            }

            Debug.Log($"[Player] Setup complete at {transform.position}");
        }

        public override void FixedUpdateNetwork()
        {
            // Handle input authority (local player)
            if (Object.HasInputAuthority)
            {
                HandleLocalPlayer();
            }

            // Handle state authority (server/host)
            if (Object.HasStateAuthority)
            {
                HandleStateAuthority();
            }
        }

        private void HandleLocalPlayer()
        {
            if (!GetInput(out NetworkInputData data)) return;

            // Check if dead
            if (IsDead)
            {
                // Update respawn countdown UI
                if (Object.HasStateAuthority && respawnCountdownText != null)
                {
                    float timeLeft = RespawnTimer.RemainingTime(Runner) ?? 0f;
                    if (timeLeft > 0)
                    {
                        respawnCountdownText.gameObject.SetActive(true);
                        respawnCountdownText.text = $"Respawning in {Mathf.CeilToInt(timeLeft)}s";
                    }
                }
                return;
            }

            // Movement
            if (characterController != null && characterController.enabled)
            {
                Vector3 move = new Vector3(data.moveInput.x, 0, data.moveInput.y);
                move = transform.TransformDirection(move);
                move.y = 0;

                Vector3 newPosition = transform.position + move * moveSpeed * Runner.DeltaTime;
                characterController.Move(move * moveSpeed * Runner.DeltaTime);

                // Update networked position only if moved significantly
                if (Vector3.Distance(transform.position, NetPosition) > POSITION_THRESHOLD)
                {
                    NetPosition = transform.position;
                }
            }

            // Horizontal rotation (Y-axis)
            if (Mathf.Abs(data.lookInput.x) > 0.01f)
            {
                float yaw = data.lookInput.x * rotationSpeed * Runner.DeltaTime;
                transform.Rotate(0, yaw, 0, Space.World);
                NetRotation = transform.rotation;
            }

            // Vertical rotation (head/camera pitch)
            if (Mathf.Abs(data.lookInput.y) > 0.01f && headTransform != null)
            {
                VerticalRotation -= data.lookInput.y * verticalLookSpeed * Runner.DeltaTime;
                VerticalRotation = Mathf.Clamp(VerticalRotation, -80f, 80f);
            }

            // Fire handling
            if (data.fire && combatEnabled && !IsDead)
            {
                if (fireTimer.ExpiredOrNotRunning(Runner))
                {
                    RPC_Fire(firePoint.position, firePoint.forward);
                    fireTimer = TickTimer.CreateFromSeconds(Runner, fireRate);
                }
            }
        }

        private void HandleStateAuthority()
        {
            // Handle respawn timer
            if (IsDead && RespawnTimer.Expired(Runner))
            {
                Respawn();
            }
        }

        private void Update()
        {
            if (Object == null) return;

            // Update head rotation for all players
            if (headTransform != null)
            {
                headTransform.localRotation = Quaternion.Euler(VerticalRotation, 0, 0);
            }

            // Smooth interpolation for remote players
            if (!Object.HasInputAuthority)
            {
                // Smooth position interpolation
                smoothPosition = Vector3.Lerp(smoothPosition, NetPosition, 12f * Time.deltaTime);
                transform.position = smoothPosition;

                // Smooth rotation interpolation
                smoothRotation = Quaternion.Slerp(smoothRotation, NetRotation, 12f * Time.deltaTime);
                transform.rotation = smoothRotation;
            }

            // Update respawn countdown for local player
            if (Object.HasInputAuthority && IsDead && respawnCountdownText != null)
            {
                float timeLeft = RespawnTimer.RemainingTime(Runner) ?? 0f;
                if (timeLeft > 0)
                {
                    respawnCountdownText.gameObject.SetActive(true);
                    respawnCountdownText.text = $"Respawning in {Mathf.CeilToInt(timeLeft)}s";
                }
                else
                {
                    respawnCountdownText.gameObject.SetActive(false);
                }
            }
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_Fire(Vector3 position, Vector3 direction)
        {
            if (!Object.HasStateAuthority || IsDead || !combatEnabled) return;

            var pool = NetworkProjectilePool.Instance;
            if (pool == null)
            {
                Debug.LogWarning("[Player] Pool not found");
                return;
            }

            var no = pool.GetPooledProjectile();
            if (no == null)
            {
                Debug.LogWarning("[Player] No projectile available");
                return;
            }

            var proj = no.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.Fire(Object.InputAuthority, position, direction);
                Debug.Log($"[Player] Fired projectile from {position}");
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_TakeDamage(int dmg, PlayerRef attacker)
        {
            if (IsDead) return;

            Health -= dmg;

            // Update health bar
            if (healthBar != null)
                healthBar.fillAmount = (float)Health / maxHealth;

            Debug.Log($"[Player] {PlayerName} took {dmg} damage. Health: {Health}");

            // Handle death
            if (Health <= 0)
            {
                Health = 0;
                IsDead = true;
                Deaths++;

                SetPlayerVisible(false);

                // Award kill to attacker
                if (Object.HasStateAuthority)
                {
                    if (Runner.TryGetPlayerObject(attacker, out NetworkObject attackerObj))
                    {
                        var attackerPC = attackerObj.GetComponent<PlayerController>();
                        if (attackerPC != null && attackerPC != this)
                        {
                            attackerPC.Kills++;
                            attackerPC.RPC_BroadcastScore(attackerPC.PlayerName.ToString(), attackerPC.Kills);
                            Debug.Log($"[Player] {attackerPC.PlayerName} got a kill! Total: {attackerPC.Kills}");
                        }
                    }

                    // Start respawn timer
                    RespawnTimer = TickTimer.CreateFromSeconds(Runner, 2f);
                }

                Debug.Log($"[Player] {PlayerName} died!");
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_BroadcastScore(NetworkString<_16> pName, int kills)
        {
            Events.RaiseUpdateScore(pName.ToString(), kills);
            Debug.Log($"[Player] Broadcasting score: {pName} - {kills} kills");
        }

        private void Respawn()
        {
            if (!Object.HasStateAuthority) return;

            Debug.Log($"[Player] Respawning {PlayerName}");

            // Reset health and state
            Health = maxHealth;
            IsDead = false;
            VerticalRotation = 0f;
            RespawnTimer = TickTimer.None;

            // Find spawn point
            var sps = FindObjectsOfType<SpawnPoint>();
            if (sps.Length > 0)
            {
                var sp = sps[Random.Range(0, sps.Length)];

                // Disable character controller temporarily
                if (characterController != null)
                    characterController.enabled = false;

                transform.position = sp.transform.position;

                // Face towards center
                Vector3 dirToCenter = (Vector3.zero - transform.position);
                dirToCenter.y = 0;
                if (dirToCenter.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(dirToCenter.normalized, Vector3.up);
                }

                if (headTransform != null)
                    headTransform.localRotation = Quaternion.identity;

                // Re-enable character controller
                if (characterController != null)
                    characterController.enabled = true;
            }

            // Update networked transform
            NetPosition = transform.position;
            NetRotation = transform.rotation;
            smoothPosition = transform.position;
            smoothRotation = transform.rotation;

            // Show player
            SetPlayerVisible(true);

            // Update UI
            RPC_UpdateHealthBar();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_UpdateHealthBar()
        {
            if (healthBar != null)
                healthBar.fillAmount = (float)Health / maxHealth;

            if (respawnCountdownText != null)
                respawnCountdownText.gameObject.SetActive(false);
        }

        private void SetPlayerVisible(bool visible)
        {
            // Toggle renderers
            if (renderers != null)
            {
                foreach (var r in renderers)
                {
                    if (r != null)
                        r.enabled = visible;
                }
            }

            // Toggle colliders
            if (colliders != null)
            {
                foreach (var c in colliders)
                {
                    if (c != null)
                        c.enabled = visible;
                }
            }

            // Keep character controller enabled for movement
            if (characterController != null)
                characterController.enabled = true;

            // Toggle UI
            if (playerCanvas != null)
                playerCanvas.enabled = visible;
        }

        public void EnableCombat(bool enable)
        {
            combatEnabled = enable;
            Debug.Log($"[Player] {PlayerName} combat enabled: {enable}");
        }

        private void OnDisable()
        {
            if (respawnCountdownText != null)
                respawnCountdownText.gameObject.SetActive(false);
        }
    }

    public struct NetworkInputData : INetworkInput
    {
        public Vector2 moveInput;
        public Vector2 lookInput;
        public bool fire;
    }
}