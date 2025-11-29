using UnityEngine;
using UnityEngine.UI;

namespace Scripts.Gameplay
{
    public class CameraController : MonoBehaviour
    {
        [Header("Camera Settings")]
        public Transform target;
        public float distance = 2f;
        public float height = 1.2f;
        public float smoothSpeed = 8f;

        [Header("Aiming")]
        public Transform firePoint;
        public float raycastDistance = 100f;
        public Image crosshair;
        public Color hitColor = Color.red;
        public Color normalColor = Color.white;

        private Vector3 offset;
        private Vector3 desiredPosition;
        private Camera mainCam;
        public PlayerController player;
        private bool targetAssigned = false;

        private void Start()
        {
            mainCam = GetComponent<Camera>();
            if (mainCam == null)
                mainCam = Camera.main;

            Debug.Log("[CameraController] Started - Waiting for player to spawn...");
        }

        private void LateUpdate()
        {
            // ===== AUTO-FIND TARGET IF NOT ASSIGNED =====
            if (!targetAssigned)
            {
                FindLocalPlayer();
                if (target == null)
                    return;
                targetAssigned = true;
            }

            if (target == null)
                return;

            // ===== UPDATE CAMERA POSITION (Third-person, behind player) =====
            float horizontal = target.eulerAngles.y * Mathf.Deg2Rad;
            offset = new Vector3(
                Mathf.Sin(horizontal) * distance,
                height,
                Mathf.Cos(horizontal) * distance
            );

            desiredPosition = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

            // ===== CAMERA LOOKS AT PLAYER =====
            Vector3 lookTarget = target.position + Vector3.up * 0.6f;
            transform.LookAt(lookTarget);

            UpdateCrosshair();
        }

        // ===== AUTO-FIND LOCAL PLAYER =====
        private void FindLocalPlayer()
        {
            var allPlayers = FindObjectsOfType<PlayerController>();

            foreach (var p in allPlayers)
            {
                if (p.Object.HasInputAuthority)
                {
                    target = p.transform;
                    player = p;

                    // Also find firePoint
                    Transform head = p.transform.Find("Head");
                    if (head != null)
                    {
                        firePoint = head.Find("FirePoint");
                        Debug.Log($"[CameraController] Found local player: {p.gameObject.name}, firePoint: {firePoint.name}");
                    }

                    return;
                }
            }
        }

        private void UpdateCrosshair()
        {
            if (crosshair == null)
                return;

            // Find firePoint from player if not already assigned
            if (firePoint == null && player != null)
            {
                Transform head = player.transform.Find("Head");
                if (head != null)
                {
                    firePoint = head.Find("FirePoint");
                    Debug.Log($"[CameraController] Found firePoint: {firePoint.name}");
                }
            }

            if (firePoint == null)
                return;

            Ray ray = new Ray(firePoint.position, firePoint.forward);
            RaycastHit hit;

            bool hitSomething = Physics.Raycast(ray, out hit, raycastDistance);

            if (hitSomething)
            {
                Vector3 hitWorldPos = hit.point;
                Vector3 crosshairScreenPos = mainCam.WorldToScreenPoint(hitWorldPos);

                RectTransform crosshairRect = crosshair.GetComponent<RectTransform>();
                if (crosshairRect != null)
                    crosshairRect.position = crosshairScreenPos;

                PlayerController playerHit = hit.collider.GetComponent<PlayerController>();
                crosshair.color = playerHit != null ? hitColor : normalColor;
                crosshair.enabled = true;

                Debug.DrawLine(firePoint.position, hit.point, Color.red);
            }
            else
            {
                Vector3 maxDistancePoint = firePoint.position + firePoint.forward * raycastDistance;
                Vector3 crosshairScreenPos = mainCam.WorldToScreenPoint(maxDistancePoint);

                RectTransform crosshairRect = crosshair.GetComponent<RectTransform>();
                if (crosshairRect != null)
                    crosshairRect.position = crosshairScreenPos;

                crosshair.color = normalColor;
                crosshair.enabled = true;

                Debug.DrawLine(firePoint.position, maxDistancePoint, Color.white);
            }
        }
    }
}