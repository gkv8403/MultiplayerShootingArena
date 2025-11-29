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

        private Renderer[] renderers;
        private Collider[] colliders;

        // FIX 1: Remove client-side prediction variables
        // We'll use pure server authority with interpolation

        public override void Spawned()
        {
            Debug.Log($"[Player] ===== PLAYER SPAWNED =====");
            Debug.Log($"[Player] Object ID: {Object.Id}");
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

            // FIX 2: Initialize on State Authority
            if (Object.HasStateAuthority)
            {
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
                    {
                        transform.rotation = Quaternion.LookRotation(dirToCenter.normalized, Vector3.up);
                    }
                    characterController.enabled = true;
                }

                // FIX 3: Initialize networked transform immediately
                NetPosition = transform.position;
                NetRotation = transform.rotation;
            }

            if (nameText != null)
                nameText.text = PlayerName.ToString();

            if (healthBar != null)
                healthBar.fillAmount = 1f;

            if (Object.HasStateAuthority)
            {
                RPC_BroadcastScore(PlayerName.ToString(), Kills);
            }

            Debug.Log($"[Player] ===== SPAWN COMPLETE =====");
        }

        public override void FixedUpdateNetwork()
        {
            // FIX 4: Only server handles game logic, clients only interpolate
            if (Object.HasStateAuthority)
            {
                HandleServerAuthority();
            }
            else
            {
                // FIX 5: All non-authority players (including input authority) just interpolate
                InterpolateToNetworkState();
            }
        }

        private void HandleServerAuthority()
        {
            // Handle respawn
            if (IsDead && RespawnTimer.Expired(Runner))
            {
                Respawn();
                return;
            }

            // Process input
            if (GetInput(out NetworkInputData data))
            {
                if (!IsDead && characterController != null && characterController.enabled)
                {
                    // Movement
                    Vector3 move = new Vector3(data.moveInput.x, 0, data.moveInput.y);
                    move = transform.TransformDirection(move);
                    move.y = 0;

                    characterController.Move(move * moveSpeed * Runner.DeltaTime);

                    // FIX 6: Always update NetPosition after movement
                    NetPosition = transform.position;

                    // Rotation
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

                    // Fire
                    if (data.fire && CombatEnabled && !IsDead)
                    {
                        if (fireTimer.ExpiredOrNotRunning(Runner))
                        {
                            // FIX 7: Use actual firePoint position and forward
                            Vector3 firePos = firePoint != null ? firePoint.position : (transform.position + Vector3.up * 1.6f);
                            Vector3 fireDir = firePoint != null ? firePoint.forward : transform.forward;

                            Fire(firePos, fireDir);
                            fireTimer = TickTimer.CreateFromSeconds(Runner, fireRate);
                        }
                    }
                }
            }
        }

        // FIX 8: Smooth interpolation for all remote players
        private void InterpolateToNetworkState()
        {
            if (characterController != null)
            {
                // Disable CharacterController for interpolation
                characterController.enabled = false;
            }

            // Smooth position interpolation
            transform.position = Vector3.Lerp(transform.position, NetPosition, 20f * Runner.DeltaTime);

            // Smooth rotation interpolation
            transform.rotation = Quaternion.Slerp(transform.rotation, NetRotation, 20f * Runner.DeltaTime);

            if (characterController != null)
            {
                characterController.enabled = true;
            }
        }

        private void Update()
        {
            if (Object == null) return;

            // Update head rotation
            if (headTransform != null)
            {
                headTransform.localRotation = Quaternion.Euler(VerticalRotation, 0, 0);
            }

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
                Debug.Log($"[Player] {PlayerName} fired from {position} dir {direction}");
                RPC_OnFireVisual();
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_OnFireVisual()
        {
            Debug.Log($"[Player] Fire visual for {PlayerName}");
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_TakeDamage(int dmg, PlayerRef attacker)
        {
            if (IsDead) return;

            Health -= dmg;

            if (healthBar != null)
                healthBar.fillAmount = (float)Health / maxHealth;

            Debug.Log($"[Player] {PlayerName} took {dmg} damage. Health: {Health}");

            if (Health <= 0 && Object.HasStateAuthority)
            {
                Health = 0;
                IsDead = true;
                Deaths++;

                RPC_SetVisible(false);

                if (Runner.TryGetPlayerObject(attacker, out NetworkObject attackerObj))
                {
                    var attackerPC = attackerObj.GetComponent<PlayerController>();
                    if (attackerPC != null && attackerPC != this)
                    {
                        attackerPC.Kills++;
                        // FIX 9: Broadcast score update
                        attackerPC.RPC_BroadcastScore(attackerPC.PlayerName.ToString(), attackerPC.Kills);
                    }
                }

                RespawnTimer = TickTimer.CreateFromSeconds(Runner, 2f);
            }
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        public void RPC_BroadcastScore(NetworkString<_16> pName, int kills)
        {
            // FIX 10: Ensure event is raised on all clients
            Events.RaiseUpdateScore(pName.ToString(), kills);
            Debug.Log($"[Player] Score broadcast received: {pName} - {kills} kills");
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
                {
                    transform.rotation = Quaternion.LookRotation(dirToCenter.normalized, Vector3.up);
                }

                if (headTransform != null)
                    headTransform.localRotation = Quaternion.identity;

                if (characterController != null)
                    characterController.enabled = true;
            }

            NetPosition = transform.position;
            NetRotation = transform.rotation;

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