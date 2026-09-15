using UnityEngine;
using System;
using System.Globalization;
using PhantasmaPhoenix.Core;
using PhantasmaPhoenix.Cryptography;
using PhantasmaPhoenix.Protocol.Carbon;
using PhantasmaPhoenix.Protocol.Carbon.Blockchain;
using PhantasmaPhoenix.Protocol.Carbon.Blockchain.TxHelpers;

// Unity MonoBehaviour used to demonstrate the carbon transaction path. Example05 builds a VM script
// and sets the fee by hand. Here the message is a native transfer, and one call plans the fee, signs
// and broadcasts it.
public class Example11_SendCarbonToken : MonoBehaviour
{
	// Entry point of example
	public void Run()
	{
		// Get reference to the scene-wide manager that stores API config and global variables
		var manager = FindFirstObjectByType<CoreExampleManager>();

		var keys = manager.keys;

		// Abort if keys are not initialized - transaction cannot be signed
		if (keys == null)
		{
			throw new Exception("Private key is not set");
		}

		// Access the initialized Phantasma API instance
		var api = manager.phantasmaAPI;

		// Token symbol to transfer (e.g. SOUL, KCAL)
		var symbol = manager.TokenSymbol;

		// A carbon message names the token by its id, so the symbol is resolved first. The same call
		// also gives the decimals the amount is converted with.
		StartCoroutine(api.GetToken(symbol, (tokenResult) =>
			{
				TxMsg message;
				try
				{
					message = NativeTxHelper.TransferFungible(new TransferFungibleParams
					{
						From = new Bytes32(keys.PublicKey),
						// Recipient address for the token transfer - configured in the Unity inspector
						To = new Bytes32(Address.Parse(manager.TestAddress).GetPublicKey()),
						TokenId = ulong.Parse(tokenResult.CarbonId, CultureInfo.InvariantCulture),
						Amount = (ulong)UnitConversion.ToBigInteger((decimal)manager.TokenAmount, tokenResult.Decimals)
					});
				}
				catch (Exception e)
				{
					Debug.LogError($"[Error] Could not build the transaction message {e}");
					return;
				}

				// No gas offer is set on the message, so the SDK reads the chain's gas configuration and
				// prices the transfer before signing it. A message whose MaxGas you set yourself is signed
				// as it is.
				StartCoroutine(api.SignAndSendCarbonTransaction(keys, message,
					// Callback on success
					(txHash, encodedTx) =>
					{
						if (!string.IsNullOrEmpty(txHash))
						{
							Debug.Log($"Transaction was sent, hash: {txHash}. Check transaction status using GetTransaction() call");

							// Start polling to track transaction execution status on-chain
							StartCoroutine(Example06_CheckTransactionState.CheckTxStateLoop(txHash, null));

							return;
						}
						else
						{
							Debug.LogError("[Error] Failed to send transaction");
						}
					},
					// Callback for a refused pre-flight, a fee that cannot be planned, and RPC errors
					(errorCode, errorMessage) =>
					{
						Debug.LogError($"[Error][{errorCode}] Failed to send transaction: {errorMessage}");
					}));
			},
			(errorCode, errorMessage) =>
			{
				Debug.LogError($"[Error][{errorCode}] {errorMessage}");
			}
		));
	}
}
