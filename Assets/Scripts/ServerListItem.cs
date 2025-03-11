using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ServerListItem : MonoBehaviour
{
    public TMP_Text serverInfoText;
    public Button selectButton;
    
    private string ipAddress;
    private int port;
    private bool isSecure;
    
    public delegate void ServerItemClickHandler(string ip, int port, bool secure);
    private ServerItemClickHandler clickHandler;
    
    public void Setup(string ip, string portStr, bool secure, ServerItemClickHandler handler)
    {
        ipAddress = ip;
        
        if (!int.TryParse(portStr, out port))
        {
            Debug.LogError($"Invalid port number: {portStr}");
            port = 12345; // Default port as fallback
        }
        
        isSecure = secure;
        clickHandler = handler;
        
        // Update UI
        string secureLabel = secure ? " (Secure)" : "";
        if (serverInfoText != null)
        {
            serverInfoText.text = $"{ipAddress}:{port}{secureLabel}";
        }
        else
        {
            Debug.LogError("serverInfoText is null in ServerListItem");
        }
        
        // Setup button
        if (selectButton != null)
        {
            selectButton.onClick.AddListener(OnClick);
        }
        else
        {
            Debug.LogError("selectButton is null in ServerListItem");
        }
    }
    
    private void OnClick()
    {
        clickHandler?.Invoke(ipAddress, port, isSecure);
    }
}