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

    private bool menuVisible = true;
    private bool gameplayVisible = false;

    private void Awake()
    {
        // Initialize all panels
        if (joinPanel != null) joinPanel.SetActive(true);
        if (scorePanel != null) scorePanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (controllerPanel != null) controllerPanel.SetActive(false);

        Debug.Log("[UIManager] Panels initialized - Menu only visible");

        // Setup button listeners
        if (hostButton != null)
            hostButton.onClick.AddListener(() => {
                Debug.Log("[UIManager] Host clicked");
                Events.RaiseHostClicked();
            });

        if (quickJoinButton != null)
            quickJoinButton.onClick.AddListener(() => {
                Debug.Log("[UIManager] QuickJoin clicked");
                Events.RaiseQuickJoinClicked();
            });

        if (restartButton != null)
            restartButton.onClick.AddListener(() => {
                Debug.Log("[UIManager] Restart clicked");
                Events.RaiseRestartClicked();
            });

        if (leaveButton != null)
            leaveButton.onClick.AddListener(() => {
                Debug.Log("[UIManager] Leave clicked");
                Events.RaiseLeaveClicked();
            });

        SetupHoldButton(moveUp, Vector2.up);
        SetupHoldButton(moveDown, Vector2.down);
        SetupHoldButton(moveLeft, Vector2.left);
        SetupHoldButton(moveRight, Vector2.right);

        // Fire button setup
        if (fireButton != null)
        {
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

        // Look area setup
        if (lookArea != null)
        {
            var dragHandler = lookArea.gameObject.AddComponent<LookAreaHandler>();
            dragHandler.OnDragDelta = (delta) => Events.RaiseLookDelta(delta);
        }

        // Subscribe to game events
        Events.OnSetStatusText += SetStatusText;
        Events.OnShowMenu += ShowMenu;
        Events.OnShowGameOver += ShowGameOver;
        Events.OnUpdateScore += UpdateScoreText;

        Debug.Log("[UIManager] Event subscribers set up");
    }

    private void OnDestroy()
    {
        Events.OnSetStatusText -= SetStatusText;
        Events.OnShowMenu -= ShowMenu;
        Events.OnShowGameOver -= ShowGameOver;
        Events.OnUpdateScore -= UpdateScoreText;
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
            // Show menu panels
            if (joinPanel != null) joinPanel.SetActive(true);
            if (scorePanel != null) scorePanel.SetActive(false);
            if (controllerPanel != null) controllerPanel.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            menuVisible = true;
            gameplayVisible = false;
            Debug.Log("[UIManager] Menu panels shown, gameplay hidden");
        }
        else
        {
            // Show gameplay panels
            if (joinPanel != null) joinPanel.SetActive(false);
            if (scorePanel != null) scorePanel.SetActive(true);
            if (controllerPanel != null) controllerPanel.SetActive(true);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);
            menuVisible = false;
            gameplayVisible = true;
            Debug.Log("[UIManager] Gameplay panels shown, menu hidden");
        }
    }

    private void ShowGameOver()
    {
        Debug.Log("[UIManager] ShowGameOver called");

        if (gameOverPanel != null) gameOverPanel.SetActive(true);
        if (scorePanel != null) scorePanel.SetActive(false);
        if (controllerPanel != null) controllerPanel.SetActive(false);
        if (joinPanel != null) joinPanel.SetActive(false);

        Debug.Log("[UIManager] Game over panel shown");
    }

    private void UpdateScoreText(string playerName, int kills)
    {
        if (scoreText != null)
            scoreText.text = $"{playerName}: {kills}";
        Debug.Log($"[UIManager] Score updated: {playerName} - {kills}");
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
