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

    private Camera mainCam;
    private Scripts.Gameplay.PlayerController localPlayer;
    private bool targetAssigned = false;

    private void Start()
    {
        mainCam = GetComponent<Camera>() ?? Camera.main;
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

        // follow (basic third-person camera)
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
            if (p.Object.HasInputAuthority)
            {
                target = p.transform;
                localPlayer = p;
                firePoint = p.firePoint;
                break;
            }
        }
    }

    private void UpdateCrosshair()
    {
        if (crosshair == null || mainCam == null) return;

        Ray ray = mainCam.ScreenPointToRay(new Vector3(Screen.width / 2f, Screen.height / 2f, 0f));
        if (Physics.Raycast(ray, out RaycastHit hit, raycastDistance))
        {
            crosshair.rectTransform.anchoredPosition = Vector2.zero; // keep centered for center-screen crosshair
            crosshair.color = Color.red;
        }
        else
        {
            crosshair.color = Color.white;
        }
    }
}
