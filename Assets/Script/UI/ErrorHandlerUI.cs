using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// Handles error popups with retry functionality for:
/// - Addressables loading failures
/// - Asset Bundle download failures  
/// - Network connection failures
/// </summary>
public class ErrorHandlerUI : MonoBehaviour
{
    public static ErrorHandlerUI Instance { get; private set; }

    [Header("Error Popup Panel")]
    public GameObject errorPanel;
    public TMP_Text errorTitleText;
    public TMP_Text errorMessageText;
    public Button retryButton;
    public Button closeButton;

    private Action currentRetryAction;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Setup buttons
        if (retryButton != null)
            retryButton.onClick.AddListener(OnRetryClicked);

        if (closeButton != null)
            closeButton.onClick.AddListener(OnCloseClicked);

        // Hide panel initially
        if (errorPanel != null)
            errorPanel.SetActive(false);

        Debug.Log("[ErrorHandler] Initialized");
    }

    /// <summary>
    /// Shows error popup with retry option
    /// </summary>
    public void ShowError(string title, string message, Action retryAction = null)
    {
        Debug.LogWarning($"[ErrorHandler] Showing error: {title} - {message}");

        if (errorPanel != null)
            errorPanel.SetActive(true);

        if (errorTitleText != null)
            errorTitleText.text = title;

        if (errorMessageText != null)
            errorMessageText.text = message;

        currentRetryAction = retryAction;

        // Show/hide retry button based on whether retry action provided
        if (retryButton != null)
            retryButton.gameObject.SetActive(retryAction != null);
    }

    private void OnRetryClicked()
    {
        Debug.Log("[ErrorHandler] Retry clicked");

        HideError();

        currentRetryAction?.Invoke();
        currentRetryAction = null;
    }

    private void OnCloseClicked()
    {
        Debug.Log("[ErrorHandler] Close clicked");
        HideError();
    }

    public void HideError()
    {
        if (errorPanel != null)
            errorPanel.SetActive(false);

        currentRetryAction = null;
    }
}

// Extension to Events.cs - add these events
public static class ErrorEvents
{
    public static event Action<string, string, Action> OnShowError;

    public static void RaiseShowError(string title, string message, Action retryAction = null)
    {
        OnShowError?.Invoke(title, message, retryAction);

        // Also show via ErrorHandlerUI if available
        if (ErrorHandlerUI.Instance != null)
        {
            ErrorHandlerUI.Instance.ShowError(title, message, retryAction);
        }
    }
}