using UnityEngine;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [Header("Input Mode")]
    public bool useKeyboardMouse = true;

    [Header("Mobile Touch Settings")]
    public float touchSensitivityX = 2f; // Horizontal sensitivity
    public float touchSensitivityY = 2f; // Vertical sensitivity
    public float touchSmoothing = 0.1f; // Lower = smoother but more delay

    public Vector2 CurrentMoveInput { get; private set; }
    public Vector2 CurrentLookDelta { get; private set; }
    public bool CurrentFire { get; private set; }
    public float CurrentVerticalMove { get; private set; }

    // For smooth mobile look
    private Vector2 rawTouchDelta = Vector2.zero;
    private Vector2 smoothedLookDelta = Vector2.zero;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Auto-detect platform
#if UNITY_STANDALONE || UNITY_EDITOR
        useKeyboardMouse = true;
#else
        useKeyboardMouse = false;
#endif

        Debug.Log($"[InputManager] Initialized - Keyboard/Mouse: {useKeyboardMouse}");
    }

    private void OnEnable()
    {
        Events.OnMoveInput += OnMove;
        Events.OnMoveInputStop += OnMoveStop;
        Events.OnLookDelta += OnLook;
        Events.OnFireDown += OnFireDown;
        Events.OnFireUp += OnFireUp;
        Events.OnVerticalMoveInput += OnVerticalMove;
        Events.OnVerticalMoveInputStop += OnVerticalMoveInputStop;
    }

    private void OnDisable()
    {
        Events.OnMoveInput -= OnMove;
        Events.OnMoveInputStop -= OnMoveStop;
        Events.OnLookDelta -= OnLook;
        Events.OnFireDown -= OnFireDown;
        Events.OnFireUp -= OnFireUp;
        Events.OnVerticalMoveInput -= OnVerticalMove;
        Events.OnVerticalMoveInputStop -= OnVerticalMoveInputStop;
    }

    private void Update()
    {
        if (useKeyboardMouse)
        {
            HandleKeyboardInput();
        }
        else
        {
            HandleMobileInput();
        }
    }

    private void HandleKeyboardInput()
    {
        // Horizontal movement (WASD)
        Vector2 moveInput = Vector2.zero;

        if (Input.GetKey(KeyCode.W)) moveInput.y += 1;
        if (Input.GetKey(KeyCode.S)) moveInput.y -= 1;
        if (Input.GetKey(KeyCode.A)) moveInput.x -= 1;
        if (Input.GetKey(KeyCode.D)) moveInput.x += 1;

        CurrentMoveInput = Vector2.ClampMagnitude(moveInput, 1f);

        // Vertical movement (Q/E for up/down)
        float verticalInput = 0f;
        if (Input.GetKey(KeyCode.Q)) verticalInput += 1f;  // Up
        if (Input.GetKey(KeyCode.E)) verticalInput -= 1f;  // Down

        CurrentVerticalMove = verticalInput;

        // Mouse look - horizontal and vertical
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        // For PC, apply mouse delta directly with multiplier
        CurrentLookDelta = new Vector2(mouseX * 30f, mouseY * 30f);

        // Simple fire - hold mouse button to keep firing
        CurrentFire = Input.GetMouseButton(0);
    }

    private void HandleMobileInput()
    {
        // Apply sensitivity to raw touch delta
        Vector2 targetDelta = new Vector2(
            rawTouchDelta.x * touchSensitivityX,
            rawTouchDelta.y * touchSensitivityY
        );

        // Smooth the delta for smoother camera movement
        smoothedLookDelta = Vector2.Lerp(smoothedLookDelta, targetDelta, touchSmoothing);

        // Set current look delta
        CurrentLookDelta = smoothedLookDelta;

        // Decay raw touch delta over time when not touching
        rawTouchDelta = Vector2.Lerp(rawTouchDelta, Vector2.zero, Time.deltaTime * 15f);

        // If raw delta is very small, snap to zero
        if (rawTouchDelta.magnitude < 0.01f)
        {
            rawTouchDelta = Vector2.zero;
            smoothedLookDelta = Vector2.zero;
        }
    }

    // Called by UI touch drag handler
    private void OnLook(Vector2 delta)
    {
        if (useKeyboardMouse) return;

        // Store raw delta from touch - will be processed in HandleMobileInput
        rawTouchDelta = delta;
    }

    // Called by UI buttons (for mobile)
    private void OnMove(Vector2 dir)
    {
        if (useKeyboardMouse) return;

        CurrentMoveInput += dir;
        CurrentMoveInput = Vector2.ClampMagnitude(CurrentMoveInput, 1f);
    }

    private void OnMoveStop()
    {
        if (useKeyboardMouse) return;
        CurrentMoveInput = Vector2.zero;
    }

    private void OnFireDown()
    {
        CurrentFire = true;
    }

    private void OnFireUp()
    {
        CurrentFire = false;
    }

    private void OnVerticalMove(float dir)
    {
        if (useKeyboardMouse) return;
        CurrentVerticalMove = dir;
    }

    private void OnVerticalMoveInputStop()
    {
        if (useKeyboardMouse) return;
        CurrentVerticalMove = 0f;
    }
}