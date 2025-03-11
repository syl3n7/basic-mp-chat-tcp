using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class ChatPanel : MonoBehaviour
{
    [Header("Chat Display")]
    public ScrollRect chatScrollRect;
    public Transform messageContainer;
    public GameObject messageBubblePrefab;
    public int maxMessages = 100;
    
    [Header("Input")]
    public TMP_InputField messageInput; // Changed from InputField
    public Button sendButton;
    public TMP_Dropdown commandDropdown; // Changed from Dropdown
    
    [Header("Room Info")]
    public TextMeshProUGUI currentRoomText; // Changed from Text
    public TextMeshProUGUI usersInRoomText; // Changed from Text
    
    private NetworkManager networkManager;
    private List<GameObject> messageObjects = new List<GameObject>();
    
    // track if user has manually scrolled up
    private bool userHasScrolled = false;
    private float autoScrollThreshold = 0.05f; // How close to bottom to auto-scroll
    
    private void OnEnable()
    {
        networkManager = NetworkManager.Instance;
        
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager instance is null in ChatPanel");
            return;
        }
        
        // Subscribe to network events
        networkManager.OnMessageReceived += HandleMessageReceived;
        Debug.Log("ChatPanel subscribed to OnMessageReceived event");
        
        // Clear chat history
        ClearChat();
        
        // Set up command dropdown
        if (commandDropdown != null)
        {
            SetupCommandDropdown();
        }
        else
        {
            Debug.LogError("Command dropdown is null in ChatPanel");
        }
        
        // Set up button listeners
        if (sendButton != null)
        {
            sendButton.onClick.RemoveAllListeners();
            sendButton.onClick.AddListener(OnSendClicked);
        }
        
        if (messageInput != null)
        {
            messageInput.onEndEdit.RemoveAllListeners();
            messageInput.onEndEdit.AddListener(OnInputSubmit);
        }
        
        if (commandDropdown != null)
        {
            commandDropdown.onValueChanged.RemoveAllListeners();
            commandDropdown.onValueChanged.AddListener(OnCommandSelected);
        }
        
        // Initial help message
        AddSystemMessage("Type /help for available commands.");
    }
    
    private void Start()
    {
        // Validate components
        if (chatScrollRect == null)
            Debug.LogError("Chat ScrollRect is not assigned!");
        
        if (messageContainer == null)
            Debug.LogError("Message Container is not assigned!");
        
        if (messageBubblePrefab == null)
            Debug.LogError("Message Bubble Prefab is not assigned!");
        
        // Validate prefab has required components
        if (messageBubblePrefab != null)
        {
            TextMeshProUGUI tmpText = messageBubblePrefab.GetComponentInChildren<TextMeshProUGUI>();
            if (tmpText == null)
                Debug.LogError("Message Bubble Prefab doesn't have a TextMeshProUGUI component!");
        }
        
        // Force canvas update to make sure layout is correct
        Canvas.ForceUpdateCanvases();
        
        // Add scroll listener to detect when user manually scrolls
        if (chatScrollRect != null)
        {
            chatScrollRect.onValueChanged.AddListener(OnScrollValueChanged);
        }
    }
    
    private void OnDisable()
    {
        // Unsubscribe to prevent memory leaks
        if (networkManager != null)
        {
            networkManager.OnMessageReceived -= HandleMessageReceived;
        }
    }
    
    // Handle incoming messages from the network
    private void HandleMessageReceived(string message)
    {
        Debug.Log($"ChatPanel received message: {message}");
        AddMessage(message);
    }
    
    private void SetupCommandDropdown()
    {
        commandDropdown.ClearOptions();
        
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData> {
            new TMP_Dropdown.OptionData("Commands"),
            new TMP_Dropdown.OptionData("/help"),
            new TMP_Dropdown.OptionData("/create-room"),
            new TMP_Dropdown.OptionData("/join-room"),
            new TMP_Dropdown.OptionData("/list-rooms"),
            new TMP_Dropdown.OptionData("/users"),
            new TMP_Dropdown.OptionData("/dm"),
            new TMP_Dropdown.OptionData("/clear"),
            new TMP_Dropdown.OptionData("/logout")
        };
        
        commandDropdown.AddOptions(options);
        commandDropdown.value = 0;
    }
    
    public void OnCommandSelected(int index)
    {
        if (index == 0) return; // "Commands" label
        
        string command = commandDropdown.options[index].text;
        
        switch (command)
        {
            case "/clear":
                ClearChat();
                break;
                
            case "/create-room":
            case "/join-room":
            case "/dm":
                // Commands that need additional input
                messageInput.text = command + " ";
                messageInput.Select();
                // Fix for TextMeshPro InputField
                messageInput.caretPosition = messageInput.text.Length;
                break;
                
            case "/logout":
                networkManager.SendMessage("/logout");
                break;
                
            default:
                // Commands that can be sent directly
                networkManager.SendMessage(command);
                break;
        }
        
        // Reset dropdown
        commandDropdown.value = 0;
    }
    
    public void AddMessage(string message)
    {
        // Handle system messages vs chat messages
        if (message.StartsWith("/") || message.Contains("Server:") || 
            message.Contains("joined") || message.Contains("left"))
        {
            AddSystemMessage(message);
        }
        else
        {
            AddChatMessage(message);
        }
    }
    
    private void AddSystemMessage(string message)
    {
        if (messageBubblePrefab == null)
        {
            Debug.LogError("messageBubblePrefab is null! Can't display message: " + message);
            return;
        }
        
        if (messageContainer == null)
        {
            Debug.LogError("messageContainer is null! Can't display message: " + message);
            return;
        }
        
        Debug.Log($"Creating system message bubble for: {message}");
        GameObject bubble = Instantiate(messageBubblePrefab, messageContainer);
        TextMeshProUGUI messageText = bubble.GetComponentInChildren<TextMeshProUGUI>();
        
        if (messageText != null)
        {
            messageText.text = message;
            
            // Style system message differently
            messageText.color = Color.yellow;
            Debug.Log("Message text color set to yellow");
        }
        else
        {
            Debug.LogError("TextMeshProUGUI component not found on messageBubblePrefab");
        }
        
        messageObjects.Add(bubble);
        CleanupOldMessages();
        ScrollToBottom();
    }
    
    private void AddChatMessage(string message)
    {
        GameObject bubble = Instantiate(messageBubblePrefab, messageContainer);
        TextMeshProUGUI messageText = bubble.GetComponentInChildren<TextMeshProUGUI>(); // Changed from Text
        
        if (messageText != null)
        {
            messageText.text = message;
        }
        else
        {
            Debug.LogError("TextMeshProUGUI component not found on messageBubblePrefab");
        }
        
        messageObjects.Add(bubble);
        CleanupOldMessages();
        ScrollToBottom();
    }
    
    private void CleanupOldMessages()
    {
        if (messageObjects.Count > maxMessages)
        {
            Destroy(messageObjects[0]);
            messageObjects.RemoveAt(0);
        }
    }
    
    private void ScrollToBottom()
    {
        // Only auto-scroll if user hasn't manually scrolled up to read history
        if (!userHasScrolled)
        {
            StartCoroutine(ScrollToBottomNextFrame());
        }
    }

    private void OnScrollValueChanged(Vector2 scrollPos)
    {
        // If we're close to the bottom, consider it "not scrolled"
        // Remember: 0 is bottom, 1 is top in ScrollRect
        if (scrollPos.y < autoScrollThreshold)
        {
            userHasScrolled = false;
        }
        else
        {
            userHasScrolled = true;
        }
    }

    // Add a button to scroll to bottom when user wants to
    public void ForceScrollToBottom()
    {
        userHasScrolled = false;
        StartCoroutine(ScrollToBottomNextFrame());
    }

    private System.Collections.IEnumerator ScrollToBottomNextFrame()
    {
        // Wait for end of frame to ensure all layout calculations are complete
        yield return new WaitForEndOfFrame();
        
        // Force all canvases to update first
        Canvas.ForceUpdateCanvases();
        
        // For vertical layout, 0 is bottom, 1 is top
        chatScrollRect.normalizedPosition = new Vector2(0, 0);
        
        // Ensure the scroll has really updated
        Canvas.ForceUpdateCanvases();
        
        // Log for debugging
        Debug.Log("Scrolled to bottom of chat");
    }
    
    public void ClearChat()
    {
        foreach (GameObject msg in messageObjects)
        {
            Destroy(msg);
        }
        messageObjects.Clear();
    }
    
    public void OnSendClicked()
    {
        SendCurrentMessage();
    }
    
    public void OnInputSubmit(string text)
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            SendCurrentMessage();
        }
    }
    
    private void SendCurrentMessage()
    {
        string message = messageInput.text.Trim();
        
        if (string.IsNullOrEmpty(message)) return;
        
        Debug.Log($"Sending message: {message}");
        
        // Handle local command
        if (message.Equals("/clear", System.StringComparison.OrdinalIgnoreCase))
        {
            Debug.Log("Clearing chat via local command");
            ClearChat();
            messageInput.text = "";
            return;
        }
        
        // Show the message locally before sending (gives instant feedback)
        string localPreview = $"You: {message}";
        AddChatMessage(localPreview);
        
        // Send message to server
        if (networkManager != null)
        {
            networkManager.SendMessage(message);
            Debug.Log($"Message sent to NetworkManager: {message}");
        }
        else
        {
            Debug.LogError("NetworkManager is null when trying to send message");
            AddSystemMessage("Error: Not connected to server");
        }
        
        // Clear input field
        messageInput.text = "";
        messageInput.Select();
    }
    
    public void UpdateRoomInfo(string roomName, List<string> users)
    {
        if (currentRoomText != null)
        {
            currentRoomText.text = "Room: " + roomName;
        }
        
        if (usersInRoomText != null)
        {
            usersInRoomText.text = "Users: " + string.Join(", ", users);
        }
    }
}