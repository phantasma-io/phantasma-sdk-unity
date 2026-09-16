using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using PhantasmaPhoenix.Cryptography;
using PhantasmaPhoenix.Protocol.Carbon;
using PhantasmaPhoenix.Protocol.Carbon.Blockchain;
using PhantasmaPhoenix.Protocol.Carbon.Blockchain.Modules;
using PhantasmaPhoenix.Protocol.Carbon.Blockchain.TxHelpers;
using PhantasmaPhoenix.RPC.Models;
using PhantasmaPhoenix.RPC.Types;
using PhantasmaPhoenix.Unity.Core.Logging;
using UnityEngine;

namespace PhantasmaPhoenix.Unity.Core
{
	/// <summary>
	/// Unity coroutine wrapper around the Phantasma JSON-RPC API.
	/// </summary>
	public class PhantasmaAPI
	{
		/// <summary>
		/// JSON-RPC endpoint URL, for example http://localhost:5172/rpc.
		/// </summary>
		public readonly string Host;

		/// <summary>
		/// Optional API key sent in the X-Api-Key header on every RPC request.
		/// </summary>
		public readonly string ApiKey;

		/// <summary>
		/// Creates a Unity RPC API wrapper for a JSON-RPC endpoint.
		/// </summary>
		/// <param name="host">JSON-RPC endpoint URL, for example http://localhost:5172/rpc.</param>
		/// <param name="apiKey">Optional API key sent in the X-Api-Key header on every RPC request.</param>
		public PhantasmaAPI(string host, string apiKey = null)
		{
			this.Host = host;
			this.ApiKey = apiKey;
		}

		/// <summary>
		/// Sends a JSON-RPC request for wrapper methods.
		/// </summary>
		/// <param name="method">JSON-RPC method name.</param>
		/// <param name="callback">Callback invoked with the decoded RPC result.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <param name="parameters">JSON-RPC positional parameters.</param>
		/// <typeparam name="T">Decoded RPC result type.</typeparam>
		/// <returns>Coroutine that sends the JSON-RPC request.</returns>
		protected virtual IEnumerator RpcRequest<T>(string method, Action<T> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries, params object[] parameters)
		{
			yield return WebClient.RPCRequest<T>(Host, ApiKey, method, timeout, retries, errorHandlingCallback, callback, parameters);
		}

		#region Account
		/// <summary>
		/// Gets a lightweight account overview (name and staking) for the specified address
		/// </summary>
		/// <remarks>
		/// Cost is independent of how much the address holds, which makes this the call to use in
		/// wallet refresh loops; balances and NFTs are fetched separately through the cursor-paginated
		/// account endpoints.
		/// </remarks>
		/// <param name="addressText">Account address text.</param>
		/// <param name="callback">Callback invoked with the account overview.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests the account overview.</returns>
		public IEnumerator GetAccountInfo(string addressText, Action<AccountInfoResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getAccountInfo", callback, errorHandlingCallback, timeout, retries, addressText);
		}

		/// <summary>
		/// Gets a lightweight account overview (name and staking) for the specified address and address type
		/// </summary>
		/// <param name="addressText">Account address text.</param>
		/// <param name="checkAddressReservedByte">True to enforce reserved-byte validation.</param>
		/// <param name="addressType">Account address type.</param>
		/// <param name="callback">Callback invoked with the account overview.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests the account overview.</returns>
		public IEnumerator GetAccountInfo(string addressText, bool checkAddressReservedByte, RpcAddressType addressType, Action<AccountInfoResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getAccountInfo", callback, errorHandlingCallback, timeout, retries, addressText, checkAddressReservedByte, addressType);
		}

		/// <summary>
		/// Gets lightweight account overviews (name and staking) for a batch of addresses in one call
		/// </summary>
		/// <remarks>
		/// Batch counterpart of GetAccountInfo with the same per-account record: the node answers
		/// every address from a single state snapshot and returns results in request order, at most
		/// 100 addresses per request. The addresses travel as a native JSON array parameter; a
		/// malformed address rejects the whole batch.
		/// </remarks>
		/// <param name="addresses">Account addresses, at most 100 per request.</param>
		/// <param name="callback">Callback invoked with the account overviews in request order.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests the account overviews, or completes immediately with an empty array when no addresses are provided.</returns>
		public IEnumerator GetAccountInfos(string[] addresses, Action<AccountInfoResult[]> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			if (addresses == null || addresses.Length == 0)
			{
				callback(new AccountInfoResult[0]);
				yield break;
			}

			yield return RpcRequest("getAccountInfos", callback, errorHandlingCallback, timeout, retries, new object[] { addresses });
		}

		/// <summary>
		/// Gets lightweight account overviews (name and staking) for a batch of addresses of the same address type
		/// </summary>
		/// <param name="addresses">Account addresses of the same address type, at most 100 per request.</param>
		/// <param name="checkAddressReservedByte">True to enforce reserved-byte validation.</param>
		/// <param name="addressType">Account address type.</param>
		/// <param name="callback">Callback invoked with the account overviews in request order.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests the account overviews, or completes immediately with an empty array when no addresses are provided.</returns>
		public IEnumerator GetAccountInfos(string[] addresses, bool checkAddressReservedByte, RpcAddressType addressType, Action<AccountInfoResult[]> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			if (addresses == null || addresses.Length == 0)
			{
				callback(new AccountInfoResult[0]);
				yield break;
			}

			yield return RpcRequest("getAccountInfos", callback, errorHandlingCallback, timeout, retries, addresses, checkAddressReservedByte, addressType);
		}

		/// <summary>
		/// Gets account information, including balances, for the specified address
		/// </summary>
		/// <remarks>
		/// Deprecated. The response embeds every NFT id the address owns, so it grows without bound
		/// with the size of the account; the node caps each Balances[].Ids list at 10000 entries while
		/// Balances[].Amount keeps the true count. Use GetAccountInfo together with
		/// GetAccountFungibleTokens and GetAccountNFTs.
		/// </remarks>
		/// <param name="addressText">Account address text.</param>
		/// <param name="callback">Callback invoked with account data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests account data.</returns>
		[Obsolete("Use GetAccountInfo with GetAccountFungibleTokens/GetAccountNFTs; getAccount caps Balances[].Ids at 10000.")]
		public IEnumerator GetAccount(string addressText, Action<AccountResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getAccount", callback, errorHandlingCallback, timeout, retries, addressText);
		}

		/// <summary>
		/// Gets account information, including balances, for the specified address and address type
		/// </summary>
		/// <param name="addressText">Account address text.</param>
		/// <param name="extended">Deprecated RPC flag kept for API parity.</param>
		/// <param name="checkAddressReservedByte">True to enforce reserved-byte validation.</param>
		/// <param name="addressType">Account address type.</param>
		/// <param name="callback">Callback invoked with account data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests account data.</returns>
		[Obsolete("Use GetAccountInfo with GetAccountFungibleTokens/GetAccountNFTs; getAccount caps Balances[].Ids at 10000.")]
		public IEnumerator GetAccount(string addressText, bool extended, bool checkAddressReservedByte, RpcAddressType addressType, Action<AccountResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getAccount", callback, errorHandlingCallback, timeout, retries, addressText, extended, checkAddressReservedByte, addressType);
		}

		/// <summary>
		/// Gets account information for multiple addresses
		/// </summary>
		/// <param name="addresses">Account addresses.</param>
		/// <param name="callback">Callback invoked with account data for the requested addresses.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests account data, or completes immediately with an empty array when no addresses are provided.</returns>
		[Obsolete("Use GetAccountInfo with GetAccountFungibleTokens/GetAccountNFTs; getAccounts caps Balances[].Ids at 10000.")]
		public IEnumerator GetAccounts(string[] addresses, Action<AccountResult[]> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			if (addresses == null || addresses.Length == 0)
			{
				callback(new AccountResult[0]);
				yield break;
			}

			yield return RpcRequest("getAccounts", callback, errorHandlingCallback, timeout, retries, String.Join(",", addresses));
		}

		/// <summary>
		/// Gets account information for multiple addresses of the same address type
		/// </summary>
		/// <param name="addresses">Account addresses of the same address type.</param>
		/// <param name="extended">Deprecated RPC flag kept for API parity.</param>
		/// <param name="checkAddressReservedByte">True to enforce reserved-byte validation.</param>
		/// <param name="addressType">Account address type.</param>
		/// <param name="callback">Callback invoked with account data for the requested addresses.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests account data, or completes immediately with an empty array when no addresses are provided.</returns>
		[Obsolete("Use GetAccountInfo with GetAccountFungibleTokens/GetAccountNFTs; getAccounts caps Balances[].Ids at 10000.")]
		public IEnumerator GetAccounts(string[] addresses, bool extended, bool checkAddressReservedByte, RpcAddressType addressType, Action<AccountResult[]> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			if (addresses == null || addresses.Length == 0)
			{
				callback(new AccountResult[0]);
				yield break;
			}

			yield return RpcRequest("getAccounts", callback, errorHandlingCallback, timeout, retries, String.Join(",", addresses), extended, checkAddressReservedByte, addressType);
		}

		/// <summary>
		/// Looks up an address by name
		/// </summary>
		/// <param name="name">Registered account name.</param>
		/// <param name="callback">Callback invoked with the resolved address text.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that resolves an account name.</returns>
		public IEnumerator LookUpName(string name, Action<string> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return WebClient.RPCRequest<string>(Host, ApiKey, "lookUpName", timeout, retries, errorHandlingCallback, (result) =>
			{
				callback(result);
			}, name);
		}

		/// <summary>
		/// Gets fungible token balances owned by an address (cursor pagination)
		/// </summary>
		/// <param name="account">Account address text.</param>
		/// <param name="callback">Callback invoked with cursor-paginated fungible balances.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests fungible token balances.</returns>
		public IEnumerator GetAccountFungibleTokens(string account, Action<CursorPaginatedResult<BalanceResult[]>> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return GetAccountFungibleTokens(account, "", 0, 10, "", true, callback, errorHandlingCallback, timeout, retries);
		}

