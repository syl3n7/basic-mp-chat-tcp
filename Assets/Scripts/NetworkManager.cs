using UnityEngine;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Security.Authentication;
using System.Collections.Concurrent;

public class NetworkManager : MonoBehaviour
{
    // Singleton pattern
    public static NetworkManager Instance { get; private set; }
    
    // Connection state
    private bool isRunning = false;
    private bool isAuthenticated = false;
    private TcpClient client;
    private Stream communicationStream;
    private Thread receiveThread;

    // For thread-safe event invocation
    private bool isConnectionStateChanged = false;
    private bool newConnectionState = false;

    // Message queue for thread-safe communication
    private ConcurrentQueue<string> incomingMessages = new ConcurrentQueue<string>();
    
    // Saved data
    private string savedUsersFile = "savedUsers.txt";
    private string savedServersFile = "savedServers.txt";
    public List<string> savedUsers = new List<string>();
    public List<string> savedServers = new List<string>(); // Format: "ip:port:secure"
    
    // Events
    public delegate void MessageReceivedHandler(string message);
    public event MessageReceivedHandler OnMessageReceived;
    
    public delegate void ConnectionStateChangedHandler(bool connected);
    public event ConnectionStateChangedHandler OnConnectionStateChanged;
    
    public delegate void AuthenticationResultHandler(bool success, string message);
    public event AuthenticationResultHandler OnAuthenticationResult;

    // Add this field to queue server entries for saving on the main thread
    private ConcurrentQueue<string> serverEntriesToSave = new ConcurrentQueue<string>();

    // Add these thread-safe fields at the top of your class
    private ConcurrentQueue<Action> mainThreadActions = new ConcurrentQueue<Action>();
    private bool disconnectQueued = false;

    private void Awake()
    {
        // Singleton pattern implementation
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadSavedData();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Update()
    {
        // Process any queued messages on main thread
        ProcessQueuedMessages();
        
        // Process connection state changes on main thread
        if (isConnectionStateChanged)
        {
            isConnectionStateChanged = false;
            OnConnectionStateChanged?.Invoke(newConnectionState);
            Debug.Log($"Connection state changed: {(newConnectionState ? "Connected" : "Disconnected")}");
        }
        
        // Process server entries to save on main thread
        while (serverEntriesToSave.TryDequeue(out string serverEntry))
        {
            SaveServerEntryInternal(serverEntry);
        }

        // Process any actions that need to run on the main thread
        while (mainThreadActions.TryDequeue(out Action action))
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error executing queued action: {ex.Message}");
            }
        }
        
