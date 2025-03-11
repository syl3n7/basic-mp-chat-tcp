using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class ServerSelectionPanel : MonoBehaviour
{
    [Header("Server List")]
    public Transform serverListContainer;
    public GameObject serverItemPrefab;
    
    [Header("Manual Connection")]
    public TMP_InputField ipAddressInput;  // Changed to TMP_InputField
    public TMP_InputField portInput;       // Changed to TMP_InputField
    public Toggle secureConnectionToggle;
    public Button connectButton;
    
    private NetworkManager networkManager;
    
    private void OnEnable()
    {
        Debug.Log("ServerSelectionPanel enabled");
        
        // Get Network Manager
        networkManager = NetworkManager.Instance;
        
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager instance is null. Make sure it exists in the scene.");
            return;
        }
        
        // Default values
        if (ipAddressInput != null) ipAddressInput.text = "127.0.0.1";
        if (portInput != null) portInput.text = "12345";
        if (secureConnectionToggle != null) secureConnectionToggle.isOn = false;
        
        // Add connection state change listener
        networkManager.OnConnectionStateChanged += HandleConnectionStateChanged;
        
        // Add button click listener
        if (connectButton != null)
        {
            connectButton.onClick.RemoveAllListeners();
            connectButton.onClick.AddListener(OnConnectClicked);
            Debug.Log("Connect button listener added successfully");
        }
        else
        {
            Debug.LogError("Connect button reference is null");
        }
        
        // Populate server list
        PopulateServerList();
    }
    
    private void OnDisable()
    {
        // Clean up event subscription to prevent memory leaks
        if (networkManager != null)
        {
            networkManager.OnConnectionStateChanged -= HandleConnectionStateChanged;
        }
    }

    private void HandleConnectionStateChanged(bool connected)
    {
        Debug.Log($"Connection state changed: {(connected ? "Connected" : "Disconnected")}");
        
        // You might want to add UI feedback here
        // For example, change button color, show a message, etc.
    }
    
    private void PopulateServerList()
    {
        // Safety check for networkManager
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager is null in PopulateServerList");
            return;
        }
        
        // Clear existing items
        foreach (Transform child in serverListContainer)
        {
            Destroy(child.gameObject);
        }
        
        // Initialize savedServers if needed
        if (networkManager.savedServers == null)
        {
            networkManager.savedServers = new List<string>();
            Debug.Log("Initialized empty savedServers list");
            return; // No servers to add
        }
        
        // Add saved servers
        for (int i = 0; i < networkManager.savedServers.Count; i++)
        {
            string[] parts = networkManager.savedServers[i].Split(':');
            if (parts.Length < 2)
            {
                Debug.LogWarning($"Invalid server format at index {i}: {networkManager.savedServers[i]}");
                continue;
            }
            
            string ipAddress = parts[0];
            string port = parts[1];
            bool isSecure = parts.Length > 2 && parts[2] == "true";
            
            // Create server item
            GameObject item = Instantiate(serverItemPrefab, serverListContainer);
            ServerListItem serverItem = item.GetComponent<ServerListItem>();
            if (serverItem != null)
            {
                serverItem.Setup(ipAddress, port, isSecure, OnServerItemClicked);
            }
            else
            {
                Debug.LogError("ServerListItem component not found on serverItemPrefab");
            }
        }
    }
    
    public void OnConnectClicked()
    {
        Debug.Log("Connect button clicked!");
        
        string ip = ipAddressInput.text;
        if (string.IsNullOrEmpty(ip)) ip = "127.0.0.1";
        
        int port = 12345;
        if (!string.IsNullOrEmpty(portInput.text) && int.TryParse(portInput.text, out int customPort))
        {
            port = customPort;
        }
        
        bool useSecure = secureConnectionToggle.isOn;
        
        // Safety check
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager is null when trying to connect");
            return;
        }
        
        Debug.Log($"Attempting to connect to server: {ip}:{port} (Secure: {useSecure})");
        
        // Make sure this method exists in your NetworkManager class
        try
        {
            networkManager.ConnectToServer(ip, port, useSecure);
            Debug.Log("ConnectToServer method called successfully");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error calling ConnectToServer: {ex.Message}");
        }
    }
    
    private void OnServerItemClicked(string ip, int port, bool secure)
    {
        // Update input fields
        ipAddressInput.text = ip;
        portInput.text = port.ToString();
        secureConnectionToggle.isOn = secure;
    }
}