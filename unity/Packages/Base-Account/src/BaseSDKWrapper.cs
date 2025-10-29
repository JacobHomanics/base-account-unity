using UnityEngine;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;

/// <summary>
/// Unity wrapper for Base SDK integration in WebGL builds.
/// Provides functionality to initialize the Base SDK, connect wallets, manage sub-accounts,
/// and send transactions through JavaScript interop.
/// 
/// Key Features:
/// - SDK initialization with configurable app settings
/// - Wallet connection and address management
/// - Sub-account creation and retrieval
/// - Transaction sending with multiple calls support
/// - Event-driven architecture for async operations
/// - WebGL-only platform support with fallback warnings
/// </summary>
public class BaseSDKWrapper : MonoBehaviour
{
    [Header("SDK Configuration")]
    [SerializeField] private string appName = "My Unity Game";           // Application name for SDK identification
    [SerializeField] private string network = "basesepolia";             // Base network to connect to
    [SerializeField] private string customRpcUrl = "";                  // Optional custom RPC endpoint
    [SerializeField] private string paymasterUrl = "https://paymaster.base.org";  // Paymaster service URL
    [SerializeField] private string paymasterPolicy = "VERIFYING_PAYMASTER";      // Paymaster policy type

    /// <summary>
    /// Represents a single transaction call with target address and data payload
    /// </summary>
    [System.Serializable]
    public class TransactionCall
    {
        public string to;    // Target contract address
        public string data;  // Encoded function call data
    }

    /// <summary>
    /// Container for multiple transaction calls to be sent in a single transaction
    /// </summary>
    [System.Serializable]
    private class TransactionCallsList
    {
        public List<TransactionCall> calls;
    }

    /// <summary>
    /// Configuration object for SDK initialization containing app settings and service configs
    /// </summary>
    [System.Serializable]
    private class SDKConfig
    {
        public string appName;                    // Application identifier
        public SubAccountsConfig subAccounts;     // Sub-account configuration
        public PaymasterConfig paymaster;         // Paymaster service configuration
    }

    /// <summary>
    /// Configuration for sub-account creation and management
    /// </summary>
    [System.Serializable]
    private class SubAccountsConfig
    {
        public string creation;       // When to create sub-accounts ("on-connect")
        public string defaultAccount; // Default account type ("sub")
    }

    /// <summary>
    /// Configuration for paymaster service integration
    /// </summary>
    [System.Serializable]
    private class PaymasterConfig
    {
        public string url;    // Paymaster service endpoint
        public string policy; // Paymaster policy type
    }

    // ===== STATE VARIABLES =====
    private bool isInitialized = false;                    // SDK initialization status
    private string[] connectedAddresses = new string[0];   // Array of connected wallet addresses
    private string subAccountAddress = null;               // Current sub-account address
    private bool isWebGL = false;                          // Platform detection flag

    // ===== EVENTS =====
    /// <summary>Fired when a transaction is successfully sent, providing the transaction hash</summary>
    public event Action<string> OnTransactionSent;
    /// <summary>Fired when SDK initialization completes, indicating success/failure</summary>
    public event Action<bool> OnSDKReady;
    /// <summary>Fired when wallet connection completes, providing connected addresses</summary>
    public event Action<string[]> OnWalletReady;
    /// <summary>Fired when sub-account retrieval completes, providing the sub-account address</summary>
    public event Action<string> OnSubAccountReady;

    // ===== JAVASCRIPT INTEROP DECLARATIONS =====
    // These methods interface with the JavaScript SDK implementation in BaseSDKWrapper.jslib
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    private static extern void InitSDK(string configJson, string network, string customRpcUrl);

    [DllImport("__Internal")]
    private static extern void ConnectWallet();

    [DllImport("__Internal")]
    private static extern void GetSubAccount();

    [DllImport("__Internal")]
    private static extern void SendTransaction(string callsJson, string chainIdOverride);

    [DllImport("__Internal")]
    private static extern string GetCurrentNetworkJSON();
#endif

    // ===== UNITY LIFECYCLE METHODS =====

    /// <summary>
    /// Unity Awake method - detects WebGL platform and warns if not supported
    /// </summary>
    void Awake()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        isWebGL = true;
#endif
        if (!isWebGL)
        {
            Debug.LogWarning("BaseSDKWrapper only works in WebGL builds.");
        }
    }

    /// <summary>
    /// Unity Start method - automatically initializes SDK if running on WebGL
    /// </summary>
    void Start()
    {
        if (isWebGL)
        {
            InitializeSDK();
        }
    }

    // ===== SDK INITIALIZATION =====

    /// <summary>
    /// Initializes the Base SDK with configured settings.
    /// Creates SDK configuration and calls JavaScript initialization.
    /// Automatically triggers wallet connection upon successful initialization.
    /// </summary>
    public void InitializeSDK()
    {
        if (!isWebGL)
        {
            Debug.LogError("Cannot initialize SDK on non-WebGL platform.");
            return;
        }

        if (isInitialized)
        {
            Debug.LogWarning("SDK already initialized.");
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        // Build SDK configuration from inspector settings
        SDKConfig config = new SDKConfig
        {
            appName = appName,
            subAccounts = new SubAccountsConfig
            {
                creation = "on-connect",      // Create sub-accounts when wallet connects
                defaultAccount = "sub"        // Use sub-account as default
            },
            paymaster = new PaymasterConfig
            {
                url = paymasterUrl,           // Paymaster service endpoint
                policy = paymasterPolicy      // Paymaster policy type
            }
        };

        string configJson = JsonUtility.ToJson(config);
        Debug.Log($"Initializing SDK with config: {configJson}");
        
        // Call JavaScript SDK initialization
        InitSDK(configJson, network, string.IsNullOrEmpty(customRpcUrl) ? null : customRpcUrl);
#endif
    }

    /// <summary>
    /// Callback method invoked by JavaScript when SDK initialization completes.
    /// Updates initialization state and triggers wallet connection on success.
    /// </summary>
    /// <param name="result">Initialization result ("success" or error message)</param>
    private void OnSDKInitialized(string result)
    {
        if (result == "success")
        {
            isInitialized = true;
            Debug.Log("SDK initialized successfully!");
            OnSDKReady?.Invoke(true);

            // Auto-connect wallet after SDK init
            ConnectWalletAsync();
        }
        else
        {
            Debug.LogError("SDK initialization failed!");
            OnSDKReady?.Invoke(false);
        }
    }

    // ===== WALLET CONNECTION =====

    /// <summary>
    /// Initiates wallet connection process.
    /// Requires SDK to be initialized first.
    /// </summary>
    public void ConnectWalletAsync()
    {
        if (!isWebGL)
        {
            Debug.LogError("Cannot connect wallet on non-WebGL platform.");
            return;
        }

        if (!isInitialized)
        {
            Debug.LogError("SDK not initialized. Call InitializeSDK first.");
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log("Connecting wallet...");
        ConnectWallet();
#endif
    }

    /// <summary>
    /// Callback method invoked by JavaScript when wallet connection completes.
    /// Parses connected addresses and automatically retrieves sub-account.
    /// </summary>
    /// <param name="addressesJson">JSON array of connected wallet addresses</param>
    private void OnWalletConnected(string addressesJson)
    {
        if (string.IsNullOrEmpty(addressesJson))
        {
            Debug.LogError("Wallet connection failed!");
            connectedAddresses = new string[0];
            OnWalletReady?.Invoke(connectedAddresses);
            return;
        }

        try
        {
            connectedAddresses = JsonHelper.FromJson<string>(addressesJson);
            Debug.Log($"Wallet connected! Addresses: {string.Join(", ", connectedAddresses)}");
            OnWalletReady?.Invoke(connectedAddresses);

            // Auto-retrieve sub-account after wallet connection
            GetSubAccountAsync();
        }
        catch (Exception e)
        {
            Debug.LogError($"Error parsing addresses: {e.Message}");
            connectedAddresses = new string[0];
            OnWalletReady?.Invoke(connectedAddresses);
        }
    }

    // ===== SUB-ACCOUNT MANAGEMENT =====

    /// <summary>
    /// Retrieves the current sub-account address.
    /// Requires SDK to be initialized and wallet connected.
    /// </summary>
    public void GetSubAccountAsync()
    {
        if (!isWebGL)
        {
            Debug.LogError("Cannot get sub-account on non-WebGL platform.");
            return;
        }

        if (!isInitialized)
        {
            Debug.LogError("SDK not initialized.");
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        Debug.Log("Retrieving sub-account...");
        GetSubAccount();
#endif
    }

    /// <summary>
    /// Callback method invoked by JavaScript when sub-account retrieval completes.
    /// Updates the sub-account address and notifies listeners.
    /// </summary>
    /// <param name="address">The retrieved sub-account address</param>
    private void OnSubAccountRetrieved(string address)
    {
        if (string.IsNullOrEmpty(address))
        {
            Debug.LogError("Sub-account retrieval failed!");
            subAccountAddress = null;
        }
        else
        {
            subAccountAddress = address;
            Debug.Log($"Sub-account retrieved: {subAccountAddress}");
        }

        OnSubAccountReady?.Invoke(subAccountAddress);
    }

    // ===== TRANSACTION MANAGEMENT =====

    /// <summary>
    /// Sends a transaction with multiple calls to the blockchain.
    /// Requires SDK initialization, wallet connection, and sub-account availability.
    /// </summary>
    /// <param name="calls">List of transaction calls to execute</param>
    /// <param name="chainIdOverride">Optional chain ID override for the transaction</param>
    public void SendTransactionAsync(List<TransactionCall> calls, string chainIdOverride = null)
    {
        if (!isWebGL)
        {
            Debug.LogError("Cannot send transaction on non-WebGL platform.");
            return;
        }

        if (!isInitialized || string.IsNullOrEmpty(subAccountAddress))
        {
            Debug.LogError("SDK not ready or sub-account not available.");
            return;
        }

        if (calls == null || calls.Count == 0)
        {
            Debug.LogError("No transaction calls provided.");
            return;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
    // Create a proper JSON array directly
    string callsJson = "[";
    for (int i = 0; i < calls.Count; i++)
    {
        if (i > 0) callsJson += ",";
        callsJson += $"{{\"to\":\"{calls[i].to}\",\"data\":\"{calls[i].data}\"}}";
    }
    callsJson += "]";
    
    Debug.Log($"Sending transaction with calls: {callsJson}");
    SendTransaction(callsJson, chainIdOverride);
#endif
    }

    /// <summary>
    /// Callback method invoked by JavaScript when transaction completes.
    /// Notifies listeners with the transaction hash on success.
    /// </summary>
    /// <param name="txHash">Transaction hash on success, empty/null on failure</param>
    private void OnTransactionComplete(string txHash)
    {
        if (string.IsNullOrEmpty(txHash))
        {
            Debug.LogError("Transaction failed!");
            return;
        }

        Debug.Log($"Transaction successful! Hash: {txHash}");
        OnTransactionSent?.Invoke(txHash);
    }

    // ===== UTILITY METHODS =====

    /// <summary>
    /// Gets the current network information as JSON string.
    /// Returns null if not running on WebGL or SDK not initialized.
    /// </summary>
    /// <returns>JSON string containing network information or null</returns>
    public string GetCurrentNetwork()
    {
        if (!isWebGL || !isInitialized)
        {
            return null;
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        return GetCurrentNetworkJSON();
#else
        return null;
#endif
    }

    /// <summary>
    /// Gets the array of currently connected wallet addresses.
    /// </summary>
    /// <returns>Array of connected wallet addresses</returns>
    public string[] GetConnectedAddresses()
    {
        return connectedAddresses;
    }

    /// <summary>
    /// Gets the current sub-account address.
    /// </summary>
    /// <returns>Sub-account address or null if not available</returns>
    public string GetSubAccountAddress()
    {
        return subAccountAddress;
    }

    /// <summary>
    /// Checks if the SDK has been successfully initialized.
    /// </summary>
    /// <returns>True if SDK is initialized, false otherwise</returns>
    public bool IsInitialized()
    {
        return isInitialized;
    }
}

// ===== HELPER CLASSES =====

/// <summary>
/// Helper class for JSON array deserialization.
/// Unity's JsonUtility doesn't support direct array deserialization,
/// so this wrapper provides a workaround for parsing JSON arrays.
/// </summary>
public static class JsonHelper
{
    /// <summary>
    /// Deserializes a JSON array into a typed array.
    /// </summary>
    /// <typeparam name="T">Type of array elements</typeparam>
    /// <param name="json">JSON array string</param>
    /// <returns>Array of deserialized objects</returns>
    public static T[] FromJson<T>(string json)
    {
        Wrapper<T> wrapper = JsonUtility.FromJson<Wrapper<T>>("{\"Items\":" + json + "}");
        return wrapper.Items;
    }

    /// <summary>
    /// Internal wrapper class for JSON array deserialization
    /// </summary>
    /// <typeparam name="T">Type of array elements</typeparam>
    [Serializable]
    private class Wrapper<T>
    {
        public T[] Items;
    }
}