        // Process disconnect request
        if (disconnectQueued)
        {
            disconnectQueued = false;
            DisconnectFromServerInternal();
        }
    }
    
    private void ProcessQueuedMessages()
    {
        while (incomingMessages.TryDequeue(out string message))
        {
            // Log message for debugging
            Debug.Log($"Received message: {message}");
            
            // Check for special messages
            if (message.Contains("You have been kicked") || 
                message.Contains("Your account is banned"))
            {
                DisconnectFromServer();
            }
            
            // Trigger the message received event
            if (OnMessageReceived != null)
            {
                OnMessageReceived(message);
                Debug.Log("OnMessageReceived event fired");
            }
            else
            {
                Debug.LogWarning("No listeners registered for OnMessageReceived event");
            }
        }
    }
    
    // Connect to server
    public void ConnectToServer(string ipAddress, int port, bool useSecureConnection)
    {
        // Log on main thread before starting connection
        Debug.Log($"Starting connection to: {ipAddress}:{port} (Secure: {useSecureConnection})");
        
        // Start in a separate thread to avoid blocking the UI
        Thread connectionThread = new Thread(() => {
            try
            {
                // Queue a message instead of direct Debug.Log in thread
                incomingMessages.Enqueue($"Connecting to {ipAddress}:{port} (Secure: {useSecureConnection})...");
                
                // Clean up any existing connections
                if (client != null)
                {
                    DisconnectFromServer();
                }
                
                client = new TcpClient();
                
                // Set connection timeout (5 seconds)
                incomingMessages.Enqueue("Establishing connection...");
                IAsyncResult ar = client.BeginConnect(ipAddress, port, null, null);
                bool connected = ar.AsyncWaitHandle.WaitOne(5000); // 5 second timeout
                
                if (!connected)
                {
                    throw new TimeoutException("Connection attempt timed out. Server may be offline.");
                }
                
                client.EndConnect(ar);
                incomingMessages.Enqueue("TCP connection established!");
                
                // Create appropriate stream
                Stream baseStream = client.GetStream();
                
                if (useSecureConnection)
                {
                    incomingMessages.Enqueue("Establishing secure connection...");
                    // Create secure stream
                    SslStream sslStream = new SslStream(baseStream, false, ValidateServerCertificate);
                    
                    try
                    {
                        sslStream.AuthenticateAsClient(ipAddress);
                        incomingMessages.Enqueue("Secure connection established!");
                    }
                    catch (AuthenticationException e)
                    {
                        incomingMessages.Enqueue($"SSL Authentication failed: {e.Message}");
                        client.Close();
                        return;
                    }
                    
                    communicationStream = sslStream;
                }
                else
                {
                    // Use standard network stream
                    communicationStream = baseStream;
                    incomingMessages.Enqueue("Using standard (unencrypted) connection");
                }
                
                isRunning = true;
                
                // Read welcome message
                incomingMessages.Enqueue("Waiting for welcome message...");
                byte[] buffer = new byte[1024];
                int bytesRead = communicationStream.Read(buffer, 0, buffer.Length);
                string welcomeMessage = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                incomingMessages.Enqueue(welcomeMessage);
                
                // Notify UI that connection is established
                // This will happen on the next Update() call
                incomingMessages.Enqueue("===CONNECTION SUCCESSFUL===");
    
                // We'll call this on the main thread in Update()
                isConnectionStateChanged = true;
                newConnectionState = true;
                
                // Start receive thread
                receiveThread = new Thread(ReceiveMessages);
                receiveThread.IsBackground = true;
                receiveThread.Start();
                
                // Queue server entry for saving on main thread instead of calling directly
                string serverEntry = $"{ipAddress}:{port}:{useSecureConnection.ToString().ToLower()}";
                serverEntriesToSave.Enqueue(serverEntry);
            }
            catch (Exception ex)
            {
                incomingMessages.Enqueue($"Connection error: {ex.Message}");
                isRunning = false;
                
                // We'll call this on the main thread in Update()
                isConnectionStateChanged = true;
                newConnectionState = false;
            }
        });
        
        connectionThread.Start();
    }
    
    // Send authentication request
    public void Authenticate(bool isLogin, string username, string password)
    {
        if (!isRunning || client == null || !client.Connected)
        {
            incomingMessages.Enqueue("Not connected to server.");
            return;
        }
        
        try
        {
            string command = isLogin ? "/login " : "/register ";
            command += $"{username}:{password}";
            
            byte[] data = Encoding.UTF8.GetBytes(command);
            communicationStream.Write(data, 0, data.Length);
            
            // Authentication response will be processed in the receive thread
            // and authentication success/failure will be determined there
        }
        catch (Exception ex)
        {
            incomingMessages.Enqueue($"Authentication error: {ex.Message}");
        }
    }
    
    // Send chat message or command
    public void SendMessage(string message)
    {
        if (!isRunning || client == null || !client.Connected)
        {
            incomingMessages.Enqueue("Not connected to server.");
            return;
        }
        
        try
        {
            if (message.Equals("/quit", StringComparison.OrdinalIgnoreCase) || 
                message.Equals("/logout", StringComparison.OrdinalIgnoreCase))
            {
                byte[] data = Encoding.UTF8.GetBytes(message);
                communicationStream.Write(data, 0, data.Length);
                DisconnectFromServer();
                return;
            }
            
            byte[] messageData = Encoding.UTF8.GetBytes(message);
            communicationStream.Write(messageData, 0, messageData.Length);
        }
        catch (Exception ex)
        {
            incomingMessages.Enqueue($"Error sending message: {ex.Message}");
            DisconnectFromServer();
        }
    }
    
    // Receive thread function
    private void ReceiveMessages()
    {
        byte[] buffer = new byte[1024];
        int bytesRead;
        
        try
        {
            while (isRunning && client != null && client.Connected)
            {
                bytesRead = communicationStream.Read(buffer, 0, buffer.Length);
                
                if (bytesRead == 0)
                {
                    QueueOnMainThread(() => {
                        incomingMessages.Enqueue("Server disconnected.");
                    });
                    DisconnectFromServer();
                    break;
                }
                
                string message = Encoding.UTF8.GetString(buffer, 0, bytesRead);
                
                // Check for authentication response
                if (!isAuthenticated && (message.Contains("Login successful") || 
                                        message.Contains("Registration successful")))
                {
                    isAuthenticated = true;
                    
                    // Queue authentication success for main thread
                    QueueOnMainThread(() => {
                        Debug.Log("Authentication successful: " + message);
                        if (OnAuthenticationResult != null)
                        {
                            try {
                                OnAuthenticationResult(true, message);
                            } catch (Exception e) {
                                Debug.LogError("Error in authentication event handler: " + e.Message);
                            }
                        }
                    });
                    
                    // Extract username and save if needed - do this on main thread
                    string username = ExtractUsernameFromMessage(message);
                    if (!string.IsNullOrEmpty(username))
                    {
                        QueueOnMainThread(() => {
                            if (!savedUsers.Contains(username))
                            {
                                savedUsers.Add(username);
                                SaveUsers();
                            }
                        });
                    }
                }
                else if (!isAuthenticated && (message.Contains("Login failed") || 
                                            message.Contains("Registration failed")))
                {
                    // Queue authentication failure for main thread
                    QueueOnMainThread(() => {
                        Debug.Log("Authentication failed: " + message);
                        if (OnAuthenticationResult != null)
                        {
                            try {
                                OnAuthenticationResult(false, message);
                            } catch (Exception e) {
                                Debug.LogError("Error in authentication event handler: " + e.Message);
                            }
                        }
                    });
                }
                
                // Queue message for processing on main thread
                QueueOnMainThread(() => {
                    incomingMessages.Enqueue(message);
                });
            }
        }
        catch (Exception ex)
        {
            QueueOnMainThread(() => {
                incomingMessages.Enqueue($"Receive error: {ex.Message}");
            });
            DisconnectFromServer();
        }
    }
    
    // Disconnect from server
    public void DisconnectFromServer()
    {
        // If we're on the main thread, disconnect immediately
        if (Thread.CurrentThread.ManagedThreadId == 1) // Unity's main thread id is usually 1
        {
            DisconnectFromServerInternal();
        }
        else
        {
            // Otherwise queue it for the main thread
            disconnectQueued = true;
        }
    }
    
    // Create a new internal method that actually does the disconnection
    private void DisconnectFromServerInternal()
    {
        isRunning = false;
        isAuthenticated = false;
        
        if (client != null && client.Connected)
        {
            try
            {
                if (communicationStream != null)
                {
                    communicationStream.Close();
                }
                client.Close();
            }
            catch (Exception ex)
            {
                Debug.LogError($"Error during disconnect: {ex.Message}");
            }
        }
        
        client = null;
        communicationStream = null;
        
        // Notify UI that connection is closed
        OnConnectionStateChanged?.Invoke(false);
    }
    
    // Certificate validation callback
    private bool ValidateServerCertificate(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
    {
        // For Unity UI, we'll accept all certificates by default
        // In a production app, you'd want to show a dialog for user confirmation
        return true;
    }
    
    // Helper to extract username from server message - adapt to your server's format
    private string ExtractUsernameFromMessage(string message)
    {
        // This is a placeholder - actual implementation depends on your server's response format
        return "";
    }
    
    // Load saved users
    private void LoadSavedData()
    {
        LoadSavedUsers();
        LoadSavedServers();
    }
    
    private void LoadSavedUsers()
    {
        try
        {
            string filePath = Path.Combine(Application.persistentDataPath, savedUsersFile);
            if (File.Exists(filePath))
            {
                savedUsers = new List<string>(File.ReadAllLines(filePath));
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading saved users: {ex.Message}");
        }
    }
    
    private void LoadSavedServers()
    {
        try
        {
            string filePath = Path.Combine(Application.persistentDataPath, savedServersFile);
            if (File.Exists(filePath))
            {
                savedServers = new List<string>(File.ReadAllLines(filePath));
                
                // Convert old format to new format if needed
                for (int i = 0; i < savedServers.Count; i++)
                {
                    string[] parts = savedServers[i].Split(':');
                    if (parts.Length == 2)
                    {
                        savedServers[i] = $"{parts[0]}:{parts[1]}:false";
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error loading saved servers: {ex.Message}");
        }
    }
    
    // Save users
    public void SaveUsers()
    {
        try
        {
            string filePath = Path.Combine(Application.persistentDataPath, savedUsersFile);
            File.WriteAllLines(filePath, savedUsers);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error saving users: {ex.Message}");
        }
    }
    
    // Save servers - only called from main thread
    public void SaveServers()
    {
        try
        {
            string filePath = Path.Combine(Application.persistentDataPath, savedServersFile);
            File.WriteAllLines(filePath, savedServers);
            Debug.Log($"Servers saved to {filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"Error saving servers: {ex.Message}");
        }
    }
    
    // Save a specific server entry - this is called from the worker thread
    public void SaveServerEntry(string ipAddress, int port, bool useSecureConnection)
    {
        string serverEntry = $"{ipAddress}:{port}:{useSecureConnection.ToString().ToLower()}";
        serverEntriesToSave.Enqueue(serverEntry);
    }
    
    // This internal method is only called from the main thread
    private void SaveServerEntryInternal(string serverEntry)
    {
        // Check if this server configuration is already saved
        bool serverAlreadySaved = false;
        foreach (string server in savedServers)
        {
            string[] parts = server.Split(':');
            if (parts.Length >= 2 && parts[0] == serverEntry.Split(':')[0] && 
                parts[1] == serverEntry.Split(':')[1])
            {
                serverAlreadySaved = true;
                break;
            }
        }
        
        if (!serverAlreadySaved)
        {
            savedServers.Add(serverEntry);
            SaveServers();
        }
    }
    
    private void OnDestroy()
    {
        DisconnectFromServer();
    }

    // Helper method to run actions on the main thread
    private void QueueOnMainThread(Action action)
    {
        if (action != null)
        {
            mainThreadActions.Enqueue(action);
        }
    }
}