using Fusion;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

namespace Scripts.Gameplay
{
    /// <summary>
    /// Main player controller handling movement, shooting, health, and network synchronization.
    /// Uses Fusion's NetworkBehaviour for multiplayer state management.
    /// </summary>
    public class PlayerController : NetworkBehaviour
    {
        #region Networked Properties
        [Networked] public int Health { get; set; }
        [Networked] public int Kills { get; set; }
        [Networked] public int Deaths { get; set; }
        [Networked] public NetworkString<_16> PlayerName { get; set; }
        [Networked] public float VerticalRotation { get; set; }
        [Networked] public Vector3 NetPosition { get; set; }
        [Networked] public Quaternion NetRotation { get; set; }
        [Networked] public TickTimer RespawnTimer { get; set; }
        [Networked] public bool IsDead { get; set; }
        [Networked] public bool CombatEnabled { get; set; }
        #endregion

        #region Movement Settings
        [Header("Movement")]
        public CharacterController characterController;
        public float moveSpeed = 5f;
        public float rotationSpeed = 150f;
        public float verticalLookSpeed = 100f;
        #endregion

        #region Combat Settings
        [Header("Combat")]
        public Transform firePoint;
        public float fireRate = 0.15f;
        private TickTimer fireTimer;
        public int maxHealth = 100;
        #endregion

        #region UI Elements
        [Header("UI")]
        public TextMeshProUGUI nameText;
        public Image healthBar;
        public Canvas playerCanvas;
        public TextMeshProUGUI respawnCountdownText;
        #endregion

        #region Components
        [Header("Components")]
        public Transform headTransform;
        public Transform gunPoint;
        #endregion

        #region Network Optimization
        [Header("Network Optimization")]
        public float positionSyncThreshold = 0.1f;
        public float minSyncInterval = 0.05f;
        public float rotationSyncThreshold = 5f;

        private Vector3 lastSyncedPosition;
        private Quaternion lastSyncedRotation;
        private float lastSyncTime;
        #endregion

        private Renderer[] renderers;
        private Collider[] colliders;

        #region Initialization
        public override void Spawned()
        {
            InitializeComponents();
            SetupPlayer();

            if (Object.HasStateAuthority)
            {
                InitializeServerState();
                SpawnAtRandomPoint();
                Invoke(nameof(BroadcastInitialScore), 2f);
            }

            lastSyncedPosition = transform.position;
            lastSyncedRotation = transform.rotation;
            lastSyncTime = Time.time;

            UpdateUI();
        }

        /// <summary>
        /// Initialize all required components
        /// </summary>
        private void InitializeComponents()
        {
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

            if (headTransform == null)
            {
                GameObject headObj = new GameObject("Head");
                headTransform = headObj.transform;
                headTransform.SetParent(transform);
                headTransform.localPosition = new Vector3(0, 1.6f, 0);
            }

            if (firePoint == null)
            {
                var fp = transform.Find("FirePoint");
                if (fp == null && gunPoint != null)
                {
                    GameObject fpObj = new GameObject("FirePoint");
                    fpObj.transform.SetParent(gunPoint);
                    fpObj.transform.localPosition = new Vector3(0, 0, 0.5f);
                    firePoint = fpObj.transform;
                }
                else if (fp != null)
                {
                    firePoint = fp;
                }
                else
                {
                    GameObject fpObj = new GameObject("FirePoint");
                    fpObj.transform.SetParent(transform);
                    fpObj.transform.localPosition = new Vector3(0, 1.6f, 0.5f);
                    firePoint = fpObj.transform;
                }
            }
        }

        /// <summary>
        /// Setup player components and renderers
        /// </summary>
        private void SetupPlayer()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);