		/// <summary>
		/// Gets fungible token balances owned by an address (cursor pagination)
		/// </summary>
		/// <param name="account">Account address text.</param>
		/// <param name="tokenSymbol">Optional token symbol filter.</param>
		/// <param name="carbonTokenId">Optional Carbon token ID filter.</param>
		/// <param name="pageSize">Maximum number of items to return.</param>
		/// <param name="cursor">Cursor returned by a previous page, or an empty string for the first page.</param>
		/// <param name="checkAddressReservedByte">True to enforce reserved-byte validation.</param>
		/// <param name="callback">Callback invoked with cursor-paginated fungible balances.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests fungible token balances.</returns>
		public IEnumerator GetAccountFungibleTokens(string account, string tokenSymbol, ulong carbonTokenId, uint pageSize, string cursor, bool checkAddressReservedByte, Action<CursorPaginatedResult<BalanceResult[]>> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getAccountFungibleTokens", callback, errorHandlingCallback, timeout, retries, account, tokenSymbol, carbonTokenId, pageSize, cursor, checkAddressReservedByte);
		}

		/// <summary>
		/// Gets fungible token balances owned by an address of the specified address type (cursor pagination)
		/// </summary>
		/// <param name="account">Account address text.</param>
		/// <param name="tokenSymbol">Optional token symbol filter.</param>
		/// <param name="carbonTokenId">Optional Carbon token ID filter.</param>
		/// <param name="pageSize">Maximum number of items to return.</param>
		/// <param name="cursor">Cursor returned by a previous page, or an empty string for the first page.</param>
		/// <param name="checkAddressReservedByte">True to enforce reserved-byte validation.</param>
		/// <param name="addressType">Account address type.</param>
		/// <param name="callback">Callback invoked with cursor-paginated fungible balances.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests fungible token balances.</returns>
		public IEnumerator GetAccountFungibleTokens(string account, string tokenSymbol, ulong carbonTokenId, uint pageSize, string cursor, bool checkAddressReservedByte, RpcAddressType addressType, Action<CursorPaginatedResult<BalanceResult[]>> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getAccountFungibleTokens", callback, errorHandlingCallback, timeout, retries, account, tokenSymbol, carbonTokenId, pageSize, cursor, checkAddressReservedByte, addressType);
		}

		/// <summary>
		/// Gets NFTs owned by an address (cursor pagination)
		/// </summary>
		/// <param name="account">Account address text.</param>
		/// <param name="callback">Callback invoked with cursor-paginated NFT data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests NFTs owned by the account.</returns>
		public IEnumerator GetAccountNFTs(string account, Action<CursorPaginatedResult<TokenDataResult[]>> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return GetAccountNFTs(account, "", 0, 0, 10, "", false, true, callback, errorHandlingCallback, timeout, retries);
		}

		/// <summary>
		/// Gets NFTs owned by an address (cursor pagination)
		/// </summary>
		/// <param name="account">Account address text.</param>
		/// <param name="tokenSymbol">Optional token symbol filter.</param>
		/// <param name="carbonTokenId">Optional Carbon token ID filter.</param>
		/// <param name="carbonSeriesId">Optional Carbon series ID filter.</param>
		/// <param name="pageSize">Maximum number of items to return.</param>
		/// <param name="cursor">Cursor returned by a previous page, or an empty string for the first page.</param>
		/// <param name="extended">True to include NFT properties.</param>
		/// <param name="checkAddressReservedByte">True to enforce reserved-byte validation.</param>
		/// <param name="callback">Callback invoked with cursor-paginated NFT data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests NFTs owned by the account.</returns>
		public IEnumerator GetAccountNFTs(string account, string tokenSymbol, ulong carbonTokenId, uint carbonSeriesId, uint pageSize, string cursor, bool extended, bool checkAddressReservedByte, Action<CursorPaginatedResult<TokenDataResult[]>> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getAccountNFTs", callback, errorHandlingCallback, timeout, retries, account, tokenSymbol, carbonTokenId, carbonSeriesId, pageSize, cursor, extended, checkAddressReservedByte);
		}

		/// <summary>
		/// Gets NFTs owned by an address of the specified address type (cursor pagination)
		/// </summary>
		/// <param name="account">Account address text.</param>
		/// <param name="tokenSymbol">Optional token symbol filter.</param>
		/// <param name="carbonTokenId">Optional Carbon token ID filter.</param>
		/// <param name="carbonSeriesId">Optional Carbon series ID filter.</param>
		/// <param name="pageSize">Maximum number of items to return.</param>
		/// <param name="cursor">Cursor returned by a previous page, or an empty string for the first page.</param>
		/// <param name="extended">True to include NFT properties.</param>
		/// <param name="checkAddressReservedByte">True to enforce reserved-byte validation.</param>
		/// <param name="addressType">Account address type.</param>
		/// <param name="callback">Callback invoked with cursor-paginated NFT data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests NFTs owned by the account.</returns>
		public IEnumerator GetAccountNFTs(string account, string tokenSymbol, ulong carbonTokenId, uint carbonSeriesId, uint pageSize, string cursor, bool extended, bool checkAddressReservedByte, RpcAddressType addressType, Action<CursorPaginatedResult<TokenDataResult[]>> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getAccountNFTs", callback, errorHandlingCallback, timeout, retries, account, tokenSymbol, carbonTokenId, carbonSeriesId, pageSize, cursor, extended, checkAddressReservedByte, addressType);
		}

		/// <summary>
		/// Gets NFT tokens for which the account owns at least one NFT instance (cursor pagination)
		/// </summary>
		/// <param name="account">Account address text.</param>
		/// <param name="callback">Callback invoked with cursor-paginated NFT token metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests NFT token types owned by the account.</returns>
		public IEnumerator GetAccountOwnedTokens(string account, Action<CursorPaginatedResult<TokenResult[]>> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return GetAccountOwnedTokens(account, "", 0, 10, "", true, callback, errorHandlingCallback, timeout, retries);
		}

		/// <summary>
		/// Gets NFT tokens for which the account owns at least one NFT instance (cursor pagination)
		/// </summary>
		/// <param name="account">Account address text.</param>
		/// <param name="tokenSymbol">Optional token symbol filter.</param>
		/// <param name="carbonTokenId">Optional Carbon token ID filter.</param>
		/// <param name="pageSize">Maximum number of items to return.</param>
		/// <param name="cursor">Cursor returned by a previous page, or an empty string for the first page.</param>
		/// <param name="checkAddressReservedByte">True to enforce reserved-byte validation.</param>
		/// <param name="callback">Callback invoked with cursor-paginated NFT token metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests NFT token types owned by the account.</returns>
		public IEnumerator GetAccountOwnedTokens(string account, string tokenSymbol, ulong carbonTokenId, uint pageSize, string cursor, bool checkAddressReservedByte, Action<CursorPaginatedResult<TokenResult[]>> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getAccountOwnedTokens", callback, errorHandlingCallback, timeout, retries, account, tokenSymbol, carbonTokenId, pageSize, cursor, checkAddressReservedByte);
		}

		/// <summary>
		/// Gets NFT tokens for which the account owns at least one NFT instance for the specified address type (cursor pagination)
		/// </summary>
		/// <param name="account">Account address text.</param>
		/// <param name="tokenSymbol">Optional token symbol filter.</param>
		/// <param name="carbonTokenId">Optional Carbon token ID filter.</param>
		/// <param name="pageSize">Maximum number of items to return.</param>
		/// <param name="cursor">Cursor returned by a previous page, or an empty string for the first page.</param>
		/// <param name="checkAddressReservedByte">True to enforce reserved-byte validation.</param>
		/// <param name="addressType">Account address type.</param>
		/// <param name="callback">Callback invoked with cursor-paginated NFT token metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests NFT token types owned by the account.</returns>
		public IEnumerator GetAccountOwnedTokens(string account, string tokenSymbol, ulong carbonTokenId, uint pageSize, string cursor, bool checkAddressReservedByte, RpcAddressType addressType, Action<CursorPaginatedResult<TokenResult[]>> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getAccountOwnedTokens", callback, errorHandlingCallback, timeout, retries, account, tokenSymbol, carbonTokenId, pageSize, cursor, checkAddressReservedByte, addressType);
		}

		/// <summary>
		/// Gets NFT series for which the account owns at least one NFT instance (cursor pagination)
		/// </summary>
		/// <param name="account">Account address text.</param>
		/// <param name="callback">Callback invoked with cursor-paginated NFT series metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests NFT series owned by the account.</returns>
		public IEnumerator GetAccountOwnedTokenSeries(string account, Action<CursorPaginatedResult<TokenSeriesResult[]>> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return GetAccountOwnedTokenSeries(account, "", 0, 10, "", true, callback, errorHandlingCallback, timeout, retries);
		}

		/// <summary>
		/// Gets NFT series for which the account owns at least one NFT instance (cursor pagination)
		/// </summary>
		/// <param name="account">Account address text.</param>
		/// <param name="tokenSymbol">Optional token symbol filter.</param>
		/// <param name="carbonTokenId">Optional Carbon token ID filter.</param>
		/// <param name="pageSize">Maximum number of items to return.</param>
		/// <param name="cursor">Cursor returned by a previous page, or an empty string for the first page.</param>
		/// <param name="checkAddressReservedByte">True to enforce reserved-byte validation.</param>
		/// <param name="callback">Callback invoked with cursor-paginated NFT series metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests NFT series owned by the account.</returns>
		public IEnumerator GetAccountOwnedTokenSeries(string account, string tokenSymbol, ulong carbonTokenId, uint pageSize, string cursor, bool checkAddressReservedByte, Action<CursorPaginatedResult<TokenSeriesResult[]>> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getAccountOwnedTokenSeries", callback, errorHandlingCallback, timeout, retries, account, tokenSymbol, carbonTokenId, pageSize, cursor, checkAddressReservedByte);
		}

