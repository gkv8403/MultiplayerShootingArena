using UnityEngine;
using Fusion;
using UnityEngine.UI;

namespace Scripts.Gameplay
{
    /// ===== FIXED: First-person camera with raycast aiming =====
    public class CameraController : MonoBehaviour
    {
        [Header("Camera Settings")]
        public Transform target;
        public float distance = 0.1f;  // Close to player for FPS feel
        public float height = 0.6f;    // Eye level height
        public float smoothSpeed = 8f;

        [Header("Aiming")]
        public Transform firePoint;
        public float raycastDistance = 100f;
        public Image crosshair;
        public Color hitColor = Color.red;
        public Color normalColor = Color.white;

        private Vector3 offset;
        private Vector3 desiredPosition;
        private NetworkObject networkObject;
        private Camera mainCam;

        private void Start()
        {
            networkObject = GetComponent<NetworkObject>();
            mainCam = GetComponent<Camera>();
            if (mainCam == null)
                mainCam = Camera.main;

            offset = new Vector3(0, height, distance);

            Debug.Log("[CameraController] Started - FPS mode enabled");
        }

        private void LateUpdate()
        {
            // ===== Only update for input authority (local player) =====
            if (networkObject != null && !networkObject.HasInputAuthority)
                return;

            if (target == null)
                return;

            // ===== UPDATE CAMERA POSITION (First-person style, close to player) =====
            desiredPosition = target.position + target.TransformDirection(offset);

            transform.position = Vector3.Lerp(
                transform.position,
                desiredPosition,
                smoothSpeed * Time.deltaTime
            );

            // ===== CAMERA LOOKS FORWARD (aligned with player direction) =====
            Vector3 lookForward = target.position + target.forward * 10f;
            transform.LookAt(lookForward);

            // ===== UPDATE CROSSHAIR WITH RAYCAST =====
            UpdateCrosshair();
        }

        private void UpdateCrosshair()
        {
            if (crosshair == null || firePoint == null)
                return;

            // ===== RAYCAST FROM FIREPOINT =====
            Ray ray = new Ray(firePoint.position, firePoint.forward);
            RaycastHit hit;

            bool hitSomething = Physics.Raycast(ray, out hit, raycastDistance);

            if (hitSomething)
            {
                // ===== HIT SOMETHING - MOVE CROSSHAIR TO HIT POINT =====
                Vector3 hitWorldPos = hit.point;
                Vector3 crosshairScreenPos = mainCam.WorldToScreenPoint(hitWorldPos);

                // Update crosshair position
                RectTransform crosshairRect = crosshair.GetComponent<RectTransform>();
                if (crosshairRect != null)
                {
                    crosshairRect.position = crosshairScreenPos;
                }

                // Check if hit is a player
                PlayerController playerHit = hit.collider.GetComponent<PlayerController>();
                if (playerHit != null)
                {
                    crosshair.color = hitColor;  // Red for enemy
                }
                else
                {
                    crosshair.color = normalColor;  // White for environment
                }

                crosshair.enabled = true;

                // Debug line
                Debug.DrawLine(firePoint.position, hit.point, Color.red);
            }
            else
            {
                // ===== HIT NOTHING - MOVE CROSSHAIR TO MAX DISTANCE =====
                Vector3 maxDistancePoint = firePoint.position + firePoint.forward * raycastDistance;
                Vector3 crosshairScreenPos = mainCam.WorldToScreenPoint(maxDistancePoint);

                RectTransform crosshairRect = crosshair.GetComponent<RectTransform>();
                if (crosshairRect != null)
                {
                    crosshairRect.position = crosshairScreenPos;
                }

                crosshair.color = normalColor;  // White
                crosshair.enabled = true;

                // Debug line
                Debug.DrawLine(firePoint.position, maxDistancePoint, Color.white);
            }
        }
    }
}