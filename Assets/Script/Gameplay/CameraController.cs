using UnityEngine;
using UnityEngine.UI;

public class CameraController : MonoBehaviour
{
    public Transform target;
    public float distance = 4f;
    public float height = 1.8f;
    public float smoothSpeed = 8f;

    public Transform firePoint;
    public float raycastDistance = 100f;
    public Image crosshair;

    [Header("Crosshair Settings")]
    public Vector2 crosshairOffset = Vector2.zero; // Offset from center if needed
    public Color crosshairNormalColor = Color.white;
    public Color crosshairTargetColor = Color.red;

    private Camera mainCam;
    private Scripts.Gameplay.PlayerController localPlayer;
    private bool targetAssigned = false;
    private RectTransform crosshairRect;

    private void Start()
    {
        mainCam = GetComponent<Camera>() ?? Camera.main;

        if (crosshair != null)
        {
            crosshairRect = crosshair.GetComponent<RectTransform>();
            // Center crosshair on screen
            if (crosshairRect != null)
            {
                crosshairRect.anchorMin = new Vector2(0.5f, 0.5f);
                crosshairRect.anchorMax = new Vector2(0.5f, 0.5f);
                crosshairRect.pivot = new Vector2(0.5f, 0.5f);
                crosshairRect.anchoredPosition = crosshairOffset;
            }
        }
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

        // Follow target (third-person camera)
        Vector3 desiredPos = target.position - target.forward * distance + Vector3.up * height;
        transform.position = Vector3.Lerp(transform.position, desiredPos, smoothSpeed * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * 1.5f);

        UpdateCrosshair();
    }

    private void FindLocalPlayer()
    {
        var allPlayers = FindObjectsOfType<Scripts.Gameplay.PlayerController>();
        foreach (var p in allPlayers)
        {
            if (p.Object != null && p.Object.HasInputAuthority)
            {
                target = p.transform;
                localPlayer = p;
                firePoint = p.firePoint;
                Debug.Log($"[Camera] Found local player: {p.PlayerName}");
                break;
            }
        }

        if (target == null)
        {
            Debug.LogWarning("[Camera] No local player found yet");
        }
    }

    private void UpdateCrosshair()
    {
        if (crosshair == null || mainCam == null) return;

        // Raycast from camera center (where bullets will go)
        Ray ray = mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

        // Check if aiming at a player
        bool hitPlayer = false;
        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance))
        {
            // Check if hit a player (but not ourselves)
            var hitPlayerController = hit.collider.GetComponentInParent<Scripts.Gameplay.PlayerController>();
            if (hitPlayerController != null && hitPlayerController != localPlayer)
            {
                hitPlayer = true;
            }
        }

        // Update crosshair color based on target
        crosshair.color = hitPlayer ? crosshairTargetColor : crosshairNormalColor;

        // Optional: Scale crosshair slightly when over target
        if (crosshairRect != null)
        {
            float targetScale = hitPlayer ? 1.2f : 1f;
            crosshairRect.localScale = Vector3.Lerp(
                crosshairRect.localScale,
                Vector3.one * targetScale,
                10f * Time.deltaTime
            );
        }
    }

    // Debug helper
    private void OnDrawGizmos()
    {
        if (mainCam == null) return;

        // Draw ray from camera center
        Ray ray = mainCam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        Gizmos.color = Color.yellow;
        Gizmos.DrawRay(ray.origin, ray.direction * raycastDistance);
    }
}