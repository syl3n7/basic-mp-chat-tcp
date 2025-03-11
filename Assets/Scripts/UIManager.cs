using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.Collections;
using System.Threading;

public class UIManager : MonoBehaviour
{
    // UI Panels
    [Header("Panels")]
    public GameObject serverSelectionPanel;
    public GameObject authenticationPanel;
    public GameObject chatPanel;
    
    // System messages display
    [Header("System Messages")]
    public TextMeshProUGUI systemMessageText;
    public float messageDisplayTime = 5f;
    private float messageTimer = 0f;

    [Header("Navigation")]
    public Button disconnectButton;
    public Button changeServerButton;
    
    // References to panel controllers
    private ServerSelectionPanel serverSelector;
    private AuthenticationPanel authPanel;
    private ChatPanel chatPanelController;
    
    private NetworkManager networkManager;

    [Header("Animations")]
    public float panelFadeTime = 0.3f;
    public AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    private void Start()
    {
        // Get references to component scripts
        serverSelector = serverSelectionPanel.GetComponent<ServerSelectionPanel>();
        authPanel = authenticationPanel.GetComponent<AuthenticationPanel>();
        chatPanelController = chatPanel.GetComponent<ChatPanel>();
        
        // Get NetworkManager reference
        networkManager = NetworkManager.Instance;
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager instance not found!");
            return;
        }
        
        // Subscribe to network events
        networkManager.OnConnectionStateChanged += HandleConnectionStateChanged;
        networkManager.OnAuthenticationResult += HandleAuthenticationResult;
        networkManager.OnMessageReceived += OnMessageReceived;
        
        // Set up navigation button events
        if (disconnectButton != null)
            disconnectButton.onClick.AddListener(OnDisconnectClicked);
        
        if (changeServerButton != null)
            changeServerButton.onClick.AddListener(OnChangeServerClicked);
        
        // Start with only server selection panel active
        ShowServerSelectionOnly();
        
        ShowSystemMessage("Welcome! Please select or enter a server to connect.");
    }
    
    private void OnDestroy()
    {
        // Unsubscribe from events
        if (networkManager != null)
        {
            networkManager.OnConnectionStateChanged -= HandleConnectionStateChanged;
            networkManager.OnAuthenticationResult -= HandleAuthenticationResult;
            networkManager.OnMessageReceived -= OnMessageReceived;
        }
    }
    
    private void Update()
    {
        // Handle system message timeout
        if (messageTimer > 0)
        {
            messageTimer -= Time.deltaTime;
            if (messageTimer <= 0)
            {
                systemMessageText.gameObject.SetActive(false);
            }
        }
    }
    
    // Show system message with timeout
    public void ShowSystemMessage(string message)
    {
        // Check if we're on the main thread
        if (Thread.CurrentThread.ManagedThreadId != 1)
        {
            Debug.LogWarning("ShowSystemMessage called from a non-main thread. Message will be queued: " + message);
            return;
        }
        
        if (systemMessageText != null)
        {
            systemMessageText.text = message;
            systemMessageText.gameObject.SetActive(true);
            messageTimer = messageDisplayTime;
        }
        
        Debug.Log("SYSTEM: " + message);
    }
    
    // Panel control methods
    private void ShowServerSelectionOnly()
    {
        StartCoroutine(TransitionToPanels(serverSelectionPanel));
    }
    
    private void ShowAuthenticationOnly()
    {
        StartCoroutine(TransitionToPanels(authenticationPanel));
    }
    
    private void ShowChatOnly()
    {
        StartCoroutine(TransitionToPanels(chatPanel));
    }
    
    private IEnumerator TransitionToPanels(params GameObject[] panelsToShow)
    {
        // Get all panels
        GameObject[] allPanels = { serverSelectionPanel, authenticationPanel, chatPanel };
        
        // Fade out all panels
        foreach (GameObject panel in allPanels)
        {
            if (panel.activeSelf && !System.Array.Exists(panelsToShow, p => p == panel))
            {
                CanvasGroup cg = panel.GetComponent<CanvasGroup>();
                if (cg == null) cg = panel.AddComponent<CanvasGroup>();
                
                float startTime = Time.time;
                float fromAlpha = 1f;
                
                while (Time.time < startTime + panelFadeTime)
                {
                    float progress = (Time.time - startTime) / panelFadeTime;
                    cg.alpha = Mathf.Lerp(fromAlpha, 0f, fadeCurve.Evaluate(progress));
                    yield return null;
                }
                
                cg.alpha = 0f;
                panel.SetActive(false);
            }
        }
        
        // Fade in target panels
        foreach (GameObject panel in panelsToShow)
        {
            panel.SetActive(true);
            CanvasGroup cg = panel.GetComponent<CanvasGroup>();
            if (cg == null) cg = panel.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
            
            float startTime = Time.time;
            float toAlpha = 1f;
            
            while (Time.time < startTime + panelFadeTime)
            {
                float progress = (Time.time - startTime) / panelFadeTime;
                cg.alpha = Mathf.Lerp(0f, toAlpha, fadeCurve.Evaluate(progress));
                yield return null;
            }
            
            cg.alpha = toAlpha;
        }
        
        // Update navigation buttons
        UpdateNavigationButtons();
    }
    
    private void UpdateNavigationButtons()
    {
        // Show/hide buttons based on the current active panel
        if (disconnectButton != null)
            disconnectButton.gameObject.SetActive(authenticationPanel.activeSelf || chatPanel.activeSelf);
        
        if (changeServerButton != null)
            changeServerButton.gameObject.SetActive(chatPanel.activeSelf);
    }
    
    // Event handlers
    private void HandleConnectionStateChanged(bool connected)
    {
        if (connected)
        {
            ShowSystemMessage("Connected to server. Please log in or register.");
            ShowAuthenticationOnly();
        }
        else
        {
            ShowSystemMessage("Disconnected from server.");
            ShowServerSelectionOnly();
        }
    }
    
    private void HandleAuthenticationResult(bool success, string message)
    {
        if (success)
        {
            ShowSystemMessage("Authentication successful! Welcome to the chat.");
            ShowChatOnly();
        }
        else
        {
            ShowSystemMessage("Authentication failed: " + message);
            // Stay on authentication panel
        }
    }
    
    // Message received handler
    private void OnMessageReceived(string message)
    {
        // Pass message to chat panel if it's active
        if (chatPanel.activeInHierarchy)
        {
            chatPanelController.AddMessage(message);
        }
        else
        {
            // Show important messages as system messages
            if (message.Contains("banned") || message.Contains("kicked"))
            {
                ShowSystemMessage(message);
            }
        }
    }
    
    // Button handlers
    public void OnDisconnectClicked()
    {
        if (networkManager != null)
        {
            networkManager.DisconnectFromServer();
            ShowSystemMessage("Disconnected from server.");
            ShowServerSelectionOnly();
        }
    }
    
    public void OnChangeServerClicked()
    {
        if (networkManager != null)
        {
            networkManager.DisconnectFromServer();
            ShowSystemMessage("Disconnected. Please select another server.");
            ShowServerSelectionOnly();
        }
    }
}