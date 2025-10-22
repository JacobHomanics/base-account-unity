# Base Account Unity SDK

A Unity WebGL wrapper around the official [Base Account SDK](https://github.com/base-org/account), providing seamless integration of Base Account functionality into Unity games and applications.

## Overview

This package wraps the official Base Account SDK (`@base-org/account`) to provide Unity developers with easy access to Base Account features including:

- **Wallet Connection**: Connect to Base-compatible wallets
- **Sub-Account Management**: Create and manage sub-accounts for enhanced security
- **Gasless Transactions**: Send transactions without gas fees using paymaster integration
- **Multi-Call Transactions**: Execute multiple contract calls in a single transaction
- **Network Support**: Built-in support for Base Mainnet and Base Sepolia

## Architecture

The wrapper consists of two main components:

### 1. `BaseSDKWrapper.cs` (Unity C# Script)

- Unity MonoBehaviour component for easy integration
- Handles SDK initialization and configuration
- Provides Unity-friendly API for wallet operations
- Manages transaction sending and event handling
- WebGL-only implementation with automatic platform detection

### 2. `BaseSDKWrapper.jslib` (JavaScript Library)

- JavaScript bridge between Unity and the Base Account SDK
- Dynamically loads the official Base Account SDK from CDN
- Implements all SDK operations using the official API
- Handles network configuration and paymaster integration

## Key Features

- ✅ **Official SDK Integration**: Uses the official `@base-org/account` package
- ✅ **WebGL Support**: Optimized for Unity WebGL builds
- ✅ **Gasless Transactions**: Built-in paymaster support for zero-gas transactions
- ✅ **Sub-Account Creation**: Automatic sub-account creation on wallet connection
- ✅ **Multi-Network Support**: Base Mainnet and Base Sepolia support
- ✅ **Event-Driven**: Transaction completion events for game integration
- ✅ **Easy Configuration**: Inspector-based configuration in Unity

## Installation

1. Copy the `src/` folder contents to your Unity project
2. Place `BaseSDKWrapper.cs` in your Assets folder
3. Place `BaseSDKWrapper.jslib` in your `Assets/Plugins/WebGL/` folder
4. Attach the `BaseSDKWrapper` component to a GameObject in your scene

## Usage

```csharp
// Get the SDK wrapper component
BaseSDKWrapper sdk = GetComponent<BaseSDKWrapper>();

// Subscribe to transaction events
sdk.OnTransactionSent += (txHash) => {
    Debug.Log($"Transaction completed: {txHash}");
};

// Send a transaction
var calls = new List<BaseSDKWrapper.TransactionCall> {
    new BaseSDKWrapper.TransactionCall {
        To = "0xYourContractAddress",
        Data = "0xYourFunctionData"
    }
};
string txHash = sdk.SendTransaction(calls);
```

## Configuration

Configure the SDK through the Unity Inspector:

- **App Name**: Your application name for Base Account SDK
- **Network**: Choose between "base" (mainnet) or "basesepolia" (testnet)
- **Custom RPC URL**: Optional custom RPC endpoint
- **Paymaster URL**: Paymaster service for gasless transactions
- **Default Calls**: Pre-configured transaction calls

## Requirements

- Unity 2021.3 or later
- WebGL build target
- Internet connection for SDK loading
- Base-compatible wallet (Coinbase Wallet, etc.)

## Official Base Account SDK

This wrapper is built on top of the official Base Account SDK. For more information about Base Account features and capabilities, visit:

- [Base Account Documentation](https://docs.base.org/account-abstraction/)
- [Official Base Account SDK](https://github.com/base-org/account)
- [Base Account Examples](https://github.com/base-org/account/tree/main/examples)

## License

This wrapper follows the same license as the official Base Account SDK.

## Note

This package is still in beta phase.
