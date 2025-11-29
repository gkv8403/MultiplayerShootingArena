using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

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
    public TMP_Text winnerInfoText; // NEW: Shows winner details
    public TMP_Text playerCountText;

    [Header("Score")]
    public TMP_Text scoreText; // Shows all players' scores

    [Header("Touch Controls")]
    public Button moveUp, moveDown, moveLeft, moveRight;
    public Button moveUpVertical, moveDownVertical; // NEW: For Q/E movement
    public Button fireButton;
    public RectTransform lookArea;
    public Image crosshair;

    // Track all player scores
    private Dictionary<string, int> playerScores = new Dictionary<string, int>();
    private bool isHost = false;
    private string lastWinner = "";
    private int lastWinnerKills = 0;

    private void Awake()
    {
        ShowMenuState();
        SetupMenuButtons();
        SetupMovementButtons();
        SetupFireButton();
        SetupLookArea();

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
                isHost = true;
                Events.RaiseHostClicked();
            });
        }

        if (quickJoinButton != null)
        {
            quickJoinButton.onClick.AddListener(() => {
                Debug.Log("[UIManager] QuickJoin clicked");
                SetButtonsInteractable(false);
                isHost = false;
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
        // Horizontal movement (WASD)
        SetupHoldButton(moveUp, Vector2.up, false);
        SetupHoldButton(moveDown, Vector2.down, false);
        SetupHoldButton(moveLeft, Vector2.left, false);
        SetupHoldButton(moveRight, Vector2.right, false);

        // Vertical movement (Q/E for PC, buttons for mobile)
        if (moveUpVertical != null)
            SetupHoldButton(moveUpVertical, Vector2.zero, true, 1f); // Up vertical

        if (moveDownVertical != null)
            SetupHoldButton(moveDownVertical, Vector2.zero, true, -1f); // Down vertical
    }

    private void SetupHoldButton(Button b, Vector2 dir, bool isVertical = false, float verticalDir = 0f)
    {
        if (b == null) return;

        var trig = b.gameObject.AddComponent<EventTrigger>();

        var down = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        down.callback.AddListener((e) => {
            if (isVertical)
                Events.RaiseVerticalMoveInput(verticalDir);
            else
                Events.RaiseMoveInput(dir);
        });
        trig.triggers.Add(down);

        var up = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        up.callback.AddListener((e) => {
            if (isVertical)
                Events.RaiseVerticalMoveInputStop();
            else
                Events.RaiseMoveInputStop();
        });
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
        SetActive(joinPanel, true);
        SetActive(scorePanel, false);
        SetActive(controllerPanel, false);
        SetActive(gameOverPanel, false);

        SetButtonsInteractable(true);
        playerScores.Clear();
        UpdateScoreDisplay();

        Debug.Log("[UIManager] ✓ Menu state active");
    }

    private void ShowGameplayState()
    {
        SetActive(joinPanel, false);
        SetActive(scorePanel, true);
        SetActive(gameOverPanel, false);

        bool isMobile = InputManager.Instance != null && !InputManager.Instance.useKeyboardMouse;
        SetActive(controllerPanel, isMobile);

        // FIX: Keep showing current scores, don't reset to "Match starting..."
        UpdateScoreDisplay();

        Debug.Log($"[UIManager] ✓ Gameplay state (Controls: {isMobile})");
    }

    private void ShowGameOver()
    {
        Debug.Log("[UIManager] ShowGameOver called");

        SetActive(gameOverPanel, true);
        SetActive(scorePanel, true);
        SetActive(controllerPanel, false);
        SetActive(joinPanel, false);

        // FIX: Show winner information
        if (winnerInfoText != null)
        {
            if (!string.IsNullOrEmpty(lastWinner))
            {
                winnerInfoText.gameObject.SetActive(true);
                winnerInfoText.text = $"🏆 Winner: {lastWinner}\nKills: {lastWinnerKills}";
            }
            else
            {
                winnerInfoText.gameObject.SetActive(false);
            }
        }

        // Different UI for host vs client
        if (isHost)
        {
            if (gameOverText != null)
                gameOverText.text = "Match Over!\n(You are Host)";

            if (restartButton != null)
                restartButton.gameObject.SetActive(true);

            if (playerCountText != null)
            {
                int playerCount = FindObjectsOfType<Scripts.Gameplay.PlayerController>().Length;
                playerCountText.gameObject.SetActive(true);
                playerCountText.text = $"Players: {playerCount}";
            }
        }
        else
        {
            if (gameOverText != null)
                gameOverText.text = "Match Over!";

            if (restartButton != null)
            {
                restartButton.gameObject.SetActive(true);
                var buttonText = restartButton.GetComponentInChildren<TMP_Text>();
                if (buttonText != null)
                    buttonText.text = "Request Restart";
            }

            if (playerCountText != null)
                playerCountText.gameObject.SetActive(false);
        }

        Debug.Log("[UIManager] ✓ Game over state");
    }

    private void UpdateScoreText(string playerName, int kills)
    {
        Debug.Log($"[UIManager] === UpdateScoreText called ===");
        Debug.Log($"[UIManager] Player: {playerName}, Kills: {kills}");

        // Update player's score in dictionary
        playerScores[playerName] = kills;

        // Track winner (player with most kills)
        if (kills > lastWinnerKills || (kills == lastWinnerKills && playerName == lastWinner))
        {
            lastWinner = playerName;
            lastWinnerKills = kills;
        }

        // Rebuild score display
        UpdateScoreDisplay();

        Debug.Log($"[UIManager] ✓ Score updated: {playerName} - {kills}");
        Debug.Log($"[UIManager] Total players tracked: {playerScores.Count}");
    }

    private void UpdateScoreDisplay()
    {
        if (scoreText == null) return;

        if (playerScores.Count == 0)
        {
            scoreText.text = "Waiting for players...";
            return;
        }

        // FIX: Simple format as requested: "Player1 -> X kills"
        string scoreDisplay = "=== SCORES ===\n";

        // Sort by kills descending
        var sortedPlayers = playerScores.OrderByDescending(x => x.Value);

        foreach (var player in sortedPlayers)
        {
            scoreDisplay += $"{player.Key} → {player.Value} kills\n";
        }

        scoreText.text = scoreDisplay;
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