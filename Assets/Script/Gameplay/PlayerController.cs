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
        [Networked] public bool CombatEnabled { get; set; }

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

        [Header("Network Optimization")]
        public float positionSyncThreshold = 0.1f;
        public float minSyncInterval = 0.05f;
        public float rotationSyncThreshold = 5f;

        private Renderer[] renderers;
        private Collider[] colliders;
        private Vector3 lastSyncedPosition;
        private Quaternion lastSyncedRotation;
        private float lastSyncTime;

        public override void Spawned()
        {
            Debug.Log($"[Player] ===== SPAWNED =====");
            Debug.Log($"[Player] PlayerRef: {Object.InputAuthority}");
            Debug.Log($"[Player] HasInputAuthority: {Object.HasInputAuthority}");
            Debug.Log($"[Player] HasStateAuthority: {Object.HasStateAuthority}");

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

            headTransform = transform.Find("Head");
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
                    firePoint = fp;
                else
                {
                    GameObject fpObj = new GameObject("FirePoint");
                    fpObj.transform.SetParent(transform);
                    fpObj.transform.localPosition = new Vector3(0, 1.6f, 0.5f);
                    firePoint = fpObj.transform;
                }
            }

            renderers = GetComponentsInChildren<Renderer>(true);
            colliders = GetComponentsInChildren<Collider>(true);

            if (respawnCountdownText != null)
                respawnCountdownText.gameObject.SetActive(false);

            if (Object.HasStateAuthority)
            {
                Health = maxHealth;
                Kills = 0;
                Deaths = 0;
                VerticalRotation = 0f;
                IsDead = false;
                CombatEnabled = false;
                PlayerName = PlayerPrefs.GetString("PlayerName", $"Player{Object.InputAuthority.PlayerId}");

                Debug.Log($"[Player] ✓ SERVER: Initialized {PlayerName} (PlayerRef: {Object.InputAuthority})");

                var sps = FindObjectsOfType<SpawnPoint>();
                if (sps.Length > 0)
                {
                    var sp = sps[Random.Range(0, sps.Length)];
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

                Invoke(nameof(BroadcastInitialScore), 2f);
            }

            lastSyncedPosition = transform.position;
            lastSyncedRotation = transform.rotation;
            lastSyncTime = Time.time;

            if (nameText != null)
                nameText.text = PlayerName.ToString();

            if (healthBar != null)
                healthBar.fillAmount = 1f;
        }

        private void BroadcastInitialScore()
        {
            if (Object.HasStateAuthority)
            {
                Debug.Log($"[Player] 📢 SERVER: Broadcasting initial score for {PlayerName}");
                RPC_UpdateScoreOnAllClients(PlayerName, 0);
            }
        }

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

        private void HandleServerAuthority()
        {
            if (IsDead && RespawnTimer.Expired(Runner))
            {
                Respawn();
                return;
            }

            if (GetInput(out NetworkInputData data))
            {
                if (!IsDead && characterController != null && characterController.enabled)
                {
                    Vector3 move = new Vector3(data.moveInput.x, data.verticalMove, data.moveInput.y);
                    move = transform.TransformDirection(move);
                    characterController.Move(move * moveSpeed * Runner.DeltaTime);

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

                    if (Mathf.Abs(data.lookInput.x) > 0.01f)
                    {
                        float yaw = data.lookInput.x * rotationSpeed * Runner.DeltaTime;
                        transform.Rotate(0, yaw, 0, Space.World);
                    }

                    if (Mathf.Abs(data.lookInput.y) > 0.01f)
                    {
                        VerticalRotation -= data.lookInput.y * verticalLookSpeed * Runner.DeltaTime;
                        VerticalRotation = Mathf.Clamp(VerticalRotation, -80f, 80f);
                    }

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
            }
        }

        private void InterpolateToNetworkState()
        {
            if (characterController != null)
                characterController.enabled = false;

            transform.position = Vector3.Lerp(transform.position, NetPosition, 20f * Runner.DeltaTime);
            transform.rotation = Quaternion.Slerp(transform.rotation, NetRotation, 20f * Runner.DeltaTime);

            if (characterController != null)
                characterController.enabled = true;
        }

        private void Update()
        {
            if (Object == null) return;

            if (headTransform != null)
                headTransform.localRotation = Quaternion.Euler(VerticalRotation, 0, 0);

            if (healthBar != null)
                healthBar.fillAmount = (float)Health / maxHealth;

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

        private void Fire(Vector3 position, Vector3 direction)
        {
            if (!Object.HasStateAuthority || IsDead || !CombatEnabled)
            {
                if (!CombatEnabled)
                    Debug.LogWarning($"[Player] ⚠️ {PlayerName} tried to fire but combat disabled!");
                return;
            }

            var pool = NetworkProjectilePool.Instance;
            if (pool == null)
            {
                Debug.LogWarning("[Player] ⚠️ Pool not found");
                return;
            }

            var no = pool.GetPooledProjectile();
            if (no == null)
            {
                Debug.LogWarning("[Player] ⚠️ No projectile available");
                return;
            }

            var proj = no.GetComponent<Projectile>();
            if (proj != null)
            {
                Debug.Log($"[Player] 🔫 SERVER: {PlayerName} (Player{Object.InputAuthority.PlayerId}) firing");
                proj.Fire(Object.InputAuthority, position, direction);
            }
        }

        // ✅ CRITICAL FIX: Completely rewritten damage logic
        public void ApplyDamageServerSide(int dmg, PlayerRef attackerRef)
        {
            if (!Object.HasStateAuthority)
            {
                Debug.LogError("[Player] ❌ ApplyDamageServerSide called on CLIENT!");
                return;
            }

            if (IsDead || !CombatEnabled)
            {
                Debug.Log($"[Player] ⚠️ Ignoring damage - Dead:{IsDead}, Combat:{CombatEnabled}");
                return;
            }

            Debug.Log($"[Player] ========================================");
            Debug.Log($"[Player] 💥 DAMAGE EVENT");
            Debug.Log($"[Player] Victim: {PlayerName} (Player{Object.InputAuthority.PlayerId})");
            Debug.Log($"[Player] Attacker PlayerRef: {attackerRef}");
            Debug.Log($"[Player] Damage: {dmg}");
            Debug.Log($"[Player] Current Health: {Health}");
            Debug.Log($"[Player] ========================================");

            Health -= dmg;
            RPC_UpdateHealthOnAllClients(Health);

            if (Health <= 0)
            {
                Health = 0;
                IsDead = true;
                Deaths++;

                Debug.Log($"[Player] 💀 {PlayerName} DIED!");

                // ✅ CRITICAL FIX: Find attacker player properly
                PlayerController attackerPC = null;

                // Method 1: Try to get player object directly
                if (Runner.TryGetPlayerObject(attackerRef, out NetworkObject attackerNetObj))
                {
                    attackerPC = attackerNetObj.GetComponent<PlayerController>();
                    Debug.Log($"[Player] ✓ Found attacker via TryGetPlayerObject: {attackerPC?.PlayerName}");
                }

                // Method 2: If that fails, search all players
                if (attackerPC == null)
                {
                    Debug.LogWarning("[Player] TryGetPlayerObject failed, searching manually...");
                    var allPlayers = FindObjectsOfType<PlayerController>();
                    foreach (var p in allPlayers)
                    {
                        if (p.Object != null && p.Object.InputAuthority == attackerRef)
                        {
                            attackerPC = p;
                            Debug.Log($"[Player] ✓ Found attacker via manual search: {attackerPC.PlayerName}");
                            break;
                        }
                    }
                }

                // Award kill
                if (attackerPC != null && attackerPC != this && attackerPC.Object != null)
                {
                    int oldKills = attackerPC.Kills;
                    attackerPC.Kills++;

                    Debug.Log($"[Player] ========================================");
                    Debug.Log($"[Player] 🎉 KILL AWARDED!");
                    Debug.Log($"[Player] Killer: {attackerPC.PlayerName} (Player{attackerPC.Object.InputAuthority.PlayerId})");
                    Debug.Log($"[Player] Kills: {oldKills} → {attackerPC.Kills}");
                    Debug.Log($"[Player] ========================================");

                    // Broadcast immediately
                    attackerPC.RPC_UpdateScoreOnAllClients(attackerPC.PlayerName, attackerPC.Kills);
                }
                else
                {
                    Debug.LogError($"[Player] ❌ FAILED TO FIND ATTACKER!");
                    Debug.LogError($"[Player] AttackerRef: {attackerRef}");
                    Debug.LogError($"[Player] AttackerRef.PlayerId: {attackerRef.PlayerId}");
                    Debug.LogError($"[Player] AttackerPC null: {attackerPC == null}");
                    if (attackerPC != null)
                    {
                        Debug.LogError($"[Player] Same player: {attackerPC == this}");
                        Debug.LogError($"[Player] Object null: {attackerPC.Object == null}");
                    }
                }

                RPC_OnPlayerDied();
                RespawnTimer = TickTimer.CreateFromSeconds(Runner, 3f);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_StartMatchOnAllClients()
        {
            Debug.Log("[Player] 🎮 RPC: Starting match on all clients!");
            Events.RaiseMatchStart();
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_UpdateScoreOnAllClients(NetworkString<_16> pName, int kills)
        {
            string playerNameStr = pName.ToString();
            Debug.Log($"[Player] ==========================================");
            Debug.Log($"[Player] 📊 RPC_UpdateScoreOnAllClients RECEIVED");
            Debug.Log($"[Player] Player: {playerNameStr}");
            Debug.Log($"[Player] Kills: {kills}");
            Debug.Log($"[Player] Is Server: {Object.HasStateAuthority}");
            Debug.Log($"[Player] Time: {Time.time:F2}");
            Debug.Log($"[Player] ==========================================");

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

        private void Respawn()
        {
            if (!Object.HasStateAuthority) return;

            Debug.Log($"[Player] ♻️ Respawning {PlayerName}");

            Health = maxHealth;
            IsDead = false;
            VerticalRotation = 0f;
            RespawnTimer = TickTimer.None;

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
                Debug.Log($"[Player] ⚔️ {PlayerName} combat: {enable}");
            }
        }

        private void OnDisable()
        {
            if (respawnCountdownText != null)
                respawnCountdownText.gameObject.SetActive(false);
        }

        private void OnGUI()
        {
            if (!Object.HasStateAuthority || !Debug.isDebugBuild) return;

            GUI.color = CombatEnabled ? Color.green : Color.yellow;
            GUILayout.BeginArea(new Rect(Screen.width - 300, 10, 290, 160));
            GUILayout.Label($"=== {PlayerName} (SERVER) ===");
            GUILayout.Label($"PlayerRef: {Object.InputAuthority.PlayerId}");
            GUILayout.Label($"Kills: {Kills} | Deaths: {Deaths}");
            GUILayout.Label($"Health: {Health}/{maxHealth}");
            GUILayout.Label($"Combat: {CombatEnabled}");
            GUILayout.Label($"Dead: {IsDead}");
            GUILayout.EndArea();
        }
    }

    public struct NetworkInputData : INetworkInput
    {
        public Vector2 moveInput;
        public Vector2 lookInput;
        public bool fire;
        public float verticalMove;
    }
}