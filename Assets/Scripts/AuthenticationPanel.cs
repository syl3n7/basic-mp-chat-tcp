using UnityEngine;
using UnityEngine.UI;
using TMPro; 
using System.Collections.Generic;

public class AuthenticationPanel : MonoBehaviour
{
    [Header("Login")]
    public GameObject loginPanel;
    public TMP_Dropdown savedUsersDropdown;
    public TMP_InputField loginUsernameInput;
    public TMP_InputField loginPasswordInput;
    public Button loginButton;
    public TextMeshProUGUI loginErrorText; // Add this field
    
    [Header("Register")]
    public GameObject registerPanel;
    public TMP_InputField registerUsernameInput;
    public TMP_InputField registerPasswordInput;
    public TMP_InputField confirmPasswordInput;
    public Button registerButton;
    public TextMeshProUGUI registerErrorText; // Add this field
    
    [Header("Toggle")]
    public Button switchToLoginButton;
    public Button switchToRegisterButton;
    
    private NetworkManager networkManager;
    
    private void OnEnable()
    {
        networkManager = NetworkManager.Instance;
        if (networkManager == null)
        {
            Debug.LogError("NetworkManager.Instance is null!");
            return;
        }
        
        // Subscribe to authentication events
        networkManager.OnAuthenticationResult += HandleAuthenticationResult;
        
        // Clear inputs
        loginUsernameInput.text = "";
        loginPasswordInput.text = "";
        registerUsernameInput.text = "";
        registerPasswordInput.text = "";
        confirmPasswordInput.text = "";
        
        // Clear error messages
        if (loginErrorText != null) loginErrorText.text = "";
        if (registerErrorText != null) registerErrorText.text = "";
        
        // Set up saved users dropdown
        PopulateSavedUsers();
        
        // Start with login panel active
        ShowLoginPanel();
        
        // Set up button listeners
        loginButton.onClick.RemoveAllListeners();
        loginButton.onClick.AddListener(OnLoginClicked);
        
        registerButton.onClick.RemoveAllListeners();
        registerButton.onClick.AddListener(OnRegisterClicked);
        
        if (switchToLoginButton != null)
        {
            switchToLoginButton.onClick.RemoveAllListeners();
            switchToLoginButton.onClick.AddListener(ShowLoginPanel);
        }

        if (switchToRegisterButton != null)
        {
            switchToRegisterButton.onClick.RemoveAllListeners();
            switchToRegisterButton.onClick.AddListener(ShowRegisterPanel);
        }
        
        savedUsersDropdown.onValueChanged.RemoveAllListeners();
        savedUsersDropdown.onValueChanged.AddListener(OnSavedUserSelected);
        
        Debug.Log("AuthenticationPanel enabled and initialized");
    }
    
    private void OnDisable()
    {
        // Unsubscribe from events
        if (networkManager != null)
        {
            networkManager.OnAuthenticationResult -= HandleAuthenticationResult;
        }
    }
    
    private void HandleAuthenticationResult(bool success, string message)
    {
        Debug.Log($"Authentication result: {success}, Message: {message}");
        
        if (success)
        {
            // Authentication successful, handle the success case
            // For example, transition to the game scene or show chat panel
            Debug.Log("Authentication successful!");
        }
        else
        {
            // Display error message in the appropriate panel
            if (loginPanel.activeSelf && loginErrorText != null)
            {
                loginErrorText.text = message;
                Debug.Log("Login failed: " + message);
            }
            else if (registerPanel.activeSelf && registerErrorText != null)
            {
                registerErrorText.text = message;
                Debug.Log("Registration failed: " + message);
            }
        }
    }
    
    private void PopulateSavedUsers()
    {
        savedUsersDropdown.ClearOptions();
        
        List<TMP_Dropdown.OptionData> options = new List<TMP_Dropdown.OptionData>();
        options.Add(new TMP_Dropdown.OptionData("Select a saved user"));
        
        if (networkManager != null && networkManager.savedUsers != null)
        {
            foreach (string user in networkManager.savedUsers)
            {
                options.Add(new TMP_Dropdown.OptionData(user));
            }
        }
        
        options.Add(new TMP_Dropdown.OptionData("Use a new username"));
        
        savedUsersDropdown.AddOptions(options);
        savedUsersDropdown.value = 0;
    }
    
    private void OnSavedUserSelected(int index)
    {
        if (index == 0) // "Select a saved user"
        {
            loginUsernameInput.text = "";
        }
        else if (index == savedUsersDropdown.options.Count - 1) // "Use a new username"
        {
            loginUsernameInput.text = "";
            loginUsernameInput.Select();
        }
        else // A saved user was selected
        {
            loginUsernameInput.text = networkManager.savedUsers[index - 1];
            loginPasswordInput.Select();
        }
        
        // Clear any previous error message
        if (loginErrorText) loginErrorText.text = "";
    }
    
    public void ShowLoginPanel()
    {
        loginPanel.SetActive(true);
        registerPanel.SetActive(false);
        
        // Clear any previous error message
        if (loginErrorText) loginErrorText.text = "";
    }
    
    public void ShowRegisterPanel()
    {
        loginPanel.SetActive(false);
        registerPanel.SetActive(true);
        
        // Clear any previous error message
        if (registerErrorText) registerErrorText.text = "";
    }
    
    public void OnLoginClicked()
    {
        Debug.Log("Login button clicked");
        
        string username = loginUsernameInput.text;
        string password = loginPasswordInput.text;
        
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            // Show error in UI
            if (loginErrorText)
                loginErrorText.text = "Please enter both username and password.";
            Debug.LogWarning("Login attempted with empty fields");
            return;
        }
        
        // Clear any previous error message
        if (loginErrorText) loginErrorText.text = "Authenticating...";
        
        if (networkManager != null)
        {
            Debug.Log($"Sending login request for user: {username}");
            networkManager.Authenticate(true, username, password);
        }
        else
        {
            Debug.LogError("NetworkManager is null when trying to login");
            if (loginErrorText) loginErrorText.text = "Error: Network manager not found";
        }
    }
    
    public void OnRegisterClicked()
    {
        Debug.Log("Register button clicked");
        
        string username = registerUsernameInput.text;
        string password = registerPasswordInput.text;
        string confirmPassword = confirmPasswordInput.text;
        
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            // Show error in UI
            if (registerErrorText)
                registerErrorText.text = "Please enter both username and password.";
            Debug.LogWarning("Registration attempted with empty fields");
            return;
        }
        
        if (password != confirmPassword)
        {
            // Show error that passwords don't match
            if (registerErrorText)
                registerErrorText.text = "Passwords don't match.";
            Debug.LogWarning("Registration attempted with mismatched passwords");
            return;
        }
        
        // Validate minimum password length
        if (password.Length < 6)
        {
            if (registerErrorText)
                registerErrorText.text = "Password must be at least 6 characters.";
            Debug.LogWarning("Registration attempted with short password");
            return;
        }
        
        // Clear any previous error message
        if (registerErrorText) registerErrorText.text = "Registering...";
        
        if (networkManager != null)
        {
            Debug.Log($"Sending registration request for user: {username}");
            networkManager.Authenticate(false, username, password);
        }
        else
        {
            Debug.LogError("NetworkManager is null when trying to register");
            if (registerErrorText) registerErrorText.text = "Error: Network manager not found";
        }
    }
}