using UnityEngine;

// Subscribes to UI events and stores the latest input snapshot consumed by NetworkManager.OnInput.
public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    // Current input snapshot
    public Vector2 CurrentMoveInput { get; private set; }
    public Vector2 CurrentLookDelta { get; private set; }
    public bool CurrentFire { get; private set; }

    // A simple friction/damping for look delta so multiple small drags don't overwhelm
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        Events.OnMoveInput += OnMove;
        Events.OnMoveInputStop += OnMoveStop;
        Events.OnLookDelta += OnLook;
        Events.OnFireDown += OnFireDown;
        Events.OnFireUp += OnFireUp;
    }

    private void OnDisable()
    {
        Events.OnMoveInput -= OnMove;
        Events.OnMoveInputStop -= OnMoveStop;
        Events.OnLookDelta -= OnLook;
        Events.OnFireDown -= OnFireDown;
        Events.OnFireUp -= OnFireUp;
    }

    private void Update()
    {
        // reduce look delta over time to make it a per-frame delta
        CurrentLookDelta = Vector2.Lerp(CurrentLookDelta, Vector2.zero, 10f * Time.deltaTime);
    }

    private void OnMove(Vector2 dir)
    {
        CurrentMoveInput += dir;
        CurrentMoveInput = Vector2.ClampMagnitude(CurrentMoveInput, 1f);
    }

    private void OnMoveStop()
    {
        CurrentMoveInput = Vector2.zero;
    }

    private void OnLook(Vector2 delta)
    {
        CurrentLookDelta += delta;
    }

    private void OnFireDown()
    {
        CurrentFire = true;
    }

    private void OnFireUp()
    {
        CurrentFire = false;
    }
}