		/// <summary>
		/// Gets NFT series for which the account owns at least one NFT instance for the specified address type (cursor pagination)
		/// </summary>
		/// <param name="account">Account address text.</param>
		/// <param name="tokenSymbol">Optional token symbol filter.</param>
		/// <param name="carbonTokenId">Optional Carbon token ID filter.</param>
		/// <param name="pageSize">Maximum number of items to return.</param>
		/// <param name="cursor">Cursor returned by a previous page, or an empty string for the first page.</param>
		/// <param name="checkAddressReservedByte">True to enforce reserved-byte validation.</param>
		/// <param name="addressType">Account address type.</param>
		/// <param name="callback">Callback invoked with cursor-paginated NFT series metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests NFT series owned by the account.</returns>
		public IEnumerator GetAccountOwnedTokenSeries(string account, string tokenSymbol, ulong carbonTokenId, uint pageSize, string cursor, bool checkAddressReservedByte, RpcAddressType addressType, Action<CursorPaginatedResult<TokenSeriesResult[]>> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getAccountOwnedTokenSeries", callback, errorHandlingCallback, timeout, retries, account, tokenSymbol, carbonTokenId, pageSize, cursor, checkAddressReservedByte, addressType);
		}
		#endregion

		#region Auction
		/// <summary>
		/// Gets the number of auctions currently available in the market contract for a given token
		/// <para><b>⚠️ Currently disabled - this functionality is not available and will be re-enabled according to the roadmap: https://phantasma.info/blockchain#roadmap</b></para>
		/// </summary>
		/// <param name="chainAddressOrName">Chain address or chain name where the market contract is located.</param>
		/// <param name="symbol">Token symbol used as an auction filter.</param>
		/// <param name="callback">Callback invoked with the auction count.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests the auction count.</returns>
		public IEnumerator GetAuctionsCount(string chainAddressOrName, string symbol, Action<int> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return WebClient.RPCRequest<string>(Host, ApiKey, "getAuctionsCount", timeout, retries, errorHandlingCallback, (result) =>
			{
				callback(int.Parse(result));
			}, chainAddressOrName, symbol);
		}

		/// <summary>
		/// Gets all auctions currently available in the market contract for a given token, with pagination
		/// <para><b>⚠️ Currently disabled - this functionality is not available and will be re-enabled according to the roadmap: https://phantasma.info/blockchain#roadmap</b></para>
		/// </summary>
		/// <param name="chainAddressOrName">Chain address or chain name where the market contract is located.</param>
		/// <param name="symbol">Token symbol used as an auction filter.</param>
		/// <param name="page">Page number to request.</param>
		/// <param name="pageSize">Maximum number of items to return.</param>
		/// <param name="callback">Callback invoked with auctions, current page, total item count, and total page count.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests paginated auctions.</returns>
		public IEnumerator GetAuctions(string chainAddressOrName, string symbol, uint page, uint pageSize, Action<AuctionResult[], uint, uint, uint> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return WebClient.RPCRequest<PaginatedResult<AuctionResult[]>>(Host, ApiKey, "getAuctions", timeout, retries, errorHandlingCallback, (result) =>
			{
				callback(result.Result, result.Page, result.Total, result.TotalPages);
			}, chainAddressOrName, symbol, page, pageSize);
		}


		/// <summary>
		/// Gets a single auction by symbol and auction id
		/// <para><b>⚠️ Currently disabled - this functionality is not available and will be re-enabled according to the roadmap: https://phantasma.info/blockchain#roadmap</b></para>
		/// </summary>
		/// <param name="chainAddressOrName">Chain address or chain name where the market contract is located.</param>
		/// <param name="symbol">Auction token symbol.</param>
		/// <param name="IDtext">Auction ID text.</param>
		/// <param name="callback">Callback invoked with auction data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests one auction.</returns>
		public IEnumerator GetAuction(string chainAddressOrName, string symbol, string IDtext, Action<AuctionResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return WebClient.RPCRequest<AuctionResult>(Host, ApiKey, "getAuction", timeout, retries, errorHandlingCallback, (result) =>
			{
				callback(result);
			}, chainAddressOrName, symbol, IDtext);
		}
		#endregion

		#region Block
		/// <summary>
		/// Gets the latest block height for a chain
		/// </summary>
		/// <param name="chainInput">Chain address or chain name.</param>
		/// <param name="callback">Callback invoked with the latest block height.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests the latest block height.</returns>
		public IEnumerator GetBlockHeight(string chainInput, Action<long> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return WebClient.RPCRequest<string>(Host, ApiKey, "getBlockHeight", timeout, retries, errorHandlingCallback, (result) =>
			{
				callback(long.Parse(result));
			}, chainInput);
		}


		/// <summary>
		/// Gets the number of transactions in a block by block hash
		/// </summary>
		/// <param name="blockHash">Block hash.</param>
		/// <param name="callback">Callback invoked with the transaction count.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests the root-chain transaction count for a block.</returns>
		public IEnumerator GetBlockTransactionCountByHash(string blockHash, Action<int> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return GetBlockTransactionCountByHash(PhantasmaPhoenix.Protocol.DomainSettings.RootChainName, blockHash, callback, errorHandlingCallback, timeout, retries);
		}

		/// <summary>
		/// Gets the number of transactions in a block by chain and block hash
		/// </summary>
		/// <param name="chainAddressOrName">Chain address or chain name where the block is located.</param>
		/// <param name="blockHash">Block hash.</param>
		/// <param name="callback">Callback invoked with the transaction count.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests the transaction count for a block.</returns>
		public IEnumerator GetBlockTransactionCountByHash(string chainAddressOrName, string blockHash, Action<int> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getBlockTransactionCountByHash", (string result) =>
			{
				callback(int.Parse(result, CultureInfo.InvariantCulture));
			}, errorHandlingCallback, timeout, retries, chainAddressOrName, blockHash);
		}

		/// <summary>
		/// Gets a block by its hash
		/// </summary>
		/// <param name="blockHash">Block hash.</param>
		/// <param name="callback">Callback invoked with block data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests a block by hash.</returns>
		public IEnumerator GetBlockByHash(string blockHash, Action<BlockResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return WebClient.RPCRequest<BlockResult>(Host, ApiKey, "getBlockByHash", timeout, retries, errorHandlingCallback, (result) =>
			{
				callback(result);
			}, blockHash);
		}

		/// <summary>
		/// Gets a block by chain and height
		/// </summary>
		/// <param name="chainInput">Chain address or chain name.</param>
		/// <param name="height">Block height.</param>
		/// <param name="callback">Callback invoked with block data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests a block by height.</returns>
		public IEnumerator GetBlockByHeight(string chainInput, long height, Action<BlockResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return WebClient.RPCRequest<BlockResult>(Host, ApiKey, "getBlockByHeight", timeout, retries, errorHandlingCallback, (result) =>
			{
				callback(result);
			}, chainInput, height.ToString());
		}

		/// <summary>
		/// Gets the latest block for a chain
		/// </summary>
		/// <param name="chainInput">Chain address or chain name.</param>
		/// <param name="callback">Callback invoked with the latest block data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests the latest block.</returns>
		public IEnumerator GetLatestBlock(string chainInput, Action<BlockResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return WebClient.RPCRequest<BlockResult>(Host, ApiKey, "getLatestBlock", timeout, retries, errorHandlingCallback, (result) =>
			{
				callback(result);
			}, chainInput);
		}

		/// <summary>
		/// Gets a root-chain transaction by block hash and transaction index
		/// </summary>
		/// <param name="blockHash">Block hash.</param>
		/// <param name="index">Transaction index within the block.</param>
		/// <param name="callback">Callback invoked with transaction data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests a root-chain transaction by block hash and index.</returns>
		public IEnumerator GetTransactionByBlockHashAndIndex(string blockHash, int index, Action<TransactionResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return GetTransactionByBlockHashAndIndex(PhantasmaPhoenix.Protocol.DomainSettings.RootChainName, blockHash, index, callback, errorHandlingCallback, timeout, retries);
		}

		/// <summary>
		/// Gets a transaction by chain, block hash and transaction index
		/// </summary>
		/// <param name="chainAddressOrName">Chain address or chain name where the block is located.</param>
		/// <param name="blockHash">Block hash.</param>
		/// <param name="index">Transaction index within the block.</param>
		/// <param name="callback">Callback invoked with transaction data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests a transaction by block hash and index.</returns>
		public IEnumerator GetTransactionByBlockHashAndIndex(string chainAddressOrName, string blockHash, int index, Action<TransactionResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getTransactionByBlockHashAndIndex", callback, errorHandlingCallback, timeout, retries, chainAddressOrName, blockHash, index);
		}
		#endregion

		#region Chain
		/// <summary>
		/// Gets chain information for the root chain.
		/// <para><b>⚠️ Currently disabled - this functionality is not available and will be re-enabled according to the roadmap: https://phantasma.info/blockchain#roadmap</b></para>
		/// </summary>
		/// <param name="callback">Callback invoked with chain metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests root chain metadata.</returns>
		public IEnumerator GetChain(Action<ChainResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return GetChain(PhantasmaPhoenix.Protocol.DomainSettings.RootChainName, true, callback, errorHandlingCallback, timeout, retries);
		}

		/// <summary>
		/// Gets chain information by chain name.
		/// <para><b>⚠️ Currently disabled - this functionality is not available and will be re-enabled according to the roadmap: https://phantasma.info/blockchain#roadmap</b></para>
		/// </summary>
		/// <param name="name">Chain name.</param>
		/// <param name="extended">True to request extended chain metadata.</param>
		/// <param name="callback">Callback invoked with chain metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests chain metadata.</returns>
		public IEnumerator GetChain(string name, bool extended, Action<ChainResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getChain", callback, errorHandlingCallback, timeout, retries, name, extended);
		}

		/// <summary>
		/// Gets an array of all chains deployed on Phantasma
		/// <para><b>⚠️ Currently disabled - this functionality is not available and will be re-enabled according to the roadmap: https://phantasma.info/blockchain#roadmap</b></para>
		/// </summary>
		/// <param name="callback">Callback invoked with chain metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests all chains.</returns>
		public IEnumerator GetChains(Action<ChainResult[]> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return WebClient.RPCRequest<ChainResult[]>(Host, ApiKey, "getChains", timeout, retries, errorHandlingCallback, (result) =>
			{
				callback(result);
			});
		}

