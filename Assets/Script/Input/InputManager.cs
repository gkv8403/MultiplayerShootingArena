using UnityEngine;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    [Header("Input Mode")]
    public bool useKeyboardMouse = true;

    public Vector2 CurrentMoveInput { get; private set; }
    public Vector2 CurrentLookDelta { get; private set; }
    public bool CurrentFire { get; private set; }

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
        if (useKeyboardMouse)
        {
            HandleKeyboardInput();
        }

        // Decay look delta smoothly
        CurrentLookDelta = Vector2.Lerp(CurrentLookDelta, Vector2.zero, 10f * Time.deltaTime);
    }

    private void HandleKeyboardInput()
    {
        // Movement input
        Vector2 moveInput = Vector2.zero;

        if (Input.GetKey(KeyCode.W)) moveInput.y += 1;
        if (Input.GetKey(KeyCode.S)) moveInput.y -= 1;
        if (Input.GetKey(KeyCode.A)) moveInput.x -= 1;
        if (Input.GetKey(KeyCode.D)) moveInput.x += 1;

        CurrentMoveInput = Vector2.ClampMagnitude(moveInput, 1f);

        // Mouse look - horizontal and vertical
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        if (mouseX != 0 || mouseY != 0)
        {
            Vector2 look = CurrentLookDelta;
            look.x += mouseX * 15f;  // Horizontal
            look.y += mouseY * 15f;  // Vertical
            CurrentLookDelta = look;
        }

        // Simple fire - hold mouse button to keep firing
        CurrentFire = Input.GetMouseButton(0);
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

    private void OnLook(Vector2 delta)
    {
        if (useKeyboardMouse) return;
        CurrentLookDelta += delta * 0.5f;
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