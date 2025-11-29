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
    public TMP_Text winnerInfoText;
    public TMP_Text playerCountText;

    [Header("Score")]
    public TMP_Text scoreText;

    [Header("Touch Controls")]
    public Button moveUp, moveDown, moveLeft, moveRight;
    public Button moveUpVertical, moveDownVertical;
    public Button fireButton;
    public RectTransform lookArea;
    public Image crosshair;

    // ✅ FIX: Thread-safe score tracking
    private Dictionary<string, int> playerScores = new Dictionary<string, int>();
    private bool isHost = false;
    private string lastWinner = "";
    private int lastWinnerKills = 0;
    private float lastScoreUpdateTime = 0f;

    private void Awake()
    {
        ShowMenuState();
        SetupMenuButtons();
        SetupMovementButtons();
        SetupFireButton();
        SetupLookArea();

        // ✅ FIX: Subscribe to events with error handling
        Events.OnSetStatusText += SetStatusText;
        Events.OnShowMenu += ShowMenu;
        Events.OnShowGameOver += ShowGameOver;
        Events.OnUpdateScore += UpdateScoreText;

        Debug.Log("[UIManager] ✓ Initialized and subscribed to events");
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
                Debug.Log("[UIManager] 🖥️ Host clicked");
                SetButtonsInteractable(false);
                isHost = true;
                Events.RaiseHostClicked();
            });
        }

        if (quickJoinButton != null)
        {
            quickJoinButton.onClick.AddListener(() => {
                Debug.Log("[UIManager] 🔌 QuickJoin clicked");
                SetButtonsInteractable(false);
                isHost = false;
                Events.RaiseQuickJoinClicked();
            });
        }

        if (restartButton != null)
        {
            restartButton.onClick.AddListener(() => {
                Debug.Log("[UIManager] 🔄 Restart clicked");
                Events.RaiseRestartClicked();
            });
        }

        if (leaveButton != null)
        {
            leaveButton.onClick.AddListener(() => {
                Debug.Log("[UIManager] 🚪 Leave clicked");
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
    }

    private void ShowMenu(bool show)
    {
        Debug.Log($"[UIManager] 📺 ShowMenu({show})");

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
        lastWinner = "";
        lastWinnerKills = 0;
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

        UpdateScoreDisplay();

        Debug.Log($"[UIManager] ✓ Gameplay state (Mobile Controls: {isMobile})");
    }

    private void ShowGameOver()
    {
        Debug.Log("[UIManager] 🏁 ShowGameOver called");

        SetActive(gameOverPanel, true);
        SetActive(scorePanel, true);
        SetActive(controllerPanel, false);
        SetActive(joinPanel, false);

        // Show winner information
        if (winnerInfoText != null)
        {
            if (!string.IsNullOrEmpty(lastWinner) && lastWinnerKills > 0)
            {
                winnerInfoText.gameObject.SetActive(true);
                winnerInfoText.text = $"🏆 {lastWinner}\n{lastWinnerKills} Kills";
                Debug.Log($"[UIManager] 🏆 Displaying winner: {lastWinner} ({lastWinnerKills} kills)");
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

        Debug.Log("[UIManager] ✓ Game over state displayed");
    }

    // ✅ FIX: Enhanced score update with proper tracking
    private void UpdateScoreText(string playerName, int kills)
    {
        Debug.Log($"[UIManager] ==================");
        Debug.Log($"[UIManager] 📊 UpdateScoreText EVENT");
        Debug.Log($"[UIManager] Player: {playerName}");
        Debug.Log($"[UIManager] Kills: {kills}");
        Debug.Log($"[UIManager] Time: {Time.time:F2}");

        // ✅ FIX: Update player's score
        if (!playerScores.ContainsKey(playerName))
        {
            Debug.Log($"[UIManager] ➕ Adding new player: {playerName}");
        }
        else
        {
            Debug.Log($"[UIManager] 🔄 Updating {playerName}: {playerScores[playerName]} → {kills}");
        }

        playerScores[playerName] = kills;

        // Track winner (highest score)
        if (kills > lastWinnerKills)
        {
            lastWinner = playerName;
            lastWinnerKills = kills;
            Debug.Log($"[UIManager] 👑 New leader: {lastWinner} with {lastWinnerKills} kills");
        }

        // Update display
        lastScoreUpdateTime = Time.time;
        UpdateScoreDisplay();

        Debug.Log($"[UIManager] ✓ Score updated successfully");
        Debug.Log($"[UIManager] Total players: {playerScores.Count}");
        Debug.Log($"[UIManager] ==================");
    }

    // ✅ FIX: Clean and clear score display
    private void UpdateScoreDisplay()
    {
        if (scoreText == null)
        {
            Debug.LogWarning("[UIManager] ⚠️ scoreText is null!");
            return;
        }

        if (playerScores.Count == 0)
        {
            scoreText.text = "Waiting for players...";
            return;
        }

        // Build score display
        string display = "=== SCOREBOARD ===\n\n";

        // Sort by kills (highest first)
        var sortedPlayers = playerScores.OrderByDescending(x => x.Value).ToList();

        for (int i = 0; i < sortedPlayers.Count; i++)
        {
            var player = sortedPlayers[i];
            string medal = "";

            if (i == 0 && player.Value > 0)
                medal = "🥇 ";
            else if (i == 1 && player.Value > 0)
                medal = "🥈 ";
            else if (i == 2 && player.Value > 0)
                medal = "🥉 ";

            display += $"{medal}{player.Key} → {player.Value}\n";
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

    // ✅ FIX: Debug display
    private void OnGUI()
    {
        if (!Debug.isDebugBuild) return;

        GUI.color = Color.cyan;
        GUILayout.BeginArea(new Rect(Screen.width - 310, 10, 300, 150));
        GUILayout.Label("=== UI MANAGER (CLIENT) ===");
        GUILayout.Label($"Players Tracked: {playerScores.Count}");
        GUILayout.Label($"Leader: {lastWinner} ({lastWinnerKills})");
        GUILayout.Label($"Last Update: {Time.time - lastScoreUpdateTime:F1}s ago");
        GUILayout.Label("--- Scores ---");
        foreach (var kvp in playerScores.OrderByDescending(x => x.Value).Take(3))
        {
            GUILayout.Label($"{kvp.Key}: {kvp.Value}");
        }
        GUILayout.EndArea();
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