		/// <summary>
		/// Gets the current on-chain gas configuration plus the chain parameters fee estimation needs
		/// </summary>
		/// <remarks>
		/// Changes only via governance resolutions, so the result is safe to cache for the lifetime of
		/// an endpoint. Feed <see cref="GasConfigResult.ToGasConfig"/> into NativeFeeEstimator to obtain
		/// Tier-1 fee estimates without a round trip per transaction.
		/// </remarks>
		/// <param name="callback">Callback invoked with the gas configuration.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests the gas configuration.</returns>
		public IEnumerator GetGasConfig(Action<GasConfigResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getGasConfig", callback, errorHandlingCallback, timeout, retries);
		}
		#endregion

		#region Contract
		/// <summary>
		/// Gets contract metadata by name from the main chain
		/// <para><b>⚠️ Currently disabled - this functionality is not available and will be re-enabled according to the roadmap: https://phantasma.info/blockchain#roadmap</b></para>
		/// </summary>
		/// <param name="contractName">Contract name on the root chain.</param>
		/// <param name="callback">Callback invoked with contract metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests contract metadata by name.</returns>
		public IEnumerator GetContract(string contractName, Action<ContractResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return WebClient.RPCRequest<ContractResult>(Host, ApiKey, "getContract", timeout, retries, errorHandlingCallback, (result) =>
			{
				callback(result);
			}, PhantasmaPhoenix.Protocol.DomainSettings.RootChainName, contractName);
		}

		/// <summary>
		/// Gets contract metadata by address from the specified chain
		/// </summary>
		/// <param name="chainAddressOrName">Chain address or chain name where the contract is deployed.</param>
		/// <param name="contractAddress">Contract address.</param>
		/// <param name="callback">Callback invoked with contract metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests contract metadata by address.</returns>
		public IEnumerator GetContractByAddress(string chainAddressOrName, string contractAddress, Action<ContractResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getContractByAddress", callback, errorHandlingCallback, timeout, retries, chainAddressOrName, contractAddress);
		}

		/// <summary>
		/// Gets all contracts deployed on the main chain
		/// <para><b>⚠️ Currently disabled - this functionality is not available and will be re-enabled according to the roadmap: https://phantasma.info/blockchain#roadmap</b></para>
		/// </summary>
		/// <param name="callback">Callback invoked with root-chain contract metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests all root-chain contracts.</returns>
		public IEnumerator GetContracts(Action<ContractResult[]> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return WebClient.RPCRequest<ContractResult[]>(Host, ApiKey, "getContracts", timeout, retries, errorHandlingCallback, (result) =>
			{
				callback(result);
			}, PhantasmaPhoenix.Protocol.DomainSettings.RootChainName);
		}
		#endregion

		#region Leaderboard
		/// <summary>
		/// Gets a leaderboard by name
		/// <para><b>⚠️ Currently disabled - this functionality is not available and will be re-enabled according to the roadmap: https://phantasma.info/blockchain#roadmap</b></para>
		/// </summary>
		/// <param name="name">Leaderboard name.</param>
		/// <param name="callback">Callback invoked with leaderboard data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests leaderboard data.</returns>
		public IEnumerator GetLeaderboard(string name, Action<LeaderboardResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return WebClient.RPCRequest<LeaderboardResult>(Host, ApiKey, "getLeaderboard", timeout, retries, errorHandlingCallback, (result) =>
			{
				callback(result);
			}, name);
		}
		#endregion

		#region Nexus
		/// <summary>
		/// Gets nexus metadata including an array of all chains deployed on Phantasma
		/// <para><b>⚠️ Currently disabled - this functionality is not available and will be re-enabled according to the roadmap: https://phantasma.info/blockchain#roadmap</b></para>
		/// </summary>
		/// <param name="callback">Callback invoked with nexus metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests nexus metadata.</returns>
		public IEnumerator GetNexus(Action<NexusResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return WebClient.RPCRequest<NexusResult>(Host, ApiKey, "getNexus", timeout, retries, errorHandlingCallback, (result) =>
			{
				callback(result);
			});
		}
		#endregion

		#region Organization
		/// <summary>
		/// Gets organization data by registered name.
		/// </summary>
		/// <param name="name">Organization name.</param>
		/// <param name="callback">Callback invoked with organization data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests organization data by name.</returns>
		public IEnumerator GetOrganization(string name, Action<OrganizationResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return GetOrganization(name, false, callback, errorHandlingCallback, timeout, retries);
		}

		/// <summary>
		/// Gets organization data by registered name.
		/// </summary>
		/// <param name="name">Organization name.</param>
		/// <param name="includeMemberCount">True to include member count in the response.</param>
		/// <param name="callback">Callback invoked with organization data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests organization data by name.</returns>
		public IEnumerator GetOrganization(string name, bool includeMemberCount, Action<OrganizationResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getOrganization", callback, errorHandlingCallback, timeout, retries, name, includeMemberCount);
		}

		/// <summary>
		/// Gets organizations with cursor pagination.
		/// </summary>
		/// <param name="callback">Callback invoked with organization data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests organizations.</returns>
		public IEnumerator GetOrganizations(Action<CursorPaginatedResult<OrganizationResult[]>> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return GetOrganizations(10, "", false, callback, errorHandlingCallback, timeout, retries);
		}

		/// <summary>
		/// Gets organizations with cursor pagination.
		/// </summary>
		/// <param name="pageSize">Maximum number of organizations to return.</param>
		/// <param name="cursor">Pagination cursor.</param>
		/// <param name="includeMemberCount">True to include member count in each organization.</param>
		/// <param name="callback">Callback invoked with organization data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests organizations.</returns>
		public IEnumerator GetOrganizations(uint pageSize, string cursor, bool includeMemberCount, Action<CursorPaginatedResult<OrganizationResult[]>> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getOrganizations", callback, errorHandlingCallback, timeout, retries, pageSize, cursor, includeMemberCount);
		}

		/// <summary>
		/// Gets organization members by registered name.
		/// </summary>
		/// <param name="name">Organization name.</param>
		/// <param name="pageSize">Maximum number of members to return.</param>
		/// <param name="cursor">Pagination cursor.</param>
		/// <param name="includeMemberTime">True to include member timestamp.</param>
		/// <param name="callback">Callback invoked with organization members.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests organization members.</returns>
		public IEnumerator GetOrganizationMembers(string name, uint pageSize, string cursor, bool includeMemberTime, Action<CursorPaginatedResult<OrganizationMemberResult[]>> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getOrganizationMembers", callback, errorHandlingCallback, timeout, retries, name, pageSize, cursor, includeMemberTime);
		}

		/// <summary>
		/// Gets one organization membership by registered name.
		/// </summary>
		/// <param name="name">Organization name.</param>
		/// <param name="address">Member address.</param>
		/// <param name="checkAddressReservedByte">True to enforce reserved-byte validation.</param>
		/// <param name="addressType">Member address type.</param>
		/// <param name="callback">Callback invoked with organization member data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests organization member data.</returns>
		public IEnumerator GetOrganizationMember(string name, string address, bool checkAddressReservedByte, RpcAddressType addressType, Action<OrganizationMemberResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getOrganizationMember", callback, errorHandlingCallback, timeout, retries, name, address, checkAddressReservedByte, addressType);
		}
		#endregion

		#region Token
		private int tokensLoadedSimultaneously = 0;

		/// <summary>
		/// Gets token metadata by symbol
		/// </summary>
		/// <param name="symbol">Token symbol.</param>
		/// <param name="callback">Callback invoked with token metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests token metadata by symbol.</returns>
		public IEnumerator GetToken(string symbol, Action<TokenResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getToken", callback, errorHandlingCallback, timeout, retries, symbol);
		}

		/// <summary>
		/// Gets token metadata by symbol or carbon token id
		/// </summary>
		/// <param name="symbol">Token symbol, or an empty string when selecting by Carbon token ID.</param>
		/// <param name="extended">True to include extended token data.</param>
		/// <param name="carbonTokenId">Carbon token ID, or zero when selecting by symbol.</param>
		/// <param name="callback">Callback invoked with token metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests token metadata by symbol or Carbon token ID.</returns>
		public IEnumerator GetToken(string symbol, bool extended, ulong carbonTokenId, Action<TokenResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getToken", callback, errorHandlingCallback, timeout, retries, symbol, extended, carbonTokenId);
		}

		/// <summary>
		/// Gets an array of all tokens deployed on Phantasma
		/// </summary>
		/// <param name="callback">Callback invoked with token metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests all tokens.</returns>
		public IEnumerator GetTokens(Action<TokenResult[]> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getTokens", callback, errorHandlingCallback, timeout, retries);
		}

		/// <summary>
		/// Gets an array of all tokens deployed on Phantasma with extended payload enabled
		/// </summary>
		/// <param name="extended">True to include extended token data. Deprecated server-side and slated for removal.</param>
		/// <param name="callback">Callback invoked with token metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests all tokens.</returns>
		public IEnumerator GetTokens(bool extended, Action<TokenResult[]> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getTokens", callback, errorHandlingCallback, timeout, retries, extended, null);
		}

		/// <summary>
		/// Gets an array of all tokens deployed on Phantasma with optional owner filtering
		/// </summary>
		/// <param name="extended">True to include extended token data. Deprecated server-side and slated for removal.</param>
		/// <param name="ownerAddress">Optional owner address filter.</param>
		/// <param name="callback">Callback invoked with token metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests tokens, optionally filtered by owner.</returns>
		public IEnumerator GetTokens(bool extended, string ownerAddress, Action<TokenResult[]> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getTokens", callback, errorHandlingCallback, timeout, retries, extended, ownerAddress);
		}

