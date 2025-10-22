mergeInto(LibraryManager.library, {
  // Type definitions (inlined as comments for reference)
  // interface BaseAccountConfig { appName: string; subAccounts?: { creation: string; defaultAccount?: string }; paymaster?: { url: string; policy: string }; }
  // interface NetworkConfig { chainId: string; name: string; rpcUrl: string; }
  // interface BaseAccountSDK { getProvider(): BaseAccountProvider; }
  // interface BaseAccountProvider { request(args: { method: string; params?: any[] } | string, params?: any[]): Promise<any>; on(event: string, callback: (...args: any[]) => void): void; }
  // interface TransactionCall { to: string; data: string; }
  // interface SendTransactionParams { from: string; calls: TransactionCall[]; chainId?: string; version?: string; }

  // Supported networks
  NETWORKS: {
    base: {
      chainId: "0x" + (8453).toString(16),
      name: "Base",
      rpcUrl: "https://mainnet.base.org",
    },
    basesepolia: {
      chainId: "0x" + (84532).toString(16),
      name: "Base Sepolia",
      rpcUrl: "https://sepolia.base.org",
    },
  },

  // Global state
  sdk: null,
  provider: null,
  universalAddress: null,
  subAccountAddress: null,
  currentNetwork: null,

  // Logging function (compatible with Unity)
  log: function (message, type) {
    if (type === undefined) type = "info";
    console.log(`[${type.toUpperCase()}] ${Pointer_stringify(message)}`);
  },

  // Load the SDK dynamically
  loadBaseAccountSDK: function () {
    return new Promise((resolve, reject) => {
      if (typeof window.createBaseAccountSDK !== "undefined") {
        resolve(window.createBaseAccountSDK);
      } else {
        const script = document.createElement("script");
        script.src =
          "https://unpkg.com/@base-org/account/dist/base-account.min.js";
        script.onload = () => resolve(window.createBaseAccountSDK);
        script.onerror = (e) => reject(new Error(`Failed to load SDK: ${e}`));
        document.head.appendChild(script);
      }
    });
  },

  // Initialize SDK with network and configuration
  initSDK: function (configJson, network, customRpcUrl) {
    return new Promise(async (resolve) => {
      try {
        const config = JSON.parse(Pointer_stringify(configJson));
        log(`Initializing SDK for network: ${Pointer_stringify(network)}...`);
        if (!NETWORKS[network.toLowerCase()]) {
          log(
            `Unsupported network: ${Pointer_stringify(
              network
            )}. Use 'base' or 'basesepolia'.`,
            "error"
          );
          resolve(false);
          return;
        }
        currentNetwork = Object.assign({}, NETWORKS[network.toLowerCase()]);
        if (customRpcUrl) {
          log(`Overriding RPC URL with: ${Pointer_stringify(customRpcUrl)}`);
          currentNetwork.rpcUrl = Pointer_stringify(customRpcUrl);
        }
        log("Loading Base Account SDK...");
        const createSDK = await loadBaseAccountSDK();
        sdk = createSDK(
          Object.assign(Object.assign({}, config), {
            subAccounts: config.subAccounts || {
              creation: "on-connect",
              defaultAccount: "sub",
            },
            paymaster: config.paymaster || {
              url: "https://paymaster.base.org",
              policy: "VERIFYING_PAYMASTER",
            },
          })
        );
        provider = sdk.getProvider();
        log(`SDK initialized successfully for ${currentNetwork.name}!`);
        resolve(true);
      } catch (error) {
        log(
          `Error initializing SDK: ${
            error instanceof Error ? error.message : String(error)
          }`,
          "error"
        );
        resolve(false);
      }
    });
  },

  // Connect wallet and retrieve accounts
  connectWallet: function () {
    return new Promise(async (resolve) => {
      if (!provider) {
        log("SDK not initialized. Call initSDK first.", "error");
        resolve([]);
        return;
      }
      try {
        log("Requesting accounts...");
        const addresses = await provider.request({
          method: "eth_requestAccounts",
        });
        universalAddress = addresses[0];
        subAccountAddress = addresses[1];
        log(`✅ Connected!`);
        log(`Universal Address: ${universalAddress}`);
        if (subAccountAddress) {
          log(`SubAccount Address: ${subAccountAddress}`);
        }
        provider.on("accountsChanged", (accounts) => {
          log(`Accounts changed: ${accounts.join(", ")}`);
          universalAddress = accounts[0];
          subAccountAddress = accounts[1] || null;
        });
        provider.on("chainChanged", (chainId) => {
          log(`Chain changed: ${chainId}`);
          if (currentNetwork && chainId !== currentNetwork.chainId) {
            log(
              `Warning: Chain ID mismatch. Expected ${currentNetwork.chainId}`,
              "warning"
            );
          }
        });
        resolve(addresses);
      } catch (error) {
        log(
          `Error connecting wallet: ${
            error instanceof Error ? error.message : String(error)
          }`,
          "error"
        );
        resolve([]);
      }
    });
  },

  // Get sub-account (manual step, as auto-creation is in config)
  getSubAccount: function () {
    return new Promise(async (resolve) => {
      if (!provider || !universalAddress) {
        log("Wallet not connected or SDK not initialized.", "error");
        resolve(null);
        return;
      }
      try {
        log("Fetching SubAccount...");
        const result = await provider.request({
          method: "wallet_getSubAccounts",
          params: [
            { account: universalAddress, domain: window.location.origin },
          ],
        });
        if (result.subAccounts && result.subAccounts.length > 0) {
          subAccountAddress = result.subAccounts[0].address;
          log(`✅ SubAccount found: ${subAccountAddress}`);
        } else {
          log("No SubAccount exists. Creating one...");
          await provider.request({
            method: "wallet_addSubAccount",
            params: [{ version: "1" }],
          });
          const newResult = await provider.request({
            method: "wallet_getSubAccounts",
            params: [
              { account: universalAddress, domain: window.location.origin },
            ],
          });
          if (newResult.subAccounts && newResult.subAccounts.length > 0) {
            subAccountAddress = newResult.subAccounts[0].address;
            log(`✅ SubAccount created: ${subAccountAddress}`);
          }
        }
        resolve(subAccountAddress);
      } catch (error) {
        log(
          `Error getting SubAccount: ${
            error instanceof Error ? error.message : String(error)
          }`,
          "error"
        );
        resolve(null);
      }
    });
  },

  // Send a generic transaction with multiple calls
  sendTransaction: function (callsJson, chainIdOverride) {
    return new Promise(async (resolve) => {
      if (!provider || !subAccountAddress) {
        log(
          "SubAccount not available. Call connectWallet and getSubAccount first.",
          "error"
        );
        resolve(null);
        return;
      }
      const calls = JSON.parse(Pointer_stringify(callsJson));
      if (!calls || calls.length === 0) {
        log("No calls provided!", "error");
        resolve(null);
        return;
      }
      try {
        log(
          `Sending transaction with ${calls.length} calls from SubAccount...`
        );
        for (const call of calls) {
          if (!call.to.match(/^0x[a-fA-F0-9]{40}$/)) {
            log(`Invalid 'to' address in call: ${call.to}`, "error");
            resolve(null);
            return;
          }
          if (!/^(0x)?[a-fA-F0-9]+$/.test(call.data)) {
            log(`Invalid 'data' in call: ${call.data}`, "error");
            resolve(null);
            return;
          }
        }
        const chainId =
          chainIdOverride ||
          (await provider.request({ method: "eth_chainId" }));
        if (
          !chainId ||
          !Object.values(NETWORKS).some((net) => net.chainId === chainId)
        ) {
          log(
            `Unsupported chainId: ${chainId}. Use Base or Base Sepolia.`,
            "error"
          );
          resolve(null);
          return;
        }
        const txParams = {
          from: subAccountAddress,
          chainId: chainId,
          version: "1",
          calls: calls,
        };
        log(`Transaction params: ${JSON.stringify(txParams)}`);
        const txHash = await provider.request({
          method: "wallet_sendCalls",
          params: [txParams],
        });
        log(`✅ Transaction sent!`);
        log(`Transaction Hash: ${txHash}`);
        log(`View on BaseScan: https://sepolia.basescan.org/tx/${txHash}`);
        resolve(txHash);
      } catch (error) {
        log(
          `Error sending transaction: ${
            error instanceof Error ? error.message : String(error)
          }`,
          "error"
        );
        console.error(error);
        resolve(null);
      }
    });
  },

  // Get current network information
  getCurrentNetwork: function () {
    return currentNetwork ? JSON.stringify(currentNetwork) : null;
  },
});
