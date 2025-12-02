using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages all UI elements including menu, scoreboard, game over screen, and touch controls.
/// Handles player score tracking and winner/loser display.
/// </summary>
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
    public TMP_Text winnerInfoText;

    [Header("Score")]
    public TMP_Text scoreText;

    [Header("Touch Controls")]
    public Button moveUp, moveDown, moveLeft, moveRight;
    public Button moveUpVertical, moveDownVertical;
    public Button fireButton;
    public Image crosshair;

        
    private Dictionary<string, int> playerScores = new Dictionary<string, int>();
    private bool isHost = false;
    private string lastWinner = "";
    private int lastWinnerKills = 0;
    private string localPlayerName = "";

    private GameObject fullscreenLookArea;
    public Transform dragarea;

    private void Awake()
    {
        ShowMenuState();
        SetupMenuButtons();
        SetupMovementButtons();
        SetupFireButton();
        SetupFullscreenLookArea();

        Events.OnSetStatusText += SetStatusText;
        Events.OnShowMenu += ShowMenu;
        Events.OnShowGameOver += ShowGameOver;
        Events.OnShowGameOverWithWinner += ShowGameOverWithWinner;
        Events.OnUpdateScore += UpdateScoreText;
    }

    private void OnDestroy()
    {
        Events.OnSetStatusText -= SetStatusText;
        Events.OnShowMenu -= ShowMenu;
        Events.OnShowGameOver -= ShowGameOver;
        Events.OnShowGameOverWithWinner -= ShowGameOverWithWinner;
        Events.OnUpdateScore -= UpdateScoreText;
    }


    /// <summary>
    /// Creates a fullscreen invisible panel for touch drag input that sits above all UI
    /// </summary>
    private void SetupFullscreenLookArea()
    {
        
       

        // Create fullscreen GameObject
        fullscreenLookArea = new GameObject("FullscreenLookArea");
        fullscreenLookArea.transform.SetParent(dragarea, false);

        // Add RectTransform - stretch to fullscreen
        RectTransform rect = fullscreenLookArea.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.sizeDelta = Vector2.zero;
        rect.anchoredPosition = Vector2.zero;

        // Add Image component (required for raycasting) - make it invisible
        Image img = fullscreenLookArea.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0); // Fully transparent
        img.raycastTarget = true; // IMPORTANT: Allow touch detection
     
        // Add CanvasGroup to control interaction
        CanvasGroup cg = fullscreenLookArea.AddComponent<CanvasGroup>();
        cg.alpha = 0; // Invisible
        cg.interactable = true;
        cg.blocksRaycasts = true;

        // Add drag handler for look input
        LookAreaHandler handler = fullscreenLookArea.AddComponent<LookAreaHandler>();
        handler.OnDragDelta = (delta) => Events.RaiseLookDelta(delta);

        // Set as LAST sibling to ensure it's on top and receives input first
        rect.SetAsLastSibling();

        Debug.Log("[UIManager] ✓ Fullscreen look area created and set to top");

        // Disable initially (only enable during gameplay on mobile)
        fullscreenLookArea.SetActive(false);
    }

    private void SetupMenuButtons()
    {
        if (hostButton != null)
        {
            hostButton.onClick.AddListener(() => {
                SetButtonsInteractable(false);
                isHost = true;
                Events.RaiseHostClicked();
            });
        }

        if (quickJoinButton != null)
        {
            quickJoinButton.onClick.AddListener(() => {
                SetButtonsInteractable(false);
                isHost = false;
                Events.RaiseQuickJoinClicked();
            });
        }

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(() => {
                Events.RaiseRestartClicked();
            });
        }

        if (leaveButton != null)
        {
            leaveButton.onClick.AddListener(() => {
                Events.RaiseLeaveClicked();
            });
        }
    }

    private void SetupMovementButtons()
    {
        SetupHoldButton(moveUp, Vector2.up, false);
        SetupHoldButton(moveDown, Vector2.down, false);
        SetupHoldButton(moveLeft, Vector2.left, false);
        SetupHoldButton(moveRight, Vector2.right, false);

        if (moveUpVertical != null)
            SetupHoldButton(moveUpVertical, Vector2.zero, true, 1f);

        if (moveDownVertical != null)
            SetupHoldButton(moveDownVertical, Vector2.zero, true, -1f);
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
            Events.RaiseFireDown();
        });
        trig.triggers.Add(pd);

        var pu = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        pu.callback.AddListener((e) => {
            Events.RaiseFireUp();
        });
        trig.triggers.Add(pu);
    }

    private void SetStatusText(string t)
    {
        if (joinStatusText != null)
            joinStatusText.text = t;
    }

    private void ShowMenu(bool show)
    {
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

        // Disable fullscreen look area in menu
        if (fullscreenLookArea != null)
            fullscreenLookArea.SetActive(false);

        SetButtonsInteractable(true);
        playerScores.Clear();
        lastWinner = "";
        lastWinnerKills = 0;
        UpdateScoreDisplay();
    }

    private void ShowGameplayState()
    {
        SetActive(joinPanel, false);
        SetActive(scorePanel, true);
        SetActive(gameOverPanel, false);

        bool isMobile = InputManager.Instance != null && !InputManager.Instance.useKeyboardMouse;
        SetActive(controllerPanel, isMobile);

        // Enable fullscreen look area ONLY on mobile during gameplay
        if (fullscreenLookArea != null)
        {
            fullscreenLookArea.SetActive(isMobile);

            if (isMobile)
            {
                // Ensure it's on top every time we show it
                fullscreenLookArea.transform.SetAsLastSibling();
                Debug.Log("[UIManager] Fullscreen look area enabled for mobile gameplay");
            }
        }

        // Get local player name
        var localPlayer = FindObjectsOfType<Scripts.Gameplay.PlayerController>()
            .FirstOrDefault(p => p.Object != null && p.Object.HasInputAuthority);

        if (localPlayer != null)
        {
            localPlayerName = localPlayer.PlayerName.ToString();
        }

        UpdateScoreDisplay();
    }
    /// <summary>
    /// Show game over with winner information from network
    /// </summary>
    private void ShowGameOverWithWinner(string winner, int kills)
    {
        // Store winner info
        lastWinner = winner;
        lastWinnerKills = kills;

        // Call regular ShowGameOver to display it
        ShowGameOver();
    }
    private void ShowGameOver()
    {
        SetActive(gameOverPanel, true);
        SetActive(scorePanel, false);
        SetActive(controllerPanel, false);
        SetActive(joinPanel, false);

        // Disable fullscreen look area in game over
        if (fullscreenLookArea != null)
            fullscreenLookArea.SetActive(false);

        // Determine if local player won
        bool didLocalPlayerWin = !string.IsNullOrEmpty(lastWinner) && lastWinner == localPlayerName;

        // Set main game over text
        if (gameOverText != null)
        {
            if (didLocalPlayerWin)
            {
                gameOverText.text = "WINNER!";
                gameOverText.color = Color.green;
            }
            else
            {
                gameOverText.text = "GAME OVER";
                gameOverText.color = Color.red;
            }
        }

        // Show winner info
        if (winnerInfoText != null)
        {
            if (!string.IsNullOrEmpty(lastWinner) && lastWinnerKills > 0)
            {
                winnerInfoText.gameObject.SetActive(true);

                if (didLocalPlayerWin)
                {
                    winnerInfoText.text = $"You won with {lastWinnerKills} kills!";
                    winnerInfoText.color = Color.yellow;
                }
                else
                {
                    winnerInfoText.text = $"Winner: {lastWinner}\n{lastWinnerKills} kills";
                    winnerInfoText.color = Color.white;
                }
            }
            else
            {
                winnerInfoText.gameObject.SetActive(false);
            }
        }

        // Setup buttons
        if (restartButton != null)
        {
            restartButton.gameObject.SetActive(true);
        }

        if (leaveButton != null)
        {
            leaveButton.gameObject.SetActive(true);
        }
    }

    private void UpdateScoreText(string playerName, int kills)
    {
        playerScores[playerName] = kills;

        // Track winner
        if (kills > lastWinnerKills)
        {
            lastWinner = playerName;
            lastWinnerKills = kills;
        }

        UpdateScoreDisplay();
    }

    private void UpdateScoreDisplay()
    {
        if (scoreText == null) return;

        if (playerScores.Count == 0)
        {
            scoreText.text = "Waiting for players...";
            return;
        }

        string display = "SCOREBOARD\n\n";
        var sortedPlayers = playerScores.OrderByDescending(x => x.Value).ToList();

        for (int i = 0; i < sortedPlayers.Count; i++)
        {
            var player = sortedPlayers[i];
            string prefix = "";

            if (i == 0 && player.Value > 0)
                prefix = "1st ";
            else if (i == 1 && player.Value > 0)
                prefix = "2nd ";
            else if (i == 2 && player.Value > 0)
                prefix = "3rd ";

            display += $"{prefix}{player.Key}: {player.Value}\n";
        }

        scoreText.text = display;
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

    /// <summary>
    /// Handles touch input dragging for camera look control
    /// Now works fullscreen without being blocked by other UI
    /// </summary>
    private class LookAreaHandler : MonoBehaviour, IDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        public System.Action<Vector2> OnDragDelta;
        private Vector2 lastPos;
        private bool dragging = false;
        private int touchId = -1;

        public void OnDrag(PointerEventData eventData)
        {
            if (!dragging) return;

            // Use eventData.delta directly - this is the most accurate
            Vector2 delta = eventData.delta;

            // Send delta immediately
            OnDragDelta?.Invoke(delta);

            // Debug log to verify touch is working
            if (delta.magnitude > 0.1f)
            {
                Debug.Log($"[LookArea] Touch drag delta: {delta}");
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            dragging = true;
            lastPos = eventData.position;
            touchId = eventData.pointerId;
            Debug.Log($"[LookArea] Touch started at {eventData.position}");
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == touchId)
            {
                dragging = false;
                touchId = -1;
                Debug.Log("[LookArea] Touch ended");
            }
        }
    }
}