		/// <summary>
		/// Gets an array of all tokens deployed on Phantasma with optional owner filtering and explicit owner address type
		/// </summary>
		/// <param name="extended">True to include extended token data. Deprecated server-side and slated for removal.</param>
		/// <param name="ownerAddress">Optional owner address filter.</param>
		/// <param name="addressType">Owner address type.</param>
		/// <param name="callback">Callback invoked with token metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests tokens, optionally filtered by owner.</returns>
		public IEnumerator GetTokens(bool extended, string ownerAddress, RpcAddressType addressType, Action<TokenResult[]> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getTokens", callback, errorHandlingCallback, timeout, retries, extended, ownerAddress, addressType);
		}

		/// <summary>
		/// Gets token series for a token (cursor pagination)
		/// </summary>
		/// <param name="callback">Callback invoked with cursor-paginated token series metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests token series.</returns>
		public IEnumerator GetTokenSeries(Action<CursorPaginatedResult<TokenSeriesResult[]>> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return GetTokenSeries("", 0, 10, "", callback, errorHandlingCallback, timeout, retries);
		}

		/// <summary>
		/// Gets token series for a token (cursor pagination)
		/// </summary>
		/// <param name="symbol">Token symbol, or an empty string when selecting by Carbon token ID.</param>
		/// <param name="carbonTokenId">Carbon token ID, or zero when selecting by symbol.</param>
		/// <param name="pageSize">Maximum number of items to return.</param>
		/// <param name="cursor">Cursor returned by a previous page, or an empty string for the first page.</param>
		/// <param name="callback">Callback invoked with cursor-paginated token series metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests token series.</returns>
		public IEnumerator GetTokenSeries(string symbol, ulong carbonTokenId, uint pageSize, string cursor, Action<CursorPaginatedResult<TokenSeriesResult[]>> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getTokenSeries", callback, errorHandlingCallback, timeout, retries, symbol, carbonTokenId, pageSize, cursor);
		}

		/// <summary>
		/// Gets a single token series by either Phantasma series id or Carbon series id
		/// </summary>
		/// <param name="symbol">Token symbol, or an empty string when selecting by Carbon token ID.</param>
		/// <param name="carbonTokenId">Carbon token ID, or zero when selecting by symbol.</param>
		/// <param name="seriesId">Phantasma series ID, or an empty string when selecting by Carbon series ID.</param>
		/// <param name="carbonSeriesId">Carbon series ID, or zero when selecting by Phantasma series ID.</param>
		/// <param name="callback">Callback invoked with token series metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests one token series.</returns>
		public IEnumerator GetTokenSeriesById(string symbol, ulong carbonTokenId, string seriesId, uint carbonSeriesId, Action<TokenSeriesResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getTokenSeriesById", callback, errorHandlingCallback, timeout, retries, symbol, carbonTokenId, seriesId, carbonSeriesId);
		}

		/// <summary>
		/// Gets NFTs for a token (cursor pagination)
		/// </summary>
		/// <param name="carbonTokenId">Carbon token ID.</param>
		/// <param name="callback">Callback invoked with cursor-paginated NFT data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests NFTs for a token.</returns>
		public IEnumerator GetTokenNFTs(ulong carbonTokenId, Action<CursorPaginatedResult<TokenDataResult[]>> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return GetTokenNFTs(carbonTokenId, 0, 10, "", false, "", callback, errorHandlingCallback, timeout, retries);
		}

		/// <summary>
		/// Gets NFTs for a token (cursor pagination)
		/// </summary>
		/// <param name="carbonTokenId">Carbon token ID.</param>
		/// <param name="carbonSeriesId">Optional Carbon series ID filter.</param>
		/// <param name="pageSize">Maximum number of items to return.</param>
		/// <param name="cursor">Cursor returned by a previous page, or an empty string for the first page.</param>
		/// <param name="extended">True to include NFT properties.</param>
		/// <param name="seriesId">Optional Phantasma series ID filter.</param>
		/// <param name="callback">Callback invoked with cursor-paginated NFT data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests NFTs for a token.</returns>
		public IEnumerator GetTokenNFTs(ulong carbonTokenId, uint carbonSeriesId, uint pageSize, string cursor, bool extended, string seriesId, Action<CursorPaginatedResult<TokenDataResult[]>> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getTokenNFTs", callback, errorHandlingCallback, timeout, retries, carbonTokenId, carbonSeriesId, pageSize, cursor, extended, seriesId);
		}

		/// <summary>
		/// Gets token data for a specific token id
		/// <para><b>⚠️ This functionality is only partially implemented - some features may be missing or incomplete. See the roadmap for planned updates: https://phantasma.info/blockchain#roadmap</b></para>
		/// </summary>
		/// <param name="symbol">Token symbol.</param>
		/// <param name="IDtext">Token ID text.</param>
		/// <param name="callback">Callback invoked with token data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests token data.</returns>
		/// <remarks>
		/// Deprecated. The node serves this as a strict subset of getNFT - same response, with
		/// property loading forced off - so GetNFT covers it entirely.
		/// </remarks>
		[Obsolete("Use GetNFT; getTokenData is a strict subset of getNFT.")]
		public IEnumerator GetTokenData(string symbol, string IDtext, Action<TokenDataResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			while (tokensLoadedSimultaneously > 5)
			{
				yield return null;
			}
			tokensLoadedSimultaneously++;

			yield return WebClient.RPCRequest<TokenDataResult>(Host, ApiKey, "getTokenData", timeout, retries, errorHandlingCallback, (result) =>
			{
				callback(result);
			}, symbol, IDtext);

			tokensLoadedSimultaneously--;
		}

		/// <summary>
		/// Gets NFT data and optionally loads properties
		/// <para><b>⚠️ This functionality is only partially implemented - some features may be missing or incomplete. See the roadmap for planned updates: https://phantasma.info/blockchain#roadmap</b></para>
		/// </summary>
		/// <param name="symbol">NFT symbol.</param>
		/// <param name="IDtext">Token ID text.</param>
		/// <param name="loadProperties">True to load NFT properties.</param>
		/// <param name="callback">Callback invoked with NFT data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests NFT data.</returns>
		public IEnumerator GetNFT(string symbol, string IDtext, bool loadProperties, Action<TokenDataResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			while (tokensLoadedSimultaneously > 5)
			{
				yield return null;
			}
			tokensLoadedSimultaneously++;

			yield return WebClient.RPCRequest<TokenDataResult>(Host, ApiKey, "getNFT", timeout, retries, errorHandlingCallback, (result) =>
			{
				// TODO remove later, check if still required
				if (string.IsNullOrEmpty(result.Id))
				{
					result.Id = IDtext;
				}

				callback(result);
			}, symbol, IDtext, loadProperties);

			tokensLoadedSimultaneously--;
		}

		/// <summary>
		/// Gets NFT data for multiple token ids
		/// <para><b>⚠️ Currently disabled - this functionality is not available and will be re-enabled according to the roadmap: https://phantasma.info/blockchain#roadmap</b></para>
		/// </summary>
		/// <param name="symbol">NFT symbol.</param>
		/// <param name="IDtext">Token ID texts.</param>
		/// <param name="callback">Callback invoked with NFT data for the requested token IDs.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests NFT data for multiple token IDs.</returns>
		public IEnumerator GetNFTs(string symbol, string[] IDtext, Action<TokenDataResult[]> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return GetNFTs(symbol, IDtext, false, callback, errorHandlingCallback, timeout, retries);
		}

		/// <summary>
		/// Gets NFT data for multiple token ids
		/// </summary>
		/// <param name="symbol">NFT symbol.</param>
		/// <param name="IDtext">Token ID texts.</param>
		/// <param name="extended">True to include NFT properties.</param>
		/// <param name="callback">Callback invoked with NFT data for the requested token IDs.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests NFT data for multiple token IDs.</returns>
		public IEnumerator GetNFTs(string symbol, string[] IDtext, bool extended, Action<TokenDataResult[]> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			while (tokensLoadedSimultaneously > 5)
			{
				yield return null;
			}

			tokensLoadedSimultaneously++;

			/*
             * Carbon RPC expects a single comma-delimited token id string here.
             * Sending the raw array breaks parity with the current C# SDK wrapper and
             * produces a different JSON payload than the node endpoint validates.
             */
			yield return RpcRequest("getNFTs", callback, errorHandlingCallback, timeout, retries, symbol, String.Join(",", IDtext ?? Array.Empty<string>()), extended);

			tokensLoadedSimultaneously--;
		}

		/// <summary>
		/// Gets the token balance for a given address and token symbol
		/// </summary>
		/// <param name="account">Account address text.</param>
		/// <param name="tokenSymbol">Token symbol.</param>
		/// <param name="chainInput">Chain address or chain name. Defaults to main.</param>
		/// <param name="callback">Callback invoked with token balance data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests an account token balance.</returns>
		public IEnumerator GetTokenBalance(string account, string tokenSymbol, string chainInput = "main", Action<BalanceResult> callback = null, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return WebClient.RPCRequest<BalanceResult>(Host, ApiKey, "getTokenBalance", timeout, retries, errorHandlingCallback, (result) =>
			{
				callback(result);
			}, account, tokenSymbol, chainInput);
		}

		/// <summary>
		/// Gets the token balance for a given address, token symbol and address type
		/// </summary>
		/// <param name="account">Account address text.</param>
		/// <param name="tokenSymbol">Token symbol.</param>
		/// <param name="chainInput">Chain address or chain name.</param>
		/// <param name="checkAddressReservedByte">True to enforce reserved-byte validation.</param>
		/// <param name="addressType">Account address type.</param>
		/// <param name="callback">Callback invoked with token balance data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests an account token balance.</returns>
		public IEnumerator GetTokenBalance(string account, string tokenSymbol, string chainInput, bool checkAddressReservedByte, RpcAddressType addressType, Action<BalanceResult> callback = null, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("getTokenBalance", callback, errorHandlingCallback, timeout, retries, account, tokenSymbol, chainInput, checkAddressReservedByte, addressType);
		}
		#endregion

