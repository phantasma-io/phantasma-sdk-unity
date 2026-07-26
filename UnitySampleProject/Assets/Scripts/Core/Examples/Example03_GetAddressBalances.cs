using Newtonsoft.Json;
using UnityEngine;

// Unity MonoBehaviour used to demonstrate how to fetch an account overview and its token balances
// without pulling an unbounded response: getAccountInfo answers name/staking at a cost independent
// of account size, and balances are walked one bounded page at a time.
public class Example03_GetAddressBalances : MonoBehaviour
{
	// The node rejects anything outside 1..100, so pages are requested at the documented maximum.
	private const uint PageSize = 100;

	// Entry point of example
	public void Run()
	{
		// Get reference to the scene-wide manager that stores API config and global variables
		var manager = FindObjectOfType<CoreExampleManager>();

		// Access the initialized Phantasma API instance
		var api = manager.phantasmaAPI;

		// Address to query - configured in the Unity inspector
		var address = manager.TestAddress;

		// Lightweight overview: registered name and staking only, no balance or NFT id lists
		StartCoroutine(api.GetAccountInfo(address, (accountInfo) =>
			{
				var json = JsonConvert.SerializeObject(accountInfo, Formatting.Indented);
				Debug.Log($"[Account] overview for {address}: {json}");

				// Balances arrive through cursor pagination; an empty cursor marks the last page
				StartCoroutine(LogBalancePage(api, address, ""));
			},
			(errorCode, errorMessage) =>
			{
				Debug.LogError($"[Error][{errorCode}] {errorMessage}");
			}
		));
	}

	// Requests one page and chains into the next while the node keeps handing back a cursor.
	private System.Collections.IEnumerator LogBalancePage(PhantasmaPhoenix.Unity.Core.PhantasmaAPI api, string address, string cursor)
	{
		yield return api.GetAccountFungibleTokens(address, "", 0, PageSize, cursor, true, (page) =>
			{
				var json = JsonConvert.SerializeObject(page.Result, Formatting.Indented);
				Debug.Log($"[Balance] fungible balances for {address}: {json}");

				if (!string.IsNullOrEmpty(page.Cursor))
				{
					StartCoroutine(LogBalancePage(api, address, page.Cursor));
				}
			},
			(errorCode, errorMessage) =>
			{
				Debug.LogError($"[Error][{errorCode}] {errorMessage}");
			}
		);
	}
}
