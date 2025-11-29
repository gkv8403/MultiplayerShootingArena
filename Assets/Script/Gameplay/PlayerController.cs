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
        [Networked] public bool CombatEnabled { get; set; } // NOW NETWORKED!

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
        public Transform headTransform;
        public Transform gunPoint;

        private Renderer[] renderers;
        private Collider[] colliders;

        // Client-side prediction
        private Vector3 previousPosition;
        private float positionErrorThreshold = 0.5f;

        public override void Spawned()
        {
            Debug.Log($"[Player] ===== PLAYER SPAWNED =====");
            Debug.Log($"[Player] Object ID: {Object.Id}");
            Debug.Log($"[Player] HasInputAuthority: {Object.HasInputAuthority}");
            Debug.Log($"[Player] HasStateAuthority: {Object.HasStateAuthority}");
            Debug.Log($"[Player] InputAuthority: {Object.InputAuthority}");
            Debug.Log($"[Player] Position: {transform.position}");

            // Initialize components
            if (characterController == null)
            {
                characterController = GetComponent<CharacterController>();
                if (characterController == null)
                {
                    Debug.Log("[Player] Creating CharacterController component");
                    characterController = gameObject.AddComponent<CharacterController>();
                    characterController.center = Vector3.up * 0.9f;
                    characterController.height = 1.8f;
                    characterController.radius = 0.3f;
                }
            }
            Debug.Log($"[Player] CharacterController: {(characterController != null ? "OK" : "MISSING")}");

            // Setup head transform
            headTransform = transform.Find("Head");
            if (headTransform == null)
            {
                Debug.Log("[Player] Creating Head transform");
                GameObject headObj = new GameObject("Head");
                headTransform = headObj.transform;
                headTransform.SetParent(transform);
                headTransform.localPosition = new Vector3(0, 1.6f, 0);
                headTransform.localRotation = Quaternion.identity;
            }
            Debug.Log($"[Player] Head transform: {(headTransform != null ? "OK" : "MISSING")}");

            // Setup fire point
            if (firePoint == null)
            {
                Debug.Log("[Player] Setting up FirePoint");
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
                else
                {
                    Debug.LogWarning("[Player] No gunPoint reference! Creating at root");
                    GameObject fpObj = new GameObject("FirePoint");
                    fpObj.transform.SetParent(transform);
                    fpObj.transform.localPosition = new Vector3(0, 1.6f, 0.5f);
                    fpObj.transform.localRotation = Quaternion.identity;
                    firePoint = fpObj.transform;
                }
            }
            Debug.Log($"[Player] FirePoint: {(firePoint != null ? "OK at " + firePoint.position : "MISSING")}");

            // Cache renderers and colliders
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);
            Debug.Log($"[Player] Found {renderers.Length} renderers and {colliders.Length} colliders");

            // Hide respawn countdown initially
            if (respawnCountdownText != null)
            {
                respawnCountdownText.gameObject.SetActive(false);
                Debug.Log("[Player] Respawn countdown hidden");
            }

            // Initialize on State Authority (Server/Host)
            if (Object.HasStateAuthority)
            {
                Debug.Log("[Player] Initializing as STATE AUTHORITY (Server)");
                Health = maxHealth;
                Kills = 0;
                Deaths = 0;
                VerticalRotation = 0f;
                IsDead = false;
                CombatEnabled = false; // Start disabled, GameManager will enable
                PlayerName = PlayerPrefs.GetString("PlayerName", $"Player{Object.InputAuthority.PlayerId}");
                Debug.Log($"[Player] Player name set to: {PlayerName}");

                // Spawn at spawn point
                var sps = FindObjectsOfType<SpawnPoint>();
                if (sps.Length > 0)
                {
                    var sp = sps[Random.Range(0, sps.Length)];
                    characterController.enabled = false;
                    transform.position = sp.transform.position;
                    Debug.Log($"[Player] Set position to spawn point: {sp.transform.position}");

                    Vector3 dirToCenter = (Vector3.zero - transform.position);
                    dirToCenter.y = 0;
                    if (dirToCenter.sqrMagnitude > 0.001f)
                    {
                        transform.rotation = Quaternion.LookRotation(dirToCenter.normalized, Vector3.up);
                    }

                    characterController.enabled = true;
                }
                else
                {
                    Debug.LogWarning("[Player] No spawn points found! Using default position");
                }

                NetPosition = transform.position;
                NetRotation = transform.rotation;
                Debug.Log($"[Player] NetPosition initialized: {NetPosition}");
            }
            else
            {
                Debug.Log("[Player] Not state authority, will receive data from server");
            }

            // Store initial position for prediction
            previousPosition = transform.position;
            Debug.Log($"[Player] Previous position set: {previousPosition}");

            // Update UI for all clients
            if (nameText != null)
            {
                nameText.text = PlayerName.ToString();
                Debug.Log($"[Player] Name text updated to: {PlayerName}");
            }

            if (healthBar != null)
            {
                healthBar.fillAmount = 1f;
                Debug.Log("[Player] Health bar set to full");
            }

            // Broadcast initial score
            if (Object.HasStateAuthority)
            {
                RPC_BroadcastScore(PlayerName.ToString(), Kills);
                Debug.Log($"[Player] Broadcasting initial score for {PlayerName}");
            }

            Debug.Log($"[Player] ===== SPAWN COMPLETE at {transform.position} =====");
        }

        public override void FixedUpdateNetwork()
        {
            // Handle state authority (server/host) - authoritative simulation
            if (Object.HasStateAuthority)
            {
                HandleStateAuthority();
            }

            // Handle input authority (local player) - client-side prediction
            if (Object.HasInputAuthority)
            {
                HandleLocalPlayer();
            }
            // Remote players - interpolate to network position
            else
            {
                InterpolateRemotePlayer();
            }
        }

        private void HandleStateAuthority()
        {
            // Handle respawn timer
            if (IsDead && RespawnTimer.Expired(Runner))
            {
                Respawn();
            }

            // Process input from client if available
            if (GetInput(out NetworkInputData data))
            {
                if (!IsDead && characterController != null && characterController.enabled)
                {
                    // Server applies movement
                    Vector3 move = new Vector3(data.moveInput.x, 0, data.moveInput.y);
                    move = transform.TransformDirection(move);
                    move.y = 0;

                    characterController.Move(move * moveSpeed * Runner.DeltaTime);
                    NetPosition = transform.position;

                    // Apply rotation
                    if (Mathf.Abs(data.lookInput.x) > 0.01f)
                    {
                        float yaw = data.lookInput.x * rotationSpeed * Runner.DeltaTime;
                        transform.Rotate(0, yaw, 0, Space.World);
                        NetRotation = transform.rotation;
                    }

                    // Vertical rotation
                    if (Mathf.Abs(data.lookInput.y) > 0.01f)
                    {
                        VerticalRotation -= data.lookInput.y * verticalLookSpeed * Runner.DeltaTime;
                        VerticalRotation = Mathf.Clamp(VerticalRotation, -80f, 80f);
                    }

                    // Fire handling
                    if (data.fire && CombatEnabled && !IsDead)
                    {
                        if (fireTimer.ExpiredOrNotRunning(Runner))
                        {
                            Fire(firePoint.position, firePoint.forward);
                            fireTimer = TickTimer.CreateFromSeconds(Runner, fireRate);
                        }
                    }
                }
            }
        }

        private void HandleLocalPlayer()
        {
            if (!GetInput(out NetworkInputData data)) return;
            if (IsDead) return;

            // Client-side prediction for smooth movement
            if (characterController != null && characterController.enabled)
            {
                Vector3 move = new Vector3(data.moveInput.x, 0, data.moveInput.y);
                move = transform.TransformDirection(move);
                move.y = 0;

                previousPosition = transform.position;
                characterController.Move(move * moveSpeed * Runner.DeltaTime);

                // Apply rotation immediately for responsive feel
                if (Mathf.Abs(data.lookInput.x) > 0.01f)
                {
                    float yaw = data.lookInput.x * rotationSpeed * Runner.DeltaTime;
                    transform.Rotate(0, yaw, 0, Space.World);
                }
            }
        }

        private void InterpolateRemotePlayer()
        {
            // Smooth interpolation for remote players
            if (Vector3.Distance(transform.position, NetPosition) > positionErrorThreshold)
            {
                // Large error, snap to network position
                if (characterController != null)
                    characterController.enabled = false;

                transform.position = NetPosition;

                if (characterController != null)
                    characterController.enabled = true;
            }
            else
            {
                // Small error, smooth interpolation
                Vector3 targetPos = Vector3.Lerp(transform.position, NetPosition, 15f * Runner.DeltaTime);
                if (characterController != null && characterController.enabled)
                {
                    Vector3 delta = targetPos - transform.position;
                    characterController.Move(delta);
                }
            }

            // Smooth rotation
            transform.rotation = Quaternion.Slerp(transform.rotation, NetRotation, 15f * Runner.DeltaTime);
        }

        private void Update()
        {
            if (Object == null) return;

            // Update head rotation for all players
            if (headTransform != null)
            {
                headTransform.localRotation = Quaternion.Euler(VerticalRotation, 0, 0);
            }

            // Update respawn countdown for LOCAL dead player
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
            else if (respawnCountdownText != null && respawnCountdownText.gameObject.activeSelf)
            {
                respawnCountdownText.gameObject.SetActive(false);
            }
        }

        // Server-side fire, then RPC to show visual to all
        private void Fire(Vector3 position, Vector3 direction)
        {
            if (!Object.HasStateAuthority || IsDead || !CombatEnabled) return;

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
                Debug.Log($"[Player] {PlayerName} fired projectile");

                // Notify all clients to show fire visual/sound
                RPC_OnFireVisual();
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_OnFireVisual()
        {
            // Add muzzle flash, sound effects, etc. here
            Debug.Log($"[Player] Fire visual for {PlayerName}");
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_TakeDamage(int dmg, PlayerRef attacker)
        {
            if (IsDead) return;

            Health -= dmg;

            // Update health bar on all clients
            if (healthBar != null)
                healthBar.fillAmount = (float)Health / maxHealth;

            Debug.Log($"[Player] {PlayerName} took {dmg} damage. Health: {Health}");

            // Handle death
            if (Health <= 0 && Object.HasStateAuthority)
            {
                Health = 0;
                IsDead = true;
                Deaths++;

                // Hide player on all clients
                RPC_SetVisible(false);

                // Award kill to attacker
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
                Debug.Log($"[Player] {PlayerName} died! Respawning in 2s");
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_BroadcastScore(NetworkString<_16> pName, int kills)
        {
            Events.RaiseUpdateScore(pName.ToString(), kills);
            Debug.Log($"[Player] Broadcasting score: {pName} - {kills} kills");
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SetVisible(bool visible)
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
                    if (c != null && !(c is CharacterController))
                        c.enabled = visible;
                }
            }

            // Keep character controller enabled
            if (characterController != null)
                characterController.enabled = true;

            // Toggle UI (except respawn countdown for local player)
            if (playerCanvas != null)
                playerCanvas.enabled = visible;

            Debug.Log($"[Player] {PlayerName} visibility set to {visible}");
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

                if (characterController != null)
                    characterController.enabled = false;

                transform.position = sp.transform.position;

                Vector3 dirToCenter = (Vector3.zero - transform.position);
                dirToCenter.y = 0;
                if (dirToCenter.sqrMagnitude > 0.001f)
                {
                    transform.rotation = Quaternion.LookRotation(dirToCenter.normalized, Vector3.up);
                }

                if (headTransform != null)
                    headTransform.localRotation = Quaternion.identity;

                if (characterController != null)
                    characterController.enabled = true;
            }

            // Update networked transform
            NetPosition = transform.position;
            NetRotation = transform.rotation;
            previousPosition = transform.position;

            // Show player and update health bar on all clients
            RPC_SetVisible(true);
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

        public void EnableCombat(bool enable)
        {
            if (Object.HasStateAuthority)
            {
                CombatEnabled = enable;
                Debug.Log($"[Player] {PlayerName} combat enabled: {enable}");
            }
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