		#region Transaction
		/// <summary>
		/// Gets address transactions with pagination
		/// This api call is paginated, multiple calls might be required to obtain a complete result 
		/// </summary>
		/// <param name="addressText">Account address text.</param>
		/// <param name="page">Page number to request.</param>
		/// <param name="pageSize">Maximum number of transactions to return.</param>
		/// <param name="callback">Callback invoked with transaction page data, current page, and total page count.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests paginated address transactions.</returns>
		public IEnumerator GetAddressTransactions(string addressText, uint page, uint pageSize, Action<AccountTransactionsResult, uint, uint> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return WebClient.RPCRequest<PaginatedResult<AccountTransactionsResult>>(Host, ApiKey, "getAddressTransactions", timeout, retries, errorHandlingCallback, (result) =>
			{
				callback(result.Result, result.Page, result.TotalPages);
			}, addressText, page, pageSize);
		}

		/// <summary>
		/// Gets the number of transactions for an address on a chain
		/// </summary>
		/// <param name="addressText">Account address text.</param>
		/// <param name="chainInput">Chain address or chain name.</param>
		/// <param name="callback">Callback invoked with the transaction count.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests the number of address transactions on a chain.</returns>
		public IEnumerator GetAddressTransactionCount(string addressText, string chainInput, Action<int> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return WebClient.RPCRequest<string>(Host, ApiKey, "getAddressTransactionCount", timeout, retries, errorHandlingCallback, (result) =>
			{
				callback(int.Parse(result));
			}, addressText, chainInput);
		}

		/// <summary>
		/// Broadcasts a transaction in hexadecimal encoding
		/// </summary>
		/// <param name="txData">Hex-encoded transaction bytes.</param>
		/// <param name="txHash">Locally computed transaction hash expected from the node.</param>
		/// <param name="callback">Callback invoked with the RPC hash text, encoded transaction, and expected hash.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that broadcasts a transaction.</returns>
		public IEnumerator SendRawTransaction(string txData, Hash txHash, Action<string, string, Hash> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("sendRawTransaction", (string result) =>
			{
				callback(result, txData, txHash);
			}, errorHandlingCallback, timeout, retries, txData);
		}

		/// <summary>
		/// Broadcasts a carbon transaction in hexadecimal encoding
		/// </summary>
		/// <param name="txData">Hex-encoded Carbon transaction bytes.</param>
		/// <param name="callback">Callback invoked with the RPC hash text and encoded transaction.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that broadcasts a Carbon transaction.</returns>
		public IEnumerator SendCarbonTransaction(string txData, Action<string, string> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("sendCarbonTransaction", (string result) =>
			{
				callback(result, txData);
			}, errorHandlingCallback, timeout, retries, txData);
		}

		/// <summary>
		/// Invokes a VM script without state changes and returns its result
		/// <para><b>⚠️ Currently disabled - this functionality is not available and will be re-enabled according to the roadmap: https://phantasma.info/blockchain#roadmap</b></para>
		/// </summary>
		/// <param name="chainInput">Chain address or chain name.</param>
		/// <param name="scriptData">Hex-encoded VM script bytes.</param>
		/// <param name="callback">Callback invoked with the script invocation result.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that invokes a VM script without committing state.</returns>
		public IEnumerator InvokeRawScript(string chainInput, string scriptData, Action<ScriptResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return WebClient.RPCRequest<ScriptResult>(Host, ApiKey, "invokeRawScript", timeout, retries, errorHandlingCallback, (result) =>
			{
				callback(result);
			}, chainInput, scriptData);
		}

		/// <summary>
		/// Dry-runs a serialized transaction envelope against current chain state and returns its exact
		/// fee bill with recommended maxGas/maxData (gas-model-v2 Tier-2 estimate)
		/// </summary>
		/// <remarks>
		/// Signatures inside the envelope may be zero-filled dummies of the correct length: the
		/// simulation skips signature checks, and dummies preserve the exact envelope byte length the
		/// bill depends on. The endpoint routes through the node's query plane and answers a standard
		/// RPC error where that plane is not deployed; the Tier-1 NativeFeeEstimator fed by
		/// <see cref="GetGasConfig"/> covers that case.
		/// </remarks>
		/// <param name="txData">Hex-encoded serialized transaction envelope (signed or dummy-signed).</param>
		/// <param name="callback">Callback invoked with the fee estimate.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that estimates the fee of a transaction envelope.</returns>
		public IEnumerator EstimateTransaction(string txData, Action<EstimateTransactionResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return RpcRequest("estimateTransaction", callback, errorHandlingCallback, timeout, retries, txData);
		}

		/// <summary>
		/// Gets a transaction by its hash if available
		/// </summary>
		/// <param name="hashText">Transaction hash text.</param>
		/// <param name="callback">Callback invoked with transaction data.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests a transaction by hash.</returns>
		public IEnumerator GetTransaction(string hashText, Action<TransactionResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return WebClient.RPCRequest<TransactionResult>(Host, ApiKey, "getTransaction", timeout, retries, errorHandlingCallback, (result) =>
			{
				callback(result);
			}, hashText);
		}
		#endregion

		#region Storage
		/// <summary>
		/// Gets archive metadata by its hash
		/// <para><b>⚠️ Currently disabled - this functionality is not available and will be re-enabled according to the roadmap: https://phantasma.info/blockchain#roadmap</b></para>
		/// </summary>
		/// <param name="hashText">Archive hash text.</param>
		/// <param name="callback">Callback invoked with archive metadata.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that requests archive metadata.</returns>
		public IEnumerator GetArchive(string hashText, Action<ArchiveResult> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return WebClient.RPCRequest<ArchiveResult>(Host, ApiKey, "getArchive", timeout, retries, errorHandlingCallback, (result) =>
			{
				callback(result);
			}, hashText);
		}

		/// <summary>
		/// Writes a single archive block
		/// <para><b>⚠️ Currently disabled - this functionality is not available and will be re-enabled according to the roadmap: https://phantasma.info/blockchain#roadmap</b></para>
		/// </summary>
		/// <param name="hashText">Archive hash text.</param>
		/// <param name="blockIndex">Archive block index, starting at zero.</param>
		/// <param name="blockContent">Raw archive block bytes to send as Base64.</param>
		/// <param name="callback">Callback invoked with true when the archive block write succeeds.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that writes one archive block.</returns>
		public IEnumerator WriteArchive(string hashText, int blockIndex, byte[] blockContent, Action<Boolean> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return WebClient.RPCRequest<string>(Host, ApiKey, "writeArchive", timeout, retries, errorHandlingCallback, (result) =>
			{
				callback(Boolean.Parse(result));
			}, hashText, blockIndex, Convert.ToBase64String(blockContent));
		}

		/// <summary>
		/// Reads a single archive block as a base64 string
		/// <para><b>⚠️ Currently disabled - this functionality is not available and will be re-enabled according to the roadmap: https://phantasma.info/blockchain#roadmap</b></para>
		/// </summary>
		/// <param name="hashText">Archive hash text.</param>
		/// <param name="blockIndex">Archive block index, starting at zero.</param>
		/// <param name="callback">Callback invoked with Base64 archive block content.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the request fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that reads one archive block.</returns>
		public IEnumerator ReadArchive(string hashText, int blockIndex, Action<string> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			yield return WebClient.RPCRequest<string>(Host, ApiKey, "readArchive", timeout, retries, errorHandlingCallback, (result) =>
			{
				callback(result);
			}, hashText, blockIndex);
		}
		#endregion

		#region Other Transaction Methods
		/// <summary>
		/// Sign and send a transaction with the payload
		/// </summary>
		/// <param name="keys">Key pair used to sign the transaction.</param>
		/// <param name="nexus">Nexus name used in the transaction.</param>
		/// <param name="script">Transaction script bytes.</param>
		/// <param name="chain">Target chain name.</param>
		/// <param name="payload">UTF-8 payload string.</param>
		/// <param name="callback">Callback invoked with transaction hash text and encoded transaction.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when signing or broadcast fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that builds, signs, broadcasts, and verifies a transaction.</returns>
		public IEnumerator SignAndSendTransaction(IKeyPair keys, string nexus, byte[] script, string chain, string payload, Action<string /*tx hash*/, string /*encoded tx*/> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			var payloadBytes = string.IsNullOrEmpty(payload) ? Array.Empty<byte>() : Encoding.UTF8.GetBytes(payload);
			return SignAndSendTransaction(keys, nexus, script, chain, payloadBytes, callback, errorHandlingCallback, null, timeout, retries);
		}

		/// <summary>
		/// Sign and send a transaction with the payload
		/// </summary>
		/// <param name="keys">Key pair used to sign the transaction.</param>
		/// <param name="nexus">Nexus name used in the transaction.</param>
		/// <param name="script">Transaction script bytes.</param>
		/// <param name="chain">Target chain name.</param>
		/// <param name="payload">Binary payload bytes.</param>
		/// <param name="callback">Callback invoked with transaction hash text and encoded transaction.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when signing or broadcast fails.</param>
		/// <param name="customSignFunction">Optional custom signer that receives data, script bytes, and payload bytes.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that builds, signs, broadcasts, and verifies a transaction.</returns>
		public IEnumerator SignAndSendTransaction(IKeyPair keys, string nexus, byte[] script, string chain, byte[] payload, Action<string /*tx hash*/, string /*encoded tx*/> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, Func<byte[], byte[], byte[], byte[]> customSignFunction = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			Log.Write("Sending transaction... script size: " + script.Length);

			var tx = new PhantasmaPhoenix.Protocol.Transaction(nexus, chain, script, DateTime.UtcNow + TimeSpan.FromMinutes(20), payload ?? Array.Empty<byte>());

			// Local hash we expect to see on the node
			Hash txHash = tx.Sign(keys, customSignFunction);

			var encoded = Base16.Encode(tx.ToByteArray(true));

			Action<string, string, Hash> wrappedCallback = (hashText, encodedTx, expectedHash) =>
			{
				if (hashText != expectedHash.ToString())
				{
					errorHandlingCallback?.Invoke(EPHANTASMA_SDK_ERROR_TYPE.API_ERROR, $"RPC returned different hash {hashText}, expected {expectedHash}");
					return;
				}

				callback?.Invoke(hashText, encodedTx);
			};

			yield return SendRawTransaction(encoded, txHash, wrappedCallback, errorHandlingCallback, timeout, retries);
		}

		// How many rows one page of an infusion query asks for, and how many pages are ever read. The
		// page size matches the plain .NET SDK, so both read a given address in the same number of calls.
		private const uint InfusionPageSize = 100;
		private const int InfusionMaxPages = 1000;

		/// <summary>
		/// Signs a carbon transaction with one key and broadcasts it
		/// </summary>
		/// <remarks>
		/// A message that carries no gas offer is planned first. The overload that takes several keys
		/// describes how.
		/// </remarks>
		/// <param name="keys">Key pair used to sign the Carbon transaction.</param>
		/// <param name="txMsg">Carbon TxMsg value to plan, sign and serialize.</param>
		/// <param name="callback">Callback invoked with transaction hash text and encoded transaction.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when planning, signing or broadcast fails.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that plans, signs, serializes, and broadcasts a Carbon transaction.</returns>
		public IEnumerator SignAndSendCarbonTransaction(IKeyPair keys, TxMsg txMsg, Action<string /*tx hash*/, string /*encoded tx*/> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			return SignAndSendCarbonTransaction(new[] { keys }, txMsg, null, callback, errorHandlingCallback, timeout: timeout, retries: retries);
		}

		/// <summary>
		/// Plans the fee of a carbon transaction, signs it with every key it needs, and broadcasts it
		/// </summary>
		/// <remarks>
		/// A message whose maxGas is zero is priced against the chain's current gas configuration before it
		/// is signed. The chain refuses an offer of zero, and it aborts a transaction that spends more than
		/// it offered. A message whose maxGas the caller already set is signed as it is.
		/// <para>
		/// A token creation is also checked against the chain first. Its symbol is asked for, because the
		/// call spends the policy fee before the contract looks at the symbol. That fee is the largest
		/// single price in the protocol, and a symbol that is already taken pays it for nothing.
		/// </para>
		/// </remarks>
		/// <param name="keys">One key pair per witness the transaction needs. A gas-payer transfer takes the gas payer and the owner, in any order.</param>
		/// <param name="txMsg">Carbon TxMsg value to plan, sign and serialize.</param>
		/// <param name="options">Fee planning options, for example the expiry, or what a burned NFT holds. Null takes the defaults.</param>
		/// <param name="callback">Callback invoked with transaction hash text and encoded transaction.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the pre-flight refuses the transaction, or when planning, signing or broadcast fails.</param>
		/// <param name="preflight">True asks the chain whether the symbol of a token creation is already taken, before anything is signed. It does not affect any other message.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that plans, signs, serializes, and broadcasts a Carbon transaction.</returns>
		public IEnumerator SignAndSendCarbonTransaction(IKeyPair[] keys, TxMsg txMsg, PlanAndSignOptions options, Action<string /*tx hash*/, string /*encoded tx*/> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, bool preflight = true, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			Log.Write("Sending carbon transaction...");

			// The pre-flight needs the gas token id out of the gas configuration, so a token creation reads
			// that configuration as well, and not only a message that still has to be priced.
			var createTokenSymbol = preflight ? CreateTokenSymbol(txMsg) : null;

			GasConfig? config = null;
			if (txMsg.maxGas == 0 || createTokenSymbol != null)
			{
				yield return ReadGasConfig(result => config = result, errorHandlingCallback, timeout, retries);

				if (config == null)
					yield break;
			}

			if (createTokenSymbol != null)
			{
				string refusal = null;
				yield return PreflightCreateToken(createTokenSymbol, config.Value.gasTokenId, reason => refusal = reason, timeout, retries);

				if (refusal != null)
				{
					errorHandlingCallback?.Invoke(EPHANTASMA_SDK_ERROR_TYPE.API_ERROR, refusal);
					yield break;
				}
			}

			// A burn returns whatever the NFT holds at its own address, and the chain charges for every
			// returned asset. That is chain state the message does not carry, and there is no costlier
			// reading of it to fall back on, so a burn cannot be priced until it is read. A caller who
			// already knows it passes it in the options, and an empty list there says the NFT holds
			// nothing. A message the caller priced needs none of this.
			if (txMsg.maxGas == 0)
			{
				string failure = null;
				yield return ResolveInfusions(txMsg, options, filled => options = filled, reason => failure = reason, timeout, retries);

				if (failure != null)
				{
					errorHandlingCallback?.Invoke(EPHANTASMA_SDK_ERROR_TYPE.API_ERROR, failure);
					yield break;
				}
			}

			string encoded = null;
			try
			{
				// PlanAndSign reads the gas offer out of the message. A message the caller priced is signed as
				// it is, and the configuration is not used.
				encoded = Base16.Encode(PlanAndSign.WithKeys(txMsg, keys, config ?? new GasConfig(), options));
			}
			catch (Exception error)
			{
				// The planner refuses a message it cannot price, for example a burn whose infusions were not
				// given. The signer refuses a key set that does not match the witnesses the message names.
				errorHandlingCallback?.Invoke(EPHANTASMA_SDK_ERROR_TYPE.API_ERROR, error.Message);
			}

			if (encoded == null)
				yield break;

			// Send to network and validate on callback
			yield return SendCarbonTransaction(encoded, callback, errorHandlingCallback, timeout, retries);
		}

		// Returns the symbol a CreateToken claims, or null when the message creates no token. A message
		// whose arguments cannot be read as a token also returns null. The planner reads the same
		// arguments a moment later and reports the real fault.
		private static string CreateTokenSymbol(TxMsg msg)
		{
			// TxTypes.Call is zero, so a TxMsg the caller built by hand and did not finish reads as a call
			// with no body. The type alone does not promise the body is there. Reading it as a call anyway
			// would throw out of the coroutine, and a coroutine cannot hand an exception back to whoever
			// started it, so the caller would get nothing at all.
			if (msg.type != TxTypes.Call || !(msg.msg is TxMsgCall call))
				return null;

			if (call.moduleId != (uint)ModuleId.Token || call.methodId != (uint)TokenContract_Methods.CreateToken)
				return null;

			try
			{
				var symbol = CarbonBlob.New<TokenInfo>(call.args).symbol.data;
				return string.IsNullOrEmpty(symbol) ? null : symbol;
			}
			catch (Exception)
			{
				return null;
			}
		}

		/// <summary>
		/// Plans the fee of a carbon transaction against the chain's current prices, and signs nothing
		/// </summary>
		/// <remarks>
		/// This is what a wallet needs before it asks the user to confirm: the plan exists before anything
		/// is signed, so the cost can be shown and the balance checked. Apply it to the message with
		/// <c>FeePlan.Apply</c>, and the send path then signs that message as it is.
		/// <para>
		/// A burn is priced from what the burned NFT holds at its own address, which is chain state the
		/// message does not carry. A caller who states it in <paramref name="options"/> is taken at their
		/// word, and an empty list there says the NFT holds nothing. A caller who states nothing has it
		/// read here, over the same queries.
		/// </para>
		/// <para>
		/// This is the Unity equivalent of <c>PhantasmaAPI.Fees.PlanAsync</c> in the plain .NET SDK. That
		/// one is not usable here, see the note above <c>ReadInfusedAssets</c>.
		/// </para>
		/// <para>
		/// This method prices the message and checks nothing else. A successful plan says the message can
		/// be priced. It says nothing about whether the chain will accept it. The send path runs the
		/// pre-flight.
		/// </para>
		/// </remarks>
		/// <param name="keys">The same key set the message will be signed with. A Call, a Call_Multi, a Trade and a Phantasma message choose their own witnesses, and the size of the envelope depends on how many there are, so the price depends on this.</param>
		/// <param name="txMsg">Carbon TxMsg value to price. The message is not changed.</param>
		/// <param name="options">Fee planning options, for example what a burned NFT holds. Null takes the defaults.</param>
		/// <param name="callback">Callback invoked with the plan.</param>
		/// <param name="errorHandlingCallback">Callback invoked with SDK error type and message when the chain cannot be read or the message cannot be priced.</param>
		/// <param name="timeout">Request timeout in seconds.</param>
		/// <param name="retries">Number of retry attempts.</param>
		/// <returns>Coroutine that plans the fee of a Carbon transaction.</returns>
		public IEnumerator PlanCarbonTransaction(IKeyPair[] keys, TxMsg txMsg, PlanAndSignOptions options, Action<FeePlan> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries)
		{
			if (keys == null)
			{
				errorHandlingCallback?.Invoke(EPHANTASMA_SDK_ERROR_TYPE.API_ERROR, "Pass the keys the message will be signed with: the price depends on how many witnesses sign it");
				yield break;
			}

			GasConfig? config = null;
			yield return ReadGasConfig(result => config = result, errorHandlingCallback, timeout, retries);

			if (config == null)
				yield break;

			string failure = null;
			yield return ResolveInfusions(txMsg, options, filled => options = filled, reason => failure = reason, timeout, retries);

			if (failure != null)
			{
				errorHandlingCallback?.Invoke(EPHANTASMA_SDK_ERROR_TYPE.API_ERROR, failure);
				yield break;
			}

			FeePlan plan = null;
			try
			{
				plan = FeePlanner.Plan(txMsg, config.Value, PlanOptionsFor(txMsg, keys, options));
			}
			catch (Exception error)
			{
				// The planner refuses a message it cannot price.
				errorHandlingCallback?.Invoke(EPHANTASMA_SDK_ERROR_TYPE.API_ERROR, error.Message);
			}

			if (plan != null)
				callback?.Invoke(plan);
		}

		// The planning options a message and its key set imply.
		//
		// Only the witness-array types take their witness count from the caller: Call, Call_Multi, Trade
		// and Phantasma. Every other type fixes its own slots in the message, and one key may fill two of
		// them, so a count would contradict the message.
		//
		// PlanAndSign.WithKeys applies this same rule before it plans, and the two have to stay equal. A
		// plan sized for fewer witnesses than the signature carries offers too little gas, and the chain
		// bills the whole offer when a transaction runs past what it offered. The test
		// PlanCarbonTransaction_ForACallMulti_PlansWhatTheSendPathWillSign fails if they ever diverge.
		private static PlanAndSignOptions PlanOptionsFor(TxMsg txMsg, IKeyPair[] keys, PlanAndSignOptions options)
		{
			var planned = (PlanAndSignOptions)(options ?? new PlanAndSignOptions()).Clone();

			if (!planned.WitnessCount.HasValue && SignedTxMsg.RequiredWitnesses(txMsg) == null)
				planned.WitnessCount = keys.Length;

			return planned;
		}

		// The chain's gas configuration, or null when it could not be read. Every failure reports through
		// <paramref name="errorHandlingCallback"/> and leaves the result null. Planning against a zero
		// configuration would offer zero gas, and the chain refuses that offer.
		private IEnumerator ReadGasConfig(Action<GasConfig?> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback, int timeout, int retries)
		{
			yield return GetGasConfig(result =>
			{
				if (result == null)
				{
					errorHandlingCallback?.Invoke(EPHANTASMA_SDK_ERROR_TYPE.MALFORMED_RESPONSE, "getGasConfig returned no result");
					return;
				}

				try
				{
					callback(result.ToGasConfig());
				}
				catch (Exception error)
				{
					errorHandlingCallback?.Invoke(EPHANTASMA_SDK_ERROR_TYPE.MALFORMED_RESPONSE, error.Message);
				}
			}, errorHandlingCallback, timeout, retries);
		}

		// Options with <c>Infusions</c> filled in from the chain when the message burns an NFT and the
		// caller did not state them. The caller's own options object is never changed, and the options are
		// passed through untouched when there is nothing to read.
		private IEnumerator ResolveInfusions(TxMsg txMsg, PlanAndSignOptions options, Action<PlanAndSignOptions> callback, Action<string> failure, int timeout, int retries)
		{
			if (options?.Infusions != null)
				yield break;

			var burned = BurnedInstances(txMsg);
			if (burned.Count == 0)
				yield break;

			var infusions = new List<InfusedAsset>();
			string reason = null;

			foreach (var instance in burned)
			{
				// Every instance is read at its own address, because the fee follows each returned asset
				// separately.
				yield return ReadInfusedAssets(instance.TokenId, instance.InstanceId, infusions, r => reason = r, timeout, retries);

				if (reason != null)
				{
					failure(reason);
					yield break;
				}
			}

			// Clone keeps the runtime type, so the caller's object is left as it was.
			var filled = (PlanAndSignOptions)(options ?? new PlanAndSignOptions()).Clone();
			filled.Infusions = infusions;
			callback(filled);
		}

		// Returns the instances a message burns, or an empty list when it burns none. A message whose
		// body cannot be read as its type promises also returns an empty list. The planner reads the same
		// body a moment later and reports the real fault through the error callback.
		private static IReadOnlyList<(ulong TokenId, ulong InstanceId)> BurnedInstances(TxMsg msg)
		{
			try
			{
				return FeePlanner.BurnedInstances(msg);
			}
			catch (Exception)
			{
				return Array.Empty<(ulong, ulong)>();
			}
		}

		// Adds what NFT <paramref name="instanceId"/> of token <paramref name="tokenId"/> holds at its
		// own address to <paramref name="assets"/>, in the form the fee planner prices. A failure is
		// reported through <paramref name="failure"/> and nothing is added.
		//
		// This is what PhantasmaPhoenix.RPC.PhantasmaAPI.InfusedAssetsAsync reads in the plain .NET SDK.
		// That class is not reused here because it carries its own HttpClient transport. This wrapper
		// exists so that every request goes through UnityWebRequest, which is the transport that works on
		// every Unity target, WebGL included. So the same queries are made here over this wrapper's own
		// transport instead.
		//
		// A fungible balance is resolved to a token id, because rows of the chain's gas and data tokens
		// are free and only the id tells which token a row belongs to. Whether the burner already holds
		// a returned token is left at the costlier reading, which moves the escrow ceiling alone.
		private IEnumerator ReadInfusedAssets(ulong tokenId, ulong instanceId, List<InfusedAsset> assets, Action<string> failure, int timeout, int retries)
		{
			var address = TokenHelper.GetNftAddress(tokenId, instanceId).ToHex();
			var found = new List<InfusedAsset>();

			var balances = new List<BalanceResult>();
			string reason = null;
			yield return ReadAllPages<BalanceResult>(
				(cursor, page, error) => GetAccountFungibleTokens(address, string.Empty, 0, InfusionPageSize, cursor, false, RpcAddressType.Carbon, page, error, timeout, retries),
				balances, r => reason = r);

			if (reason != null)
			{
				failure(reason);
				yield break;
			}

			foreach (var balance in balances)
			{
				TokenResult token = null;
				yield return GetToken(balance.Symbol, result => token = result, (type, message) => reason = message, timeout, retries);

				if (token == null)
				{
					failure($"Could not read what the burned NFT holds: getToken returned nothing for {balance.Symbol}" + (reason == null ? "" : ": " + reason));
					yield break;
				}

				found.Add(new InfusedAsset { TokenId = ulong.Parse(token.CarbonId, CultureInfo.InvariantCulture), NonFungible = false });
			}

			var owned = new List<TokenResult>();
			yield return ReadAllPages<TokenResult>(
				(cursor, page, error) => GetAccountOwnedTokens(address, string.Empty, 0, InfusionPageSize, cursor, false, RpcAddressType.Carbon, page, error, timeout, retries),
				owned, r => reason = r);

			if (reason != null)
			{
				failure(reason);
				yield break;
			}

			foreach (var token in owned)
			{
				BalanceResult balance = null;
				yield return GetTokenBalance(address, token.Symbol, "main", false, RpcAddressType.Carbon, result => balance = result, (type, message) => reason = message, timeout, retries);

				if (balance == null)
				{
					failure($"Could not read what the burned NFT holds: getTokenBalance returned nothing for {token.Symbol}" + (reason == null ? "" : ": " + reason));
					yield break;
				}

				found.Add(new InfusedAsset
				{
					TokenId = ulong.Parse(token.CarbonId, CultureInfo.InvariantCulture),
					NonFungible = true,
					InstanceCount = (uint)ulong.Parse(balance.Amount, CultureInfo.InvariantCulture)
				});
			}

			assets.AddRange(found);
		}

		// Walks a cursor-paginated query to the end and collects every item. Each page is asked for with
		// the cursor the node returned for the page before it. The loop stops on an empty cursor, on a
		// cursor it has already seen, and at the page cap. A node that keeps handing out fresh cursors
		// therefore cannot hold the coroutine forever.
		private IEnumerator ReadAllPages<T>(Func<string, Action<CursorPaginatedResult<T[]>>, Action<EPHANTASMA_SDK_ERROR_TYPE, string>, IEnumerator> page, List<T> items, Action<string> failure)
		{
			var seen = new HashSet<string>();
			var cursor = string.Empty;

			for (var i = 0; i < InfusionMaxPages; i++)
			{
				CursorPaginatedResult<T[]> result = null;
				string reason = null;
				yield return page(cursor, r => result = r, (type, message) => reason = message);

				if (result == null)
				{
					failure("Could not read what the burned NFT holds: " + (reason ?? "the query returned nothing"));
					yield break;
				}

				if (result.Result != null)
					items.AddRange(result.Result);

				if (string.IsNullOrEmpty(result.Cursor) || !seen.Add(result.Cursor))
					yield break;

				cursor = result.Cursor;
			}

			failure($"Could not read what the burned NFT holds: the node kept returning pages past {InfusionMaxPages}");
		}

		// Asks the chain whether a token symbol is already in use, and reports a refusal through
		// <paramref name="refusal"/>. Nothing is reported when the chain answered that the symbol is free.
		//
		// This is the rule PhantasmaPhoenix.RPC.TransactionPreflight applies in the plain .NET SDK. That
		// helper is not reused here because it reads through a PhantasmaAPI that carries its own
		// HttpClient transport. This wrapper exists so that every request goes through UnityWebRequest,
		// which is the transport that works on every Unity target, WebGL included. So the same two
		// lookups are made here over this wrapper's own transport instead.
		//
		// A symbol that resolves to a token is taken. A symbol that does not resolve comes back as an
		// ordinary RPC error, and the node reports an absent symbol, a missing method and a failed backend
		// in the same way. So the check asks a second question whose answer it already knows: it fetches
		// the gas token by its id, which every live chain has. A node that answers the control is serving
		// token lookups, and its refusal about the caller's symbol is then a real absence. A node that does
		// not answer the control has established nothing, and the transaction is refused for that reason.
		private IEnumerator PreflightCreateToken(string symbol, ulong controlTokenId, Action<string> refusal, int timeout, int retries)
		{
			TokenResult token = null;
			// An error here is the expected answer for a free symbol, so it is not reported to the caller.
			yield return GetToken(symbol, false, 0, result => token = result, (type, message) => { }, timeout, retries);

			if (token != null)
			{
				refusal($"Token symbol {symbol} is already taken");
				yield break;
			}

			if (controlTokenId == 0)
			{
				refusal($"Could not establish whether token symbol {symbol} is taken: the chain named no gas token to check the lookup against");
				yield break;
			}

			TokenResult control = null;
			string controlError = null;
			yield return GetToken(string.Empty, false, controlTokenId, result => control = result, (type, message) => controlError = message, timeout, retries);

			if (control == null)
				refusal($"Could not establish whether token symbol {symbol} is taken: {controlError ?? "the control lookup returned nothing"}");
		}
		#endregion

		/// <summary>
		/// Validates a WIF-formatted private key string
		/// </summary>
		/// <param name="key">WIF-formatted private key text.</param>
		/// <returns>True when the key starts with K or L and has WIF private-key length.</returns>
		public static bool IsValidPrivateKey(string key)
		{
			return (key.StartsWith("L", false, CultureInfo.InvariantCulture) ||
					key.StartsWith("K", false, CultureInfo.InvariantCulture)) && key.Length == 52;
		}

		/// <summary>
		/// Validates the format of a Phantasma address string
		/// </summary>
		/// <param name="address">Phantasma address text.</param>
		/// <returns>True when the address starts with P and has Phantasma address length.</returns>
		public static bool IsValidAddress(string address)
		{
			return address.StartsWith("P", false, CultureInfo.InvariantCulture) && address.Length == 45;
		}
	}
}
