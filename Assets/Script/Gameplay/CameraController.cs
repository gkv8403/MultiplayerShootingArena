using UnityEngine;
using UnityEngine.UI;

namespace Scripts.Gameplay
{
    public class CameraController : MonoBehaviour
    {
        [Header("Camera Settings")]
        public Transform target;
        public float distance = 4f;
        public float height = 1.8f;
        public float smoothSpeed = 8f;

        [Header("Aiming")]
        public Transform firePoint;
        public float raycastDistance = 100f;
        public Image crosshair;
        public Color hitColor = Color.red;
        public Color normalColor = Color.white;

        private Camera mainCam;
        public PlayerController player;
        private bool targetAssigned = false;

        private void Start()
        {
            mainCam = GetComponent<Camera>();
            if (mainCam == null)
                mainCam = Camera.main;

            Debug.Log("[CameraController] Started");
        }

        private void LateUpdate()
        {
            if (!targetAssigned)
            {
                FindLocalPlayer();
                if (target == null) return;
                targetAssigned = true;
            }

            if (target == null) return;

            UpdateCameraPosition();
            UpdateCrosshair();
        }

        private void FindLocalPlayer()
        {
            var allPlayers = FindObjectsOfType<PlayerController>();

            foreach (var p in allPlayers)
            {
                if (p.Object.HasInputAuthority)
                {
                    target = p.transform;
                    player = p;

                    // Find firePoint
                    if (p.headTransform != null)
                    {
                        firePoint = p.headTransform.Find("FirePoint");
                        Debug.Log($"[CameraController] ✓ Found local player and firePoint");
                    }
                    return;
                }
            }
        }

        private void UpdateCameraPosition()
        {
            // Position camera behind and above player
            Vector3 offset = -target.forward * distance + Vector3.up * height;
            Vector3 desiredPosition = target.position + offset;

            transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);

            // Look at point slightly above player's position
            Vector3 lookPoint = target.position + Vector3.up * 1.5f;

            // If we have head, look at head for better vertical aim
            if (player != null && player.headTransform != null)
            {
                lookPoint = player.headTransform.position;
            }

            transform.LookAt(lookPoint);
        }

        private void UpdateCrosshair()
        {
            if (crosshair == null) return;

            // Get firePoint if we don't have it
            if (firePoint == null && player != null && player.headTransform != null)
            {
                firePoint = player.transform.Find("FirePoint");
            }

            if (firePoint == null)
            {
                crosshair.enabled = false;
                return; 
            }

            // Raycast from firePoint forward
            Ray ray = new Ray(firePoint.position, firePoint.forward);
            RaycastHit hit;

            Vector3 targetPoint;
            bool hitSomething = Physics.Raycast(ray, out hit, raycastDistance);

            if (hitSomething)
            {
                targetPoint = hit.point;

                // Check if aiming at player
                var hitPlayer = hit.collider.GetComponent<PlayerController>();
                crosshair.color = (hitPlayer != null && hitPlayer != player) ? hitColor : normalColor;

                Debug.DrawLine(firePoint.position, hit.point, Color.red);
            }
            else
            {
                targetPoint = firePoint.position + firePoint.forward * raycastDistance;
                crosshair.color = normalColor;
                Debug.DrawLine(firePoint.position, targetPoint, Color.white);
            }

            // Position crosshair on screen
            Vector3 screenPos = mainCam.WorldToScreenPoint(targetPoint);

            if (screenPos.z > 0)
            {
                RectTransform rect = crosshair.GetComponent<RectTransform>();
                if (rect != null) rect.position = screenPos;
                crosshair.enabled = true;
            }
            else
            {
                crosshair.enabled = false;
            }
        }
    }
}