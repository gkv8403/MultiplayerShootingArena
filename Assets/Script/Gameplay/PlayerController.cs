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
        private int lastBroadcastedKills = -1;

        public override void Spawned()
        {
            Debug.Log($"[Player] ===== PLAYER SPAWNED =====");
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
                Debug.Log("[Player] Initializing as SERVER");
                Health = maxHealth;
                Kills = 0;
                Deaths = 0;
                VerticalRotation = 0f;
                IsDead = false;
                CombatEnabled = false;
                PlayerName = PlayerPrefs.GetString("PlayerName", $"Player{Object.InputAuthority.PlayerId}");

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

                lastBroadcastedKills = 0;
                Invoke(nameof(BroadcastInitialScore), 0.5f);
            }

            lastSyncedPosition = transform.position;
            lastSyncedRotation = transform.rotation;
            lastSyncTime = Time.time;

            if (nameText != null)
                nameText.text = PlayerName.ToString();

            if (healthBar != null)
                healthBar.fillAmount = 1f;

            Debug.Log($"[Player] ===== SPAWN COMPLETE =====");
        }

        private void BroadcastInitialScore()
        {
            if (Object.HasStateAuthority)
            {
                Debug.Log($"[Player] 📊 Broadcasting initial score for {PlayerName}");
                RPC_BroadcastScore(PlayerName, Kills);
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

            // Update health bar continuously
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
            if (!Object.HasStateAuthority || IsDead || !CombatEnabled) return;

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
                Debug.Log($"[Player] 🔫 {PlayerName} firing projectile from {position} dir {direction}");
                proj.Fire(Object.InputAuthority, position, direction);
                RPC_OnFireVisual();
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_OnFireVisual()
        {
            Debug.Log($"[Player] 🎬 Fire visual for {PlayerName}");
        }

        // ✅ SERVER-SIDE ONLY damage method called directly by Projectile
        public void ApplyDamageServerSide(int dmg, PlayerRef attacker)
        {
            // CRITICAL: This MUST only be called on server
            if (!Object.HasStateAuthority)
            {
                Debug.LogError($"[Player] ❌❌❌ ApplyDamageServerSide called on CLIENT! This is wrong!");
                return;
            }

            if (IsDead)
            {
                Debug.Log($"[Player] ⚠️ {PlayerName} already dead");
                return;
            }

            Debug.Log($"[Player] 💥💥💥 SERVER: {PlayerName} taking {dmg} damage from {attacker}");

            Health -= dmg;

            // Sync health to all clients
            RPC_UpdateHealthVisual(Health);

            Debug.Log($"[Player] ❤️ SERVER: {PlayerName} health now {Health}/{maxHealth}");

            if (Health <= 0)
            {
                Health = 0;
                IsDead = true;
                Deaths++;

                Debug.Log($"[Player] 💀💀💀 SERVER: {PlayerName} DIED!");

                // Award kill
                if (attacker != PlayerRef.None && Runner.TryGetPlayerObject(attacker, out NetworkObject attackerObj))
                {
                    var attackerPC = attackerObj.GetComponent<PlayerController>();
                    if (attackerPC != null && attackerPC != this)
                    {
                        attackerPC.Kills++;
                        int newKills = attackerPC.Kills;

                        Debug.Log($"[Player] 🎉🏆🎉 SERVER: {attackerPC.PlayerName} GOT KILL #{newKills}!");

                        // Broadcast score immediately
                        if (attackerPC.lastBroadcastedKills != newKills)
                        {
                            attackerPC.lastBroadcastedKills = newKills;
                            attackerPC.RPC_BroadcastScore(attackerPC.PlayerName, newKills);
                            Events.RaiseUpdateScore(attackerPC.PlayerName.ToString(), newKills);

                            Debug.Log($"[Player] 📢📢📢 Broadcasted score: {attackerPC.PlayerName} = {newKills}");
                        }
                    }
                }

                RPC_OnPlayerDied();
                RespawnTimer = TickTimer.CreateFromSeconds(Runner, 3f);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_UpdateHealthVisual(int newHealth)
        {
            Health = newHealth;
            if (healthBar != null)
                healthBar.fillAmount = (float)Health / maxHealth;
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_OnPlayerDied()
        {
            Debug.Log($"[Player] 💀 CLIENT: {PlayerName} died (visual)");
            RPC_SetVisible(false);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_BroadcastScore(NetworkString<_16> pName, int kills)
        {
            string playerNameStr = pName.ToString();
            Debug.Log($"[Player] 📊 RPC_BroadcastScore: {playerNameStr} = {kills}");
            Events.RaiseUpdateScore(playerNameStr, kills);
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

            Debug.Log($"[Player] 👁️ {PlayerName} visibility: {visible}");
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

            GUI.color = Color.green;
            GUILayout.BeginArea(new Rect(Screen.width - 300, 10, 290, 140));
            GUILayout.Label($"[{PlayerName}]");
            GUILayout.Label($"Kills: {Kills} | Deaths: {Deaths}");
            GUILayout.Label($"Health: {Health}/{maxHealth}");
            GUILayout.Label($"Combat: {CombatEnabled}");
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