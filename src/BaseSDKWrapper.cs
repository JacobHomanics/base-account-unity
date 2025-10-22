/*
 * BaseSDKWrapper.cs
 * 
 * Unity WebGL wrapper for Base Account SDK integration
 * 
 * This script provides a Unity-friendly interface to interact with the Base Account SDK
 * through JavaScript interop. It handles SDK initialization, wallet connection, sub-account
 * management, and transaction sending on any Ethereum-compatible network.
 * For now, we will support only Base Sepolia and Base Mainnet. (Support for other networks will be added later)
 * 
 * Key Features:
 * - WebGL-only support with automatic platform detection
 * - Configurable network settings (Base Mainnet/Sepolia)
 * - Custom RPC URL support for testing environments
 * - Paymaster integration for gasless transactions
 * - Sub-account creation and management
 * - Transaction sending with multiple call support
 * - Event-driven architecture for transaction completion
 * 
 * Usage:
 * 1. Attach this script to a GameObject in your Unity scene
 * 2. Configure the SDK settings in the inspector
 * 3. The SDK will automatically initialize on Start() for WebGL builds
 * 4. Subscribe to OnTransactionSent event for transaction completion handling
 * 
 * Dependencies:
 * - Base Account SDK jslib wrapper (BaseSDKWrapper.jslib)
 * - Unity WebGL platform
 * - Internet connection for SDK loading
 */

using UnityEngine;
using System.Runtime.InteropServices;
using System;
using System.Collections.Generic;

public class BaseSDKWrapper : MonoBehaviour
{
    [Header("SDK Configuration")]
    [SerializeField] private string appName = "My Unity Game"; // Application name for Base Account SDK
    [SerializeField] private string network = "basesepolia"; // Network to connect to: "base" or "basesepolia"
    [SerializeField] private string customRpcUrl = ""; // Optional custom RPC URL override
    [SerializeField] private string paymasterUrl = "https://paymaster.base.org"; // Paymaster service URL for gasless transactions
    [SerializeField] private string paymasterPolicy = "VERIFYING_PAYMASTER"; // Paymaster policy type

    [Header("Transaction Settings")]
    [SerializeField]
    private List<TransactionCall> defaultCalls = new List<TransactionCall>
    {
        new TransactionCall { To = "0xYourContractAddress", Data = "0xYourFunctionData" }
    };

    /// <summary>
    /// Represents a single transaction call with target contract and encoded function data
    /// </summary>
    [System.Serializable]
    public class TransactionCall
    {
        public string To; // Target contract address (0x format)
        public string Data; // Encoded function call data (0x format)
    }

    // Runtime state variables
    private bool isInitialized = false; // SDK initialization status
    private string[] connectedAddresses = new string[0]; // Array of connected wallet addresses
    private string subAccountAddress = null; // Sub-account address for transactions
    private string currentNetworkJson = null; // Current network configuration as JSON
    private bool isWebGL = Application.platform == RuntimePlatform.WebGLPlayer; // Platform check

#if UNITY_WEBGL
    /// <summary>
    /// JavaScript interop declarations for WebGL builds
    /// These functions are implemented in the BaseSDKWrapper.jslib file
    /// </summary>
    [DllImport("__Internal")]
    private static extern bool initSDK(string configJson, string network, string customRpcUrl);

    [DllImport("__Internal")]
    private static extern string[] connectWallet();

    [DllImport("__Internal")]
    private static extern string getSubAccount();

    [DllImport("__Internal")]
    private static extern string sendTransaction(string callsJson, string chainIdOverride);

    [DllImport("__Internal")]
    private static extern string getCurrentNetwork();
#endif

    /// <summary>
    /// Event triggered when a transaction is successfully sent
    /// Subscribe to this event to handle transaction completion
    /// </summary>
    public event Action<string> OnTransactionSent; // Triggered with txHash on success

    /// <summary>
    /// Unity lifecycle method - automatically initializes SDK on WebGL platforms
    /// </summary>
    void Start()
    {
        DontDestroyOnLoad(gameObject);
        if (!isWebGL)
        {
            Debug.LogWarning("BaseSDKWrapper is only supported on WebGL. Current platform: " + Application.platform);
            enabled = false; // Disable the script on non-WebGL platforms
            return;
        }
        InitializeSDK();
    }

    /// <summary>
    /// Initializes the Base Account SDK with the configured settings
    /// Creates the SDK instance, sets up network configuration, and prepares for wallet connection
    /// </summary>
    public void InitializeSDK()
    {
        if (!isWebGL)
        {
            Debug.LogError("Cannot initialize SDK on non-WebGL platform.");
            return;
        }
        if (isInitialized) return;

#if UNITY_WEBGL
        string configJson = JsonUtility.ToJson(new
        {
            appName = appName,
            subAccounts = new { creation = "on-connect", defaultAccount = "sub" },
            paymaster = new { url = paymasterUrl, policy = paymasterPolicy }
        });

        bool success = initSDK(configJson, network.ToLower(), string.IsNullOrEmpty(customRpcUrl) ? null : customRpcUrl);
        if (success)
        {
            isInitialized = true;
            Debug.Log($"SDK initialized for {network} with RPC: {customRpcUrl ?? "Default"}");
            ConnectWallet();
        }
        else
        {
            Debug.LogError("Failed to initialize SDK. Check network and configuration.");
        }
#else
        Debug.LogWarning("SDK initialization is not supported on this platform.");
#endif
    }

    /// <summary>
    /// Connects to the user's wallet and retrieves account addresses
    /// This method triggers the wallet connection UI and retrieves both universal and sub-account addresses
    /// </summary>
    public void ConnectWallet()
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

#if UNITY_WEBGL
        connectedAddresses = connectWallet();
        if (connectedAddresses.Length > 0)
        {
            Debug.Log($"Connected addresses: {string.Join(", ", connectedAddresses)}");
            subAccountAddress = getSubAccount();
            if (!string.IsNullOrEmpty(subAccountAddress))
            {
                Debug.Log($"SubAccount: {subAccountAddress}");
            }
            else
            {
                Debug.LogWarning("SubAccount not found or created.");
            }
            currentNetworkJson = getCurrentNetwork();
            Debug.Log($"Current Network: {currentNetworkJson}");
        }
        else
        {
            Debug.LogError("Failed to connect wallet.");
        }
#else
        Debug.LogWarning("Wallet connection is not supported on this platform.");
#endif
    }

    /// <summary>
    /// Sends a transaction with the specified contract calls
    /// Uses the connected sub-account to send transactions with gasless functionality via paymaster
    /// </summary>
    /// <param name="calls">List of transaction calls to execute. If null, uses defaultCalls</param>
    /// <param name="chainIdOverride">Optional chain ID override for cross-chain transactions</param>
    /// <returns>Transaction hash if successful, null if failed</returns>
    public string SendTransaction(List<TransactionCall> calls = null, string chainIdOverride = null)
    {
        if (!isWebGL)
        {
            Debug.LogError("Cannot send transaction on non-WebGL platform.");
            return null;
        }
        if (!isInitialized || string.IsNullOrEmpty(subAccountAddress))
        {
            Debug.LogError("SDK not initialized or SubAccount not available. Call InitializeSDK and ConnectWallet first.");
            return null;
        }

        calls = calls ?? defaultCalls;
        if (calls == null || calls.Count == 0)
        {
            Debug.LogError("No transaction calls provided.");
            return null;
        }

#if UNITY_WEBGL
        string callsJson = JsonUtility.ToJson(calls);
        string txHash = sendTransaction(callsJson, chainIdOverride);
        if (!string.IsNullOrEmpty(txHash))
        {
            Debug.Log($"Transaction sent. Hash: {txHash}");
            OnTransactionSent?.Invoke(txHash); // Trigger event for subscribers
        }
        else
        {
            Debug.LogError("Transaction failed.");
        }
        return txHash;
#else
        Debug.LogWarning("Transaction sending is not supported on this platform.");
        return null;
#endif
    }

    /// <summary>
    /// Retrieves the current network configuration as a JSON string
    /// Returns network details including chain ID, name, and RPC URL
    /// </summary>
    /// <returns>JSON string containing network configuration, or null if not initialized</returns>
    public string GetCurrentNetwork()
    {
        if (!isWebGL)
        {
            Debug.LogError("Cannot get network on non-WebGL platform.");
            return null;
        }
        if (!isInitialized)
        {
            Debug.LogError("SDK not initialized. Call InitializeSDK first.");
            return null;
        }

#if UNITY_WEBGL
        return currentNetworkJson ?? getCurrentNetwork();
#else
        Debug.LogWarning("Network retrieval is not supported on this platform.");
        return null;
#endif
    }

    /// <summary>
    /// Retrieves the currently connected wallet addresses
    /// Returns an array of connected addresses (universal address at index 0)
    /// </summary>
    /// <returns>Array of connected addresses, or empty array if not connected</returns>
    public string[] GetConnectedAddresses()
    {
        if (!isWebGL)
        {
            Debug.LogError("Cannot get addresses on non-WebGL platform.");
            return new string[0];
        }
        return connectedAddresses;
    }

    /// <summary>
    /// Retrieves the current sub-account address used for transactions
    /// Sub-accounts enable gasless transactions and enhanced security features
    /// </summary>
    /// <returns>Sub-account address if available, null otherwise</returns>
    public string GetSubAccountAddress()
    {
        if (!isWebGL)
        {
            Debug.LogError("Cannot get sub-account on non-WebGL platform.");
            return null;
        }
        return subAccountAddress;
    }


    /// <summary>
    /// Unity lifecycle method - subscribes to transaction events when the component is enabled
    /// </summary>
    void OnEnable()
    {
        if (!isWebGL) return;
        OnTransactionSent += HandleTransactionSent;
    }

    /// <summary>
    /// Unity lifecycle method - unsubscribes from transaction events when the component is disabled
    /// </summary>
    void OnDisable()
    {
        if (!isWebGL) return;
        OnTransactionSent -= HandleTransactionSent;
    }

    /// <summary>
    /// Event handler for successful transaction completion
    /// Override this method or subscribe to OnTransactionSent event for custom handling
    /// </summary>
    /// <param name="txHash">The transaction hash of the completed transaction</param>
    private void HandleTransactionSent(string txHash)
    {
        if (!isWebGL) return;
        Debug.Log($"Transaction completed with hash: {txHash}");
        // Add custom transaction completion logic here
        // Examples: Update UI, trigger game events, save transaction history, etc.
        // TODO: Remove this in future updates (This is for testing purposes)
    }
}