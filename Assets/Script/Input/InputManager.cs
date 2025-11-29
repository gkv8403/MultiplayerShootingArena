using UnityEngine;

public class InputManager : MonoBehaviour
{
    public static InputManager Instance { get; private set; }

    public Vector2 CurrentMoveInput { get; private set; }
    public Vector2 CurrentLookDelta { get; private set; }
    public bool CurrentFire { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        Debug.Log("[InputManager] Initialized as singleton");
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
        // Handle keyboard input for PC
        HandleKeyboardInput();

        // Decay look delta smoothly
        CurrentLookDelta = Vector2.Lerp(CurrentLookDelta, Vector2.zero, 10f * Time.deltaTime);
    }

    private void HandleKeyboardInput()
    {
        Vector2 moveInput = Vector2.zero;

        if (Input.GetKey(KeyCode.W)) moveInput.y += 1;
        if (Input.GetKey(KeyCode.S)) moveInput.y -= 1;
        if (Input.GetKey(KeyCode.A)) moveInput.x -= 1;
        if (Input.GetKey(KeyCode.D)) moveInput.x += 1;

        CurrentMoveInput = Vector2.ClampMagnitude(moveInput, 1f);

        // Mouse look (horizontal rotation)
        float mouseX = Input.GetAxis("Mouse X");
        if (mouseX != 0)
        {
            Vector2 look = CurrentLookDelta;
            look.x += mouseX * 10f;
            CurrentLookDelta = look;
        }


        // Mouse fire
        if (Input.GetMouseButtonDown(0))
        {
            Events.RaiseFireDown();
        }
        if (Input.GetMouseButtonUp(0))
        {
            Events.RaiseFireUp();
        }
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