            if (respawnCountdownText != null)
                respawnCountdownText.gameObject.SetActive(false);
        }

        /// <summary>
        /// Initialize server-side state for new player
        /// </summary>
        private void InitializeServerState()
        {
            Health = maxHealth;
            Kills = 0;
            Deaths = 0;
            VerticalRotation = 0f;
            IsDead = false;
            CombatEnabled = false;

            // Generate random player name
            PlayerName = GenerateRandomPlayerName();
        }

        /// <summary>
        /// Generate random player name in format: abc123
        /// </summary>
        private string GenerateRandomPlayerName()
        {
            const string chars = "abcdefghijklmnopqrstuvwxyz";
            const string numbers = "0123456789";

            string name = "";
            for (int i = 0; i < 3; i++)
            {
                name += chars[Random.Range(0, chars.Length)];
            }
            for (int i = 0; i < 3; i++)
            {
                name += numbers[Random.Range(0, numbers.Length)];
            }

            return name;
        }

        /// <summary>
        /// Spawn player at random spawn point
        /// </summary>
        private void SpawnAtRandomPoint()
        {
            var spawnPoints = FindObjectsOfType<SpawnPoint>();
            if (spawnPoints.Length > 0)
            {
                var sp = spawnPoints[Random.Range(0, spawnPoints.Length)];
                characterController.enabled = false;
                transform.position = sp.transform.position;

                Vector3 dirToCenter = (Vector3.zero - transform.position);
                dirToCenter.y = 0;
                if (dirToCenter.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(dirToCenter.normalized, Vector3.up);

                characterController.enabled = true;
            }

            NetPosition = transform.position;
            NetRotation = transform.rotation;
        }

        /// <summary>
        /// Broadcast initial score to all clients after spawn
        /// </summary>
        private void BroadcastInitialScore()
        {
            if (Object.HasStateAuthority)
            {
                RPC_UpdateScoreOnAllClients(PlayerName, 0);
            }
        }

        /// <summary>
        /// Update UI elements
        /// </summary>
        private void UpdateUI()
        {
            if (nameText != null)
                nameText.text = PlayerName.ToString();

            if (healthBar != null)
                healthBar.fillAmount = 1f;
        }
        #endregion

        #region Network Update
        public override void FixedUpdateNetwork()
        {
            if (Object.HasStateAuthority)
            {
                HandleServerAuthority();
            }
            else
            {
                InterpolateToNetworkState();
            }
        }

        /// <summary>
        /// Handle server-side player logic including movement, rotation, and shooting
        /// </summary>
        private void HandleServerAuthority()
        {
            // Handle respawn timer
            if (IsDead && RespawnTimer.Expired(Runner))
            {
                Respawn();
                return;
            }

            if (GetInput(out NetworkInputData data))
            {
                if (!IsDead && characterController != null && characterController.enabled)
                {
                    HandleMovement(data);
                    HandleRotation(data);
                    HandleShooting(data);
                }
            }
        }

        /// <summary>
        /// Handle player movement with vertical support
        /// </summary>
        private void HandleMovement(NetworkInputData data)
        {
            Vector3 move = new Vector3(data.moveInput.x, data.verticalMove, data.moveInput.y);
            move = transform.TransformDirection(move);
            characterController.Move(move * moveSpeed * Runner.DeltaTime);

            // Sync position if threshold exceeded
            bool shouldSyncPosition = false;
            bool shouldSyncRotation = false;
            float timeSinceLastSync = Time.time - lastSyncTime;

            if (timeSinceLastSync >= minSyncInterval)
            {
                float distanceMoved = Vector3.Distance(transform.position, lastSyncedPosition);
                if (distanceMoved >= positionSyncThreshold)
                    shouldSyncPosition = true;

                float angleDiff = Quaternion.Angle(transform.rotation, lastSyncedRotation);
                if (angleDiff >= rotationSyncThreshold)
                    shouldSyncRotation = true;
            }

            if (shouldSyncPosition || shouldSyncRotation)
            {
                if (shouldSyncPosition)
                {
                    NetPosition = transform.position;
                    lastSyncedPosition = transform.position;
                }

                if (shouldSyncRotation)
                {
                    NetRotation = transform.rotation;
                    lastSyncedRotation = transform.rotation;
                }

                lastSyncTime = Time.time;
            }
        }

        /// <summary>
        /// Handle player rotation (horizontal and vertical)
        /// </summary>
        private void HandleRotation(NetworkInputData data)
        {
            // Horizontal rotation
            if (Mathf.Abs(data.lookInput.x) > 0.01f)
            {
                float yaw = data.lookInput.x * rotationSpeed * Runner.DeltaTime;
                transform.Rotate(0, yaw, 0, Space.World);
            }

            // Vertical rotation (pitch)
            if (Mathf.Abs(data.lookInput.y) > 0.01f)
            {
                VerticalRotation -= data.lookInput.y * verticalLookSpeed * Runner.DeltaTime;
                VerticalRotation = Mathf.Clamp(VerticalRotation, -80f, 80f);
            }
        }

        /// <summary>
        /// Handle shooting logic with fire rate control
        /// </summary>
        private void HandleShooting(NetworkInputData data)
        {
            if (data.fire && CombatEnabled && !IsDead)
            {
                if (fireTimer.ExpiredOrNotRunning(Runner))
                {
                    Vector3 firePos = firePoint != null ? firePoint.position : (transform.position + Vector3.up * 1.6f);
                    Vector3 fireDir = firePoint != null ? firePoint.forward : transform.forward;

                    Fire(firePos, fireDir);
                    fireTimer = TickTimer.CreateFromSeconds(Runner, fireRate);
                }
            }
        }

        /// <summary>
        /// Client-side interpolation to network state
        /// </summary>
        private void InterpolateToNetworkState()
        {
            if (characterController != null)
                characterController.enabled = false;

            transform.position = Vector3.Lerp(transform.position, NetPosition, 20f * Runner.DeltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, NetRotation, 20f * Runner.DeltaTime);

            if (characterController != null)
                characterController.enabled = true;
        }
        #endregion

        #region Update
        private void Update()
        {
            if (Object == null) return;

            // Update head rotation
            if (headTransform != null)
                headTransform.localRotation = Quaternion.Euler(VerticalRotation, 0, 0);

            // Update health bar
            if (healthBar != null)
                healthBar.fillAmount = (float)Health / maxHealth;

            // Update respawn countdown
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
        #endregion

        #region Combat
        /// <summary>
        /// Fire a projectile from the pool
        /// </summary>
        private void Fire(Vector3 position, Vector3 direction)
        {
            if (!Object.HasStateAuthority || IsDead || !CombatEnabled)
                return;

            var pool = NetworkProjectilePool.Instance;
            if (pool == null) return;

            var netObj = pool.GetPooledProjectile();
            if (netObj == null) return;

            var proj = netObj.GetComponent<Projectile>();
            if (proj != null)
            {
                proj.Fire(Object.InputAuthority, position, direction);
            }
        }

        /// <summary>
        /// Apply damage to this player (server-side only)
        /// </summary>
        public void ApplyDamageServerSide(int dmg, PlayerRef attackerRef)
        {
            if (!Object.HasStateAuthority)
                return;

            if (IsDead || !CombatEnabled)
                return;

            Health -= dmg;
            RPC_UpdateHealthOnAllClients(Health);

            if (Health <= 0)
            {
                Health = 0;
                IsDead = true;
                Deaths++;

                // Find attacker and award kill
                PlayerController attackerPC = null;

                if (Runner.TryGetPlayerObject(attackerRef, out NetworkObject attackerNetObj))
                {
                    attackerPC = attackerNetObj.GetComponent<PlayerController>();
                }

                if (attackerPC == null)
                {
                    var allPlayers = FindObjectsOfType<PlayerController>();
                    foreach (var p in allPlayers)
                    {
                        if (p.Object != null && p.Object.InputAuthority == attackerRef)
                        {
                            attackerPC = p;
                            break;
                        }
                    }
                }

                // Award kill to attacker
                if (attackerPC != null && attackerPC != this && attackerPC.Object != null)
                {
                    attackerPC.Kills++;
                    attackerPC.RPC_UpdateScoreOnAllClients(attackerPC.PlayerName, attackerPC.Kills);
                }

                RPC_OnPlayerDied();
                RespawnTimer = TickTimer.CreateFromSeconds(Runner, 3f);
            }
        }
        #endregion

        #region RPCs
        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_StartMatchOnAllClients()
        {
            Events.RaiseMatchStart();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_UpdateScoreOnAllClients(NetworkString<_16> pName, int kills)
        {
            string playerNameStr = pName.ToString();
            Events.RaiseUpdateScore(playerNameStr, kills);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_UpdateHealthOnAllClients(int newHealth)
        {
            Health = newHealth;
            if (healthBar != null)
                healthBar.fillAmount = (float)Health / maxHealth;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_OnPlayerDied()
        {
            RPC_SetVisible(false);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_SetVisible(bool visible)
        {
            if (renderers != null)
            {
                foreach (var r in renderers)
                {
                    if (r != null)
                        r.enabled = visible;
                }
            }

            if (colliders != null)
            {
                foreach (var c in colliders)
                {
                    if (c != null && !(c is CharacterController))
                        c.enabled = visible;
                }
            }

            if (characterController != null)
                characterController.enabled = true;

            if (playerCanvas != null)
                playerCanvas.enabled = visible;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_UpdateHealthBar()
        {
            if (healthBar != null)
                healthBar.fillAmount = (float)Health / maxHealth;

            if (respawnCountdownText != null)
                respawnCountdownText.gameObject.SetActive(false);
        }
        #endregion

        #region Respawn
        /// <summary>
        /// Respawn player at random spawn point
        /// </summary>
        private void Respawn()
        {
            if (!Object.HasStateAuthority) return;

            Health = maxHealth;
            IsDead = false;
            VerticalRotation = 0f;
            RespawnTimer = TickTimer.None;

            var spawnPoints = FindObjectsOfType<SpawnPoint>();
            if (spawnPoints.Length > 0)
            {
                var sp = spawnPoints[Random.Range(0, spawnPoints.Length)];

                if (characterController != null)
                    characterController.enabled = false;

                transform.position = sp.transform.position;

                Vector3 dirToCenter = (Vector3.zero - transform.position);
                dirToCenter.y = 0;
                if (dirToCenter.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(dirToCenter.normalized, Vector3.up);

                if (headTransform != null)
                    headTransform.localRotation = Quaternion.identity;

                if (characterController != null)
                    characterController.enabled = true;
            }

            NetPosition = transform.position;
            NetRotation = transform.rotation;
            lastSyncedPosition = transform.position;
            lastSyncedRotation = transform.rotation;
            lastSyncTime = Time.time;

            RPC_SetVisible(true);
            RPC_UpdateHealthBar();
        }
        #endregion

        #region Public Methods
        /// <summary>
        /// Enable or disable combat for this player (server-side only)
        /// </summary>
        public void EnableCombat(bool enable)
        {
            if (Object.HasStateAuthority)
            {
                CombatEnabled = enable;
            }
        }
        #endregion

        private void OnDisable()
        {
            if (respawnCountdownText != null)
                respawnCountdownText.gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Network input data structure for player controls
    /// </summary>
    public struct NetworkInputData : INetworkInput
    {
        public Vector2 moveInput;
        public Vector2 lookInput;
        public bool fire;
        public float verticalMove;
    }
}