using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [Header("Panels")]
    public GameObject joinPanel;
    public GameObject scorePanel;
    public GameObject gameOverPanel;
    public GameObject controllerPanel;

    [Header("Join Panel")]
    public Button hostButton;
    public Button quickJoinButton;
    public TMP_Text joinStatusText;

    [Header("Game Over")]
    public Button restartButton;
    public Button leaveButton;
    public TMP_Text gameOverText;

    [Header("Score")]
    public TMP_Text scoreText;

    [Header("Touch Controls")]
    public Button moveUp, moveDown, moveLeft, moveRight;
    public Button fireButton;
    public RectTransform lookArea;
    public Image crosshair;

    private void Awake()
    {
        // Start with menu visible
        ShowMenuState();

        SetupMenuButtons();
        SetupMovementButtons();
        SetupFireButton();
        SetupLookArea();

        // Subscribe to events
        Events.OnSetStatusText += SetStatusText;
        Events.OnShowMenu += ShowMenu;
        Events.OnShowGameOver += ShowGameOver;
        Events.OnUpdateScore += UpdateScoreText;

        Debug.Log("[UIManager] Initialized");
    }

    private void OnDestroy()
    {
        Events.OnSetStatusText -= SetStatusText;
        Events.OnShowMenu -= ShowMenu;
        Events.OnShowGameOver -= ShowGameOver;
        Events.OnUpdateScore -= UpdateScoreText;
    }

    private void SetupMenuButtons()
    {
        if (hostButton != null)
        {
            hostButton.onClick.AddListener(() => {
                Debug.Log("[UIManager] Host clicked");
                SetButtonsInteractable(false);
                Events.RaiseHostClicked();
            });
        }

        if (quickJoinButton != null)
        {
            quickJoinButton.onClick.AddListener(() => {
                Debug.Log("[UIManager] QuickJoin clicked");
                SetButtonsInteractable(false);
                Events.RaiseQuickJoinClicked();
            });
        }

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(() => {
                Debug.Log("[UIManager] Restart clicked");
                Events.RaiseRestartClicked();
            });
        }

        if (leaveButton != null)
        {
            leaveButton.onClick.AddListener(() => {
                Debug.Log("[UIManager] Leave clicked");
                Events.RaiseLeaveClicked();
            });
        }
    }

    private void SetupMovementButtons()
    {
        SetupHoldButton(moveUp, Vector2.up);
        SetupHoldButton(moveDown, Vector2.down);
        SetupHoldButton(moveLeft, Vector2.left);
        SetupHoldButton(moveRight, Vector2.right);
    }

    private void SetupHoldButton(Button b, Vector2 dir)
    {
        if (b == null) return;

        var trig = b.gameObject.AddComponent<EventTrigger>();

        var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener((e) => Events.RaiseMoveInput(dir));
        trig.triggers.Add(down);

        var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener((e) => Events.RaiseMoveInputStop());
        trig.triggers.Add(up);
    }

    private void SetupFireButton()
    {
        if (fireButton == null) return;

        var trig = fireButton.gameObject.AddComponent<EventTrigger>();

        var pd = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        pd.callback.AddListener((e) => {
            Debug.Log("[UI] Fire Down");
            Events.RaiseFireDown();
        });
        trig.triggers.Add(pd);

        var pu = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        pu.callback.AddListener((e) => {
            Debug.Log("[UI] Fire Up");
            Events.RaiseFireUp();
        });
        trig.triggers.Add(pu);
    }

    private void SetupLookArea()
    {
        if (lookArea == null) return;

        var dragHandler = lookArea.gameObject.AddComponent<LookAreaHandler>();
        dragHandler.OnDragDelta = (delta) => Events.RaiseLookDelta(delta);
    }

    private void SetStatusText(string t)
    {
        if (joinStatusText != null)
            joinStatusText.text = t;
        Debug.Log($"[UIManager] Status: {t}");
    }

    private void ShowMenu(bool show)
    {
        Debug.Log($"[UIManager] ShowMenu({show}) called");

        if (show)
        {
            ShowMenuState();
        }
        else
        {
            ShowGameplayState();
        }
    }

    private void ShowMenuState()
    {
        // Show ONLY menu panel
        SetActive(joinPanel, true);
        SetActive(scorePanel, false);
        SetActive(controllerPanel, false);
        SetActive(gameOverPanel, false);

        SetButtonsInteractable(true);

        Debug.Log("[UIManager] ✓ Menu state active");
    }

    private void ShowGameplayState()
    {
        // Hide menu, show gameplay UI
        SetActive(joinPanel, false);
        SetActive(scorePanel, true);
        SetActive(gameOverPanel, false);

        // Show controls based on input mode
        bool isMobile = InputManager.Instance != null && !InputManager.Instance.useKeyboardMouse;
        SetActive(controllerPanel, isMobile);

        // Initialize score
        if (scoreText != null)
            scoreText.text = "Waiting for match...";

        Debug.Log($"[UIManager] ✓ Gameplay state (Controls: {isMobile})");
    }

    private void ShowGameOver()
    {
        Debug.Log("[UIManager] ShowGameOver called");

        // Show game over panel, keep score visible
        SetActive(gameOverPanel, true);
        SetActive(scorePanel, true);
        SetActive(controllerPanel, false);
        SetActive(joinPanel, false);

        if (gameOverText != null)
            gameOverText.text = "Game Over!";

        Debug.Log("[UIManager] ✓ Game over state");
    }

    private void UpdateScoreText(string playerName, int kills)
    {
        if (scoreText != null)
        {
            scoreText.text = $"{playerName}: {kills} kills";
            Debug.Log($"[UIManager] ✓ Score updated: {playerName} - {kills}");
        }
    }

    private void SetButtonsInteractable(bool interactable)
    {
        if (hostButton != null)
            hostButton.interactable = interactable;

        if (quickJoinButton != null)
            quickJoinButton.interactable = interactable;
    }

    private void SetActive(GameObject obj, bool active)
    {
        if (obj != null && obj.activeSelf != active)
        {
            obj.SetActive(active);
        }
    }

    private class LookAreaHandler : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        public System.Action<Vector2> OnDragDelta;
        private Vector2 lastPos;
        private bool dragging = false;

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging) return;
            Vector2 pos = eventData.position;
            Vector2 delta = pos - lastPos;
            lastPos = pos;
            OnDragDelta?.Invoke(delta);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            dragging = true;
            lastPos = eventData.position;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            dragging = false;
        }
    }
}