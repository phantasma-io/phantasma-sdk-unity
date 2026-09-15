using System;
using System.Collections;
using System.Collections.Generic;
using Newtonsoft.Json;
using NUnit.Framework;
using PhantasmaPhoenix.Cryptography;
using PhantasmaPhoenix.Protocol.Carbon;
using PhantasmaPhoenix.Protocol.Carbon.Blockchain;
using PhantasmaPhoenix.Protocol.Carbon.Blockchain.Modules.Builders;
using PhantasmaPhoenix.Protocol.Carbon.Blockchain.TxHelpers;
using PhantasmaPhoenix.RPC.Models;
using PhantasmaPhoenix.RPC.Types;
using PhantasmaPhoenix.Unity.Core;

public class PhantasmaApiRpcRequestTests
{
	private sealed class CapturingPhantasmaApi : PhantasmaAPI
	{
		public string LastMethod { get; private set; }
		public object[] LastParameters { get; private set; }
		public int LastTimeout { get; private set; }
		public int LastRetries { get; private set; }
		public object NextResult { get; set; }
		public List<string> Methods { get; } = new List<string>();
		public List<object[]> Parameters { get; } = new List<object[]>();
		private readonly Dictionary<string, Queue<object>> answers = new Dictionary<string, Queue<object>>();

		/// <summary>Queues one answer for the next call of that RPC method. A path that calls the same
		/// method twice takes the answers in the order they were queued. A method with no queued answer
		/// falls back to NextResult.</summary>
		public CapturingPhantasmaApi Answer(string method, object result)
		{
			if (!answers.TryGetValue(method, out var queue))
			{
				queue = new Queue<object>();
				answers[method] = queue;
			}

			queue.Enqueue(result);
			return this;
		}

		public CapturingPhantasmaApi() : base("http://127.0.0.1:1/rpc")
		{
		}

		protected override IEnumerator RpcRequest<T>(string method, Action<T> callback, Action<EPHANTASMA_SDK_ERROR_TYPE, string> errorHandlingCallback = null, int timeout = WebClient.DefaultTimeout, int retries = WebClient.DefaultRetries, params object[] parameters)
		{
			LastMethod = method;
			LastParameters = parameters ?? Array.Empty<object>();
			LastTimeout = timeout;
			LastRetries = retries;
			Methods.Add(LastMethod);
			Parameters.Add(LastParameters);

			var answer = NextResult;
			if (answers.TryGetValue(method, out var queued) && queued.Count > 0)
			{
				answer = queued.Dequeue();
			}

			// A node that cannot answer reports an RPC error and never calls back. The pre-flight reads
			// an absent token symbol out of exactly that, so the fake has to be able to produce it.
			if (answer is RpcFailure failure)
			{
				errorHandlingCallback?.Invoke(EPHANTASMA_SDK_ERROR_TYPE.API_ERROR, failure.Message);
				yield break;
			}

			if (callback != null)
			{
				var result = answer is T typed ? typed : default;
				callback(result);
			}

			yield break;
		}
	}

	/// <summary>An RPC error in place of a result, for a call the node refuses to answer.</summary>
	private sealed class RpcFailure
	{
		public string Message { get; }

		public RpcFailure(string message)
		{
			Message = message;
		}
	}

	private static void RunCoroutine(IEnumerator coroutine)
	{
		while (coroutine.MoveNext())
		{
			if (coroutine.Current is IEnumerator nested)
			{
				RunCoroutine(nested);
			}
		}
	}

	private static void AssertCall(CapturingPhantasmaApi api, string expectedMethod, params object[] expectedParameters)
	{
		Assert.That(api.LastMethod, Is.EqualTo(expectedMethod));
		Assert.That(api.LastParameters, Is.EqualTo(expectedParameters));
	}

	private static PhantasmaKeys CreateDeterministicKeys()
	{
		return CreateDeterministicKeys(1);
	}

	// Distinct seeds give distinct accounts, so a test can tell the gas payer from the token owner.
	private static PhantasmaKeys CreateDeterministicKeys(byte seed)
	{
		var privateKey = new byte[PhantasmaKeys.PrivateKeyLength];
		for (var i = 0; i < privateKey.Length; i++)
		{
			privateKey[i] = (byte)(i + seed);
		}

		return new PhantasmaKeys(privateKey);
	}

	[Test]
	public void RpcResponseDecoder_WithMatchingId_ReturnsResult()
	{
		var ok = WebClient.TryDecodeRpcResponse<string>(
			"{\"jsonrpc\":\"2.0\",\"id\":\"request-1\",\"result\":\"done\"}",
			"request-1",
			out var result,
			out _,
			out var errorMessage);

		Assert.That(ok, Is.True);
		Assert.That(result, Is.EqualTo("done"));
		Assert.That(errorMessage, Is.Null);
	}

	[Test]
	public void RpcResponseDecoder_WithoutId_ReportsMalformedResponse()
	{
		var ok = WebClient.TryDecodeRpcResponse<string>(
			"{\"jsonrpc\":\"2.0\",\"result\":\"done\"}",
			"request-1",
			out _,
			out var errorType,
			out var errorMessage);

		Assert.That(ok, Is.False);
		Assert.That(errorType, Is.EqualTo(EPHANTASMA_SDK_ERROR_TYPE.MALFORMED_RESPONSE));
		Assert.That(errorMessage, Does.Contain("Missing response id"));
	}

	[Test]
	public void RpcResponseDecoder_WithDifferentId_ReportsMalformedResponse()
	{
		var ok = WebClient.TryDecodeRpcResponse<string>(
			"{\"jsonrpc\":\"2.0\",\"id\":\"other-request\",\"result\":\"done\"}",
			"request-1",
			out _,
			out var errorType,
			out var errorMessage);

		Assert.That(ok, Is.False);
		Assert.That(errorType, Is.EqualTo(EPHANTASMA_SDK_ERROR_TYPE.MALFORMED_RESPONSE));
		Assert.That(errorMessage, Does.Contain("Response id mismatch"));
	}

	[Test]
	public void RpcResponseDecoder_WithDifferentIdAndRpcError_ReportsIdMismatch()
	{
		var ok = WebClient.TryDecodeRpcResponse<string>(
			"{\"jsonrpc\":\"2.0\",\"id\":\"other-request\",\"error\":{\"code\":-32603,\"message\":\"Execution failed\"}}",
			"request-1",
			out _,
			out var errorType,
			out var errorMessage);

		Assert.That(ok, Is.False);
		Assert.That(errorType, Is.EqualTo(EPHANTASMA_SDK_ERROR_TYPE.MALFORMED_RESPONSE));
		Assert.That(errorMessage, Does.Contain("Response id mismatch"));
		Assert.That(errorMessage, Does.Not.Contain("Execution failed"));
	}

	[Test]
	public void RpcResponseDecoder_WithWrongIdType_ReportsMalformedResponse()
	{
		var ok = WebClient.TryDecodeRpcResponse<string>(
			"{\"jsonrpc\":\"2.0\",\"id\":{\"bad\":\"id\"},\"result\":\"done\"}",
			"request-1",
			out _,
			out var errorType,
			out var errorMessage);

		Assert.That(ok, Is.False);
		Assert.That(errorType, Is.EqualTo(EPHANTASMA_SDK_ERROR_TYPE.MALFORMED_RESPONSE));
		Assert.That(errorMessage, Does.Contain("JSON-RPC id must be a string, integer, or null"));
	}

	[Test]
	public void RpcResponseDecoder_WithoutResult_ReportsMalformedResponse()
	{
		var ok = WebClient.TryDecodeRpcResponse<string>(
			"{\"jsonrpc\":\"2.0\",\"id\":\"request-1\"}",
			"request-1",
			out _,
			out var errorType,
			out var errorMessage);

		Assert.That(ok, Is.False);
		Assert.That(errorType, Is.EqualTo(EPHANTASMA_SDK_ERROR_TYPE.MALFORMED_RESPONSE));
		Assert.That(errorMessage, Does.Contain("Missing response result"));
	}

	[Test]
	public void GetAccountInfo_SendsOnlyTheAccount()
	{
		var api = new CapturingPhantasmaApi();
		var callbackInvoked = false;

		// The endpoint exists so wallets can refresh at a cost independent of account size; sending an
		// extra argument would change which node overload is dispatched.
		RunCoroutine(api.GetAccountInfo("P2Kaccount", _ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getAccountInfo", "P2Kaccount");
	}

	[Test]
	public void GetAccountInfo_WithAddressType_UsesExpandedSignature()
	{
		var api = new CapturingPhantasmaApi();
		var callbackInvoked = false;

		RunCoroutine(api.GetAccountInfo(
			"00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff",
			false,
			RpcAddressType.Carbon,
			_ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getAccountInfo", "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff", false, RpcAddressType.Carbon);
	}

	[Test]
	public void GetAccountInfos_SendsNativeAddressArray()
	{
		var api = new CapturingPhantasmaApi();
		var callbackInvoked = false;

		// The batch contract is a NATIVE JSON array parameter (one element), not the comma-joined
		// string the deprecated getAccounts wire used; a params-array expansion bug would spread the
		// addresses into separate positional parameters.
		RunCoroutine(api.GetAccountInfos(new[] { "P2Kaccount1", "P2Kaccount2" }, _ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getAccountInfos", (object)new[] { "P2Kaccount1", "P2Kaccount2" });
	}

	[Test]
	public void GetAccountInfos_WithAddressType_UsesExpandedSignature()
	{
		var api = new CapturingPhantasmaApi();
		var callbackInvoked = false;

		RunCoroutine(api.GetAccountInfos(
			new[] { "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff" },
			false,
			RpcAddressType.Carbon,
			_ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getAccountInfos", new[] { "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff" }, false, RpcAddressType.Carbon);
	}

	[Test]
	// Covers the deprecated account surface on purpose: the expanded signature must keep working
	// for existing integrations until they migrate to GetAccountInfo.
#pragma warning disable CS0618
	public void GetAccount_WithAddressTypeAndValidationFlags_UsesExpandedSignature()
	{
		var api = new CapturingPhantasmaApi();
		var callbackInvoked = false;

		RunCoroutine(api.GetAccount(
			"00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff",
			true,
			false,
			RpcAddressType.Carbon,
			_ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getAccount", "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff", true, false, RpcAddressType.Carbon);
	}
#pragma warning restore CS0618

	[Test]
	// Covers the deprecated account surface on purpose: the expanded signature must keep working
	// for existing integrations until they migrate to GetAccountInfo.
#pragma warning disable CS0618
	public void GetAccounts_WithAddressTypeAndValidationFlags_JoinsAddressesAndUsesExpandedSignature()
	{
		var api = new CapturingPhantasmaApi();
		var callbackInvoked = false;

		RunCoroutine(api.GetAccounts(
			new[]
			{
				"addr-1",
				"addr-2"
			},
			true,
			false,
			RpcAddressType.Carbon,
			_ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getAccounts", "addr-1,addr-2", true, false, RpcAddressType.Carbon);
	}
#pragma warning restore CS0618

	[Test]
	public void GetAccountFungibleTokens_WithAddressType_UsesCursorPayload()
	{
		var api = new CapturingPhantasmaApi();
		var callbackInvoked = false;

		RunCoroutine(api.GetAccountFungibleTokens(
			"00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff",
			"SOUL",
			0,
			25,
			"cursor-1",
			false,
			RpcAddressType.Carbon,
			_ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getAccountFungibleTokens", "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff", "SOUL", 0UL, 25U, "cursor-1", false, RpcAddressType.Carbon);
	}

	[Test]
	public void GetAccountNfts_WithAddressType_UsesCursorPayload()
	{
		var api = new CapturingPhantasmaApi();
		var callbackInvoked = false;

		RunCoroutine(api.GetAccountNFTs(
			"00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff",
			"CROWN",
			17,
			3,
			25,
			"cursor-2",
			true,
			false,
			RpcAddressType.Carbon,
			_ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getAccountNFTs", "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff", "CROWN", 17UL, 3U, 25U, "cursor-2", true, false, RpcAddressType.Carbon);
	}

	[Test]
	public void GetAccountOwnedTokens_WithAddressType_UsesCursorPayload()
	{
		var api = new CapturingPhantasmaApi();
		var callbackInvoked = false;

		RunCoroutine(api.GetAccountOwnedTokens(
			"00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff",
			"CROWN",
			17,
			25,
			"cursor-3",
			false,
			RpcAddressType.Carbon,
			_ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getAccountOwnedTokens", "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff", "CROWN", 17UL, 25U, "cursor-3", false, RpcAddressType.Carbon);
	}

	[Test]
	public void GetAccountOwnedTokenSeries_WithAddressType_UsesCursorPayload()
	{
		var api = new CapturingPhantasmaApi();
		var callbackInvoked = false;

		RunCoroutine(api.GetAccountOwnedTokenSeries(
			"00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff",
			"CROWN",
			17,
			25,
			"cursor-4",
			false,
			RpcAddressType.Carbon,
			_ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getAccountOwnedTokenSeries", "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff", "CROWN", 17UL, 25U, "cursor-4", false, RpcAddressType.Carbon);
	}

	[Test]
	public void GetContractByAddress_UsesChainAndAddress()
	{
		var api = new CapturingPhantasmaApi();
		var callbackInvoked = false;

		RunCoroutine(api.GetContractByAddress("main", "P2KcontractAddress", _ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getContractByAddress", "main", "P2KcontractAddress");
	}

	[Test]
	public void OrganizationMethods_UseFinalNameFirstPayloads()
	{
		var api = new CapturingPhantasmaApi();

		RunCoroutine(api.GetOrganization("masters", true, _ => { }));
		AssertCall(api, "getOrganization", "masters", true);

		RunCoroutine(api.GetOrganizations(2, "cursor", true, _ => { }));
		AssertCall(api, "getOrganizations", 2U, "cursor", true);

		RunCoroutine(api.GetOrganizationMembers("masters", 2, "", false, _ => { }));
		AssertCall(api, "getOrganizationMembers", "masters", 2U, "", false);

		RunCoroutine(api.GetOrganizationMember("masters", "Pmember", true, RpcAddressType.Phantasma, _ => { }));
		AssertCall(api, "getOrganizationMember", "masters", "Pmember", true, RpcAddressType.Phantasma);
	}

	[Test]
	public void GetToken_WithExtendedAndCarbonId_UsesExpandedSignature()
	{
		var api = new CapturingPhantasmaApi();
		var callbackInvoked = false;

		RunCoroutine(api.GetToken("TESTN", true, 111, _ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getToken", "TESTN", true, 111UL);
	}

	[Test]
	public void GetTokens_WithExtendedOwnerAndAddressType_UsesCarbonPayload()
	{
		var api = new CapturingPhantasmaApi();
		var callbackInvoked = false;

		RunCoroutine(api.GetTokens(
			true,
			"00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff",
			RpcAddressType.Carbon,
			_ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getTokens", true, "00112233445566778899aabbccddeeff00112233445566778899aabbccddeeff", RpcAddressType.Carbon);
	}

	[Test]
	public void GetTokenSeries_UsesCarbonCursorPayload()
	{
		var api = new CapturingPhantasmaApi();
		var callbackInvoked = false;

		RunCoroutine(api.GetTokenSeries("CROWN", 17, 25, "cursor-5", _ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getTokenSeries", "CROWN", 17UL, 25U, "cursor-5");
	}

	[Test]
	public void GetTokenSeriesById_UsesBothSeriesIdentifiers()
	{
		var api = new CapturingPhantasmaApi();
		var callbackInvoked = false;

		RunCoroutine(api.GetTokenSeriesById("CROWN", 17, "series-alpha", 3, _ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getTokenSeriesById", "CROWN", 17UL, "series-alpha", 3U);
	}

	[Test]
	public void GetTokenNfts_UsesExtendedCursorPayload()
	{
		var api = new CapturingPhantasmaApi();
		var callbackInvoked = false;

		RunCoroutine(api.GetTokenNFTs(17, 3, 25, "cursor-6", true, "series-beta", _ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getTokenNFTs", 17UL, 3U, 25U, "cursor-6", true, "series-beta");
	}

	[Test]
	public void GetNfts_JoinsIdsBeforeCallingCarbonRpc()
	{
		var api = new CapturingPhantasmaApi();
		var callbackInvoked = false;

		RunCoroutine(api.GetNFTs("CROWN", new[] { "1", "2", "3" }, true, _ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getNFTs", "CROWN", "1,2,3", true);
	}

	[Test]
	public void GetTokenBalance_WithAddressType_UsesExpandedSignature()
	{
		var api = new CapturingPhantasmaApi();
		var callbackInvoked = false;

		RunCoroutine(api.GetTokenBalance("P2K...", "SOUL", "main", false, RpcAddressType.Carbon, _ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getTokenBalance", "P2K...", "SOUL", "main", false, RpcAddressType.Carbon);
	}

	[Test]
	public void GetBlockTransactionCountByHash_WithChainParameter_SendsChainAndBlockHash()
	{
		var api = new CapturingPhantasmaApi
		{
			NextResult = "7"
		};
		var callbackResult = -1;

		RunCoroutine(api.GetBlockTransactionCountByHash("main", "ABCDEF0123456789", result => callbackResult = result));

		Assert.That(callbackResult, Is.EqualTo(7));
		AssertCall(api, "getBlockTransactionCountByHash", "main", "ABCDEF0123456789");
	}

	[Test]
	public void GetBlockTransactionCountByHash_WithoutChainParameter_SendsRootChainAndBlockHash()
	{
		var api = new CapturingPhantasmaApi
		{
			NextResult = "3"
		};
		var callbackResult = -1;

		RunCoroutine(api.GetBlockTransactionCountByHash("ABCDEF0123456789", result => callbackResult = result));

		Assert.That(callbackResult, Is.EqualTo(3));
		AssertCall(api, "getBlockTransactionCountByHash", "main", "ABCDEF0123456789");
	}

	[Test]
	public void GetChain_WithoutParameters_SendsRootChainAndExtendedFlag()
	{
		var api = new CapturingPhantasmaApi
		{
			NextResult = new ChainResult()
		};
		var callbackInvoked = false;

		RunCoroutine(api.GetChain(_ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getChain", "main", true);
	}

	[Test]
	public void GetChain_WithParameters_SendsNameAndExtendedFlag()
	{
		var api = new CapturingPhantasmaApi
		{
			NextResult = new ChainResult()
		};
		var callbackInvoked = false;

		RunCoroutine(api.GetChain("main", false, _ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getChain", "main", false);
	}

	[Test]
	public void GetTransactionByBlockHashAndIndex_WithChainParameter_SendsChainBlockHashAndIndex()
	{
		var api = new CapturingPhantasmaApi
		{
			NextResult = new TransactionResult()
		};
		var callbackInvoked = false;

		RunCoroutine(api.GetTransactionByBlockHashAndIndex("main", "ABCDEF0123456789", 2, _ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getTransactionByBlockHashAndIndex", "main", "ABCDEF0123456789", 2);
	}

	[Test]
	public void GetTransactionByBlockHashAndIndex_WithoutChainParameter_SendsRootChainBlockHashAndIndex()
	{
		var api = new CapturingPhantasmaApi
		{
			NextResult = new TransactionResult()
		};
		var callbackInvoked = false;

		RunCoroutine(api.GetTransactionByBlockHashAndIndex("ABCDEF0123456789", 2, _ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getTransactionByBlockHashAndIndex", "main", "ABCDEF0123456789", 2);
	}

	[Test]
	public void SignAndSendTransaction_WhenRpcHashDiffers_ReportsApiError()
	{
		var api = new CapturingPhantasmaApi
		{
			NextResult = "DIFFERENT_HASH"
		};
		var keys = CreateDeterministicKeys();
		var successInvoked = false;
		EPHANTASMA_SDK_ERROR_TYPE? errorType = null;
		string errorMessage = null;

		RunCoroutine(api.SignAndSendTransaction(
			keys,
			"mainnet",
			Array.Empty<byte>(),
			"main",
			Array.Empty<byte>(),
			(_, _) => successInvoked = true,
			(type, message) =>
			{
				errorType = type;
				errorMessage = message;
			}));

		Assert.That(successInvoked, Is.False);
		Assert.That(errorType, Is.EqualTo(EPHANTASMA_SDK_ERROR_TYPE.API_ERROR));
		Assert.That(errorMessage, Does.Contain("DIFFERENT_HASH"));
	}

	[Test]
	public void SignAndSendTransaction_WithNullStringPayload_ReachesBroadcast()
	{
		var api = new CapturingPhantasmaApi
		{
			NextResult = "DIFFERENT_HASH"
		};
		var keys = CreateDeterministicKeys();

		RunCoroutine(api.SignAndSendTransaction(
			keys,
			"mainnet",
			Array.Empty<byte>(),
			"main",
			(string)null,
			(_, _) => { },
			(_, _) => { }));

		Assert.That(api.LastMethod, Is.EqualTo("sendRawTransaction"));
		Assert.That(api.LastParameters, Has.Length.EqualTo(1));
		Assert.That(api.LastParameters[0], Is.TypeOf<string>());
	}

	[Test]
	public void SignAndSendTransaction_WithNullBinaryPayload_ReachesBroadcast()
	{
		var api = new CapturingPhantasmaApi
		{
			NextResult = "DIFFERENT_HASH"
		};
		var keys = CreateDeterministicKeys();

		RunCoroutine(api.SignAndSendTransaction(
			keys,
			"mainnet",
			Array.Empty<byte>(),
			"main",
			(byte[])null,
			(_, _) => { },
			(_, _) => { }));

		Assert.That(api.LastMethod, Is.EqualTo("sendRawTransaction"));
		Assert.That(api.LastParameters, Has.Length.EqualTo(1));
		Assert.That(api.LastParameters[0], Is.TypeOf<string>());
	}

	[Test]
	public void GetGasConfig_SendsNoParameters()
	{
		var api = new CapturingPhantasmaApi();
		var callbackInvoked = false;

		// The node exposes no overload of getGasConfig; sending any argument would fail to dispatch.
		RunCoroutine(api.GetGasConfig(_ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "getGasConfig");
	}

	[Test]
	public void EstimateTransaction_SendsOnlyTheEnvelope()
	{
		var api = new CapturingPhantasmaApi();
		var callbackInvoked = false;

		// The envelope is the single argument and must reach the node verbatim: the bill it returns
		// is a function of the exact byte length, so any re-encoding here would change the estimate.
		RunCoroutine(api.EstimateTransaction("0a0b0c", _ => callbackInvoked = true));

		Assert.That(callbackInvoked, Is.True);
		AssertCall(api, "estimateTransaction", "0a0b0c");
	}

	// The gas configuration of mainnet, as getGasConfig reports it. The fee figures the send tests
	// assert below are the ones this configuration prices.
	private const string MainnetGasConfigJson = @"{
	  ""gasModelVersion"": 2,
	  ""blockRateTarget"": 2000,
	  ""expiryWindow"": 3600000,
	  ""unitsPerBlockDataByte"": 25,
	  ""gasConfig"": {
	    ""version"": 1, ""maxNameLength"": 255, ""maxTokenSymbolLength"": 255, ""feeShift"": 0, ""maxStructureSize"": 1048576,
	    ""feeMultiplier"": ""10000"", ""gasTokenId"": ""1"", ""dataTokenId"": ""2"", ""minimumGasOffer"": ""10"", ""dataEscrowPerRow"": ""200000"",
	    ""gasFeeTransfer"": ""10"", ""gasFeeQuery"": ""10"", ""gasFeeCreateTokenBase"": ""10000000000"", ""gasFeeCreateTokenSymbol"": ""10000000000"",
	    ""gasFeeCreateTokenSeries"": ""2500000000"", ""gasFeePerByte"": ""250000"", ""gasFeeRegisterName"": ""10000000000000"",
	    ""gasBurnRatioMul"": ""1"", ""gasBurnRatioShift"": 0, ""minimumGasBill"": ""10000000"",
	    ""gasProducerRatioMul"": ""0"", ""gasProducerRatioShift"": 0, ""gasDappRatioMul"": ""0"", ""gasDappRatioShift"": 0,
	    ""policyFeeCreateTokenBase"": ""100000000000000"", ""policyFeeCreateTokenSymbol"": ""100000000000000"",
	    ""policyFeeCreateTokenSeries"": ""25000000000000"", ""policyFeeRegisterName"": ""100000000000000000"", ""legacyDataEscrowPerRow"": ""2""
	  }
	}";

	private static GasConfigResult MainnetGasConfig()
	{
		return JsonConvert.DeserializeObject<GasConfigResult>(MainnetGasConfigJson);
	}

	private static void FailOnError(EPHANTASMA_SDK_ERROR_TYPE errorType, string message)
	{
		Assert.Fail(errorType + ": " + message);
	}

	private static TxMsg Transfer(PhantasmaKeys owner, PhantasmaKeys gasPayer = null, ulong maxGas = 0)
	{
		return NativeTxHelper.TransferFungible(new TransferFungibleParams
		{
			From = new Bytes32(owner.PublicKey),
			To = new Bytes32(CreateDeterministicKeys(9).PublicKey),
			TokenId = 1,
			Amount = 5,
			GasPayer = gasPayer == null ? (Bytes32?)null : new Bytes32(gasPayer.PublicKey),
			MaxGas = maxGas == 0 ? (ulong?)null : maxGas
		});
	}

	private static TxMsg CreateToken(string symbol, PhantasmaKeys owner)
	{
		var metadata = TokenMetadataBuilder.BuildAndSerialize(new Dictionary<string, string>
		{
			["name"] = "Unity probe",
			["icon"] = "data:image/png;base64,iVBORw0KGgo=",
			["url"] = "https://example.invalid/p",
			["description"] = "x"
		});

		var info = TokenInfoBuilder.Build(symbol, new IntX(0), false, 2, new Bytes32(owner.PublicKey), metadata, null);
		return CreateTokenTxHelper.BuildTx(info, new Bytes32(owner.PublicKey));
	}

	// The envelope the wrapper broadcast, read back from the hexadecimal it passed to
	// sendCarbonTransaction. What was sent is asserted, never what was intended.
	private static SignedTxMsg DecodeSent(CapturingPhantasmaApi api)
	{
		var index = api.Methods.IndexOf("sendCarbonTransaction");
		Assert.That(index, Is.GreaterThanOrEqualTo(0), "no transaction was broadcast");

		return CarbonBlob.New<SignedTxMsg>(Base16.Decode((string)api.Parameters[index][0]));
	}

	[Test]
	public void SignAndSendCarbonTransaction_WithoutAGasOffer_PlansTheFeeThenSends()
	{
		var owner = CreateDeterministicKeys(1);
		var api = new CapturingPhantasmaApi();
		api.Answer("getGasConfig", MainnetGasConfig());
		api.Answer("sendCarbonTransaction", "HASH");

		RunCoroutine(api.SignAndSendCarbonTransaction(owner, Transfer(owner), (hash, encoded) => { }, FailOnError));

		// 42,600,000 is what the vendored planner prices this transfer at against the configuration
		// above. A wrapper that sent the message unplanned, or that planned it against an empty
		// configuration, cannot produce that number.
		var sent = DecodeSent(api);
		Assert.That(sent.msg.maxGas, Is.EqualTo(42_600_000UL));
		Assert.That(sent.witnesses.Length, Is.EqualTo(1));
		Assert.That(sent.witnesses[0].address.ToHex(), Is.EqualTo(new Bytes32(owner.PublicKey).ToHex()));
		// A transfer creates no token, so no symbol is looked up.
		Assert.That(api.Methods, Is.EqualTo(new[] { "getGasConfig", "sendCarbonTransaction" }));
	}

	[Test]
	public void SignAndSendCarbonTransaction_WithAGasOfferAlreadySet_SendsItAsItIs()
	{
		var owner = CreateDeterministicKeys(1);
		var api = new CapturingPhantasmaApi();
		api.Answer("sendCarbonTransaction", "HASH");

		RunCoroutine(api.SignAndSendCarbonTransaction(owner, Transfer(owner, maxGas: 999_000_000), (hash, encoded) => { }, FailOnError));

		// The caller priced the message, so the gas configuration is not read and the offer is untouched.
		Assert.That(DecodeSent(api).msg.maxGas, Is.EqualTo(999_000_000UL));
		Assert.That(api.Methods, Is.EqualTo(new[] { "sendCarbonTransaction" }));
	}

	[Test]
	public void SignAndSendCarbonTransaction_WithAGasPayer_SignsOneWitnessPerKey()
	{
		var owner = CreateDeterministicKeys(1);
		var payer = CreateDeterministicKeys(2);
		var api = new CapturingPhantasmaApi();
		api.Answer("getGasConfig", MainnetGasConfig());
		api.Answer("sendCarbonTransaction", "HASH");

		RunCoroutine(api.SignAndSendCarbonTransaction(new IKeyPair[] { owner, payer }, Transfer(owner, gasPayer: payer), null, (hash, encoded) => { }, FailOnError));

		// The node fixes the witness order of a gas-payer transfer: the payer signs first. Both
		// signatures are over the same serialized message, and both are checked here.
		var sent = DecodeSent(api);
		Assert.That(sent.msg.type, Is.EqualTo(TxTypes.TransferFungible_GasPayer));
		Assert.That(sent.witnesses.Length, Is.EqualTo(2));
		Assert.That(sent.witnesses[0].address.ToHex(), Is.EqualTo(new Bytes32(payer.PublicKey).ToHex()));
		Assert.That(sent.witnesses[1].address.ToHex(), Is.EqualTo(new Bytes32(owner.PublicKey).ToHex()));

		var message = CarbonBlob.Serialize(sent.msg);
		foreach (var witness in sent.witnesses)
		{
			Assert.That(Ed25519.Verify(witness.signature.bytes, message, witness.address.bytes), Is.True);
		}
	}

	[Test]
	public void SignAndSendCarbonTransaction_WithATakenSymbol_RefusesBeforeSending()
	{
		var owner = CreateDeterministicKeys(1);
		var api = new CapturingPhantasmaApi();
		api.Answer("getGasConfig", MainnetGasConfig());
		api.Answer("getToken", new TokenResult { Symbol = "KCAL" });
		string reported = null;

		RunCoroutine(api.SignAndSendCarbonTransaction(owner, CreateToken("KCAL", owner), (hash, encoded) => Assert.Fail("the transaction was sent"), (errorType, message) => reported = message));

		// The policy fee is spent before the contract looks at the symbol, so a taken symbol must cost
		// nothing at all.
		Assert.That(reported, Does.Contain("KCAL"));
		Assert.That(reported, Does.Contain("already taken"));
		Assert.That(api.Methods, Does.Not.Contain("sendCarbonTransaction"));
	}

	[Test]
	public void SignAndSendCarbonTransaction_WithAFreeSymbol_SendsAfterTheControlLookup()
	{
		var owner = CreateDeterministicKeys(1);
		var api = new CapturingPhantasmaApi();
		api.Answer("getGasConfig", MainnetGasConfig());
		api.Answer("getToken", new RpcFailure("Token symbol not found"));
		api.Answer("getToken", new TokenResult { Symbol = "KCAL" });
		api.Answer("sendCarbonTransaction", "HASH");

		RunCoroutine(api.SignAndSendCarbonTransaction(owner, CreateToken("GPX", owner), (hash, encoded) => { }, FailOnError));

		// The symbol lookup failed, and an error alone never means absence. The gas token was fetched by
		// its id as the control, it answered, and only then was the symbol taken to be free.
		Assert.That(api.Methods, Is.EqualTo(new[] { "getGasConfig", "getToken", "getToken", "sendCarbonTransaction" }));
		Assert.That(api.Parameters[1][0], Is.EqualTo("GPX"));
		Assert.That(api.Parameters[2][2], Is.EqualTo(1UL));
		Assert.That(DecodeSent(api).msg.maxGas, Is.GreaterThan(100_000_000_000_000UL));
	}

	[Test]
	public void SignAndSendCarbonTransaction_WhenTheControlLookupIsUnanswered_RefusesTheTokenCreation()
	{
		var owner = CreateDeterministicKeys(1);
		var api = new CapturingPhantasmaApi();
		api.Answer("getGasConfig", MainnetGasConfig());
		api.Answer("getToken", new RpcFailure("Token symbol not found"));
		api.Answer("getToken", new RpcFailure("Execution failed"));
		string reported = null;

		RunCoroutine(api.SignAndSendCarbonTransaction(owner, CreateToken("GPX", owner), (hash, encoded) => Assert.Fail("the transaction was sent"), (errorType, message) => reported = message));

		// The node answered neither question, so nothing is established. Paying the largest price in the
		// protocol on an unestablished state is what the round trip exists to avoid.
		Assert.That(reported, Does.Contain("Could not establish"));
		Assert.That(reported, Does.Contain("Execution failed"));
		Assert.That(api.Methods, Does.Not.Contain("sendCarbonTransaction"));
	}

	[Test]
	public void SignAndSendCarbonTransaction_WithAnEmptyCallMessage_ReportsTheFault()
	{
		var owner = CreateDeterministicKeys(1);
		var api = new CapturingPhantasmaApi();
		api.Answer("getGasConfig", MainnetGasConfig());
		string reported = null;

		// TxTypes.Call is zero, so a TxMsg the caller built by hand and did not finish arrives here as a
		// call with no body. A coroutine cannot hand an exception back to whoever started it, so this has
		// to reach the error callback. An exception escaping instead would be logged by Unity and lost.
		var unfinished = new TxMsg();

		RunCoroutine(api.SignAndSendCarbonTransaction(owner, unfinished, (hash, encoded) => Assert.Fail("the transaction was sent"), (errorType, message) => reported = message));

		Assert.That(reported, Is.Not.Null.And.Not.Empty);
		Assert.That(api.Methods, Does.Not.Contain("sendCarbonTransaction"));
	}

	private static TxMsg Burn(PhantasmaKeys owner, ulong tokenId = 9, ulong instanceId = 5)
	{
		return NativeTxHelper.BurnNonFungible(new BurnNonFungibleParams
		{
			From = new Bytes32(owner.PublicKey),
			TokenId = tokenId,
			InstanceId = instanceId
		});
	}

	// One empty page, which is what the account queries answer for an address that holds nothing.
	private static CursorPaginatedResult<T[]> NoPage<T>()
	{
		return new CursorPaginatedResult<T[]>(Array.Empty<T>(), null);
	}

	[Test]
	public void SignAndSendCarbonTransaction_WithABurnOfAnEmptyNft_AsksTheChainThenPlans()
	{
		var owner = CreateDeterministicKeys(1);
		var api = new CapturingPhantasmaApi();
		api.Answer("getGasConfig", MainnetGasConfig());
		api.Answer("getAccountFungibleTokens", NoPage<BalanceResult>());
		api.Answer("getAccountOwnedTokens", NoPage<TokenResult>());
		api.Answer("sendCarbonTransaction", "HASH");

		RunCoroutine(api.SignAndSendCarbonTransaction(owner, Burn(owner), (hash, encoded) => { }, FailOnError));

		// The planner refuses a burn until it is told what the NFT holds. Both queries are asked, the
		// empty answer says it holds nothing, and only then can the message be priced.
		Assert.That(api.Methods, Is.EqualTo(new[] { "getGasConfig", "getAccountFungibleTokens", "getAccountOwnedTokens", "sendCarbonTransaction" }));
		Assert.That(DecodeSent(api).msg.maxGas, Is.GreaterThan(0UL));
	}

	[Test]
	public void SignAndSendCarbonTransaction_WithAnInfusedNft_PricesWhatItHolds()
	{
		var owner = CreateDeterministicKeys(1);

		var empty = new CapturingPhantasmaApi();
		empty.Answer("getGasConfig", MainnetGasConfig());
		empty.Answer("getAccountFungibleTokens", NoPage<BalanceResult>());
		empty.Answer("getAccountOwnedTokens", NoPage<TokenResult>());
		empty.Answer("sendCarbonTransaction", "HASH");
		RunCoroutine(empty.SignAndSendCarbonTransaction(owner, Burn(owner), (hash, encoded) => { }, FailOnError));

		var infused = new CapturingPhantasmaApi();
		infused.Answer("getGasConfig", MainnetGasConfig());
		// The NFT holds a fungible balance of a token that is neither the gas nor the data token, so its
		// returned row is paid for.
		infused.Answer("getAccountFungibleTokens", new CursorPaginatedResult<BalanceResult[]>(new[] { new BalanceResult { Symbol = "GPX" } }, null));
		infused.Answer("getToken", new TokenResult { Symbol = "GPX", CarbonId = "7" });
		infused.Answer("getAccountOwnedTokens", NoPage<TokenResult>());
		infused.Answer("sendCarbonTransaction", "HASH");
		RunCoroutine(infused.SignAndSendCarbonTransaction(owner, Burn(owner), (hash, encoded) => { }, FailOnError));

		// The symbol was resolved to a token id, because the rows of the gas and data tokens are free and
		// only the id tells which token a row belongs to.
		Assert.That(infused.Methods, Is.EqualTo(new[] { "getGasConfig", "getAccountFungibleTokens", "getToken", "getAccountOwnedTokens", "sendCarbonTransaction" }));
		Assert.That(infused.Parameters[2][0], Is.EqualTo("GPX"));
		// Returning that asset to the burner costs a transfer, so the burn of an infused NFT is priced
		// above the burn of an empty one.
		Assert.That(DecodeSent(infused).msg.maxGas, Is.GreaterThan(DecodeSent(empty).msg.maxGas));
	}

	[Test]
	public void SignAndSendCarbonTransaction_WithInfusionsFromTheCaller_DoesNotAskTheChain()
	{
		var owner = CreateDeterministicKeys(1);
		var api = new CapturingPhantasmaApi();
		api.Answer("getGasConfig", MainnetGasConfig());
		api.Answer("sendCarbonTransaction", "HASH");

		// A caller who already knows what the NFT holds states it. An empty list is a statement too: it
		// says the NFT holds nothing.
		var options = new PlanAndSignOptions { Infusions = Array.Empty<InfusedAsset>() };

		RunCoroutine(api.SignAndSendCarbonTransaction(new IKeyPair[] { owner }, Burn(owner), options, (hash, encoded) => { }, FailOnError));

		Assert.That(api.Methods, Is.EqualTo(new[] { "getGasConfig", "sendCarbonTransaction" }));
	}

	[Test]
	public void SignAndSendCarbonTransaction_WhenTheInfusionQueryFails_RefusesBeforeSending()
	{
		var owner = CreateDeterministicKeys(1);
		var api = new CapturingPhantasmaApi();
		api.Answer("getGasConfig", MainnetGasConfig());
		api.Answer("getAccountFungibleTokens", new RpcFailure("Execution failed"));
		string reported = null;

		RunCoroutine(api.SignAndSendCarbonTransaction(owner, Burn(owner), (hash, encoded) => Assert.Fail("the transaction was sent"), (errorType, message) => reported = message));

		// A query that did not answer establishes nothing. Planning the burn as if the NFT held nothing
		// would offer too little gas, and the chain aborts a transaction that spends more than it offered.
		Assert.That(reported, Does.Contain("Could not read what the burned NFT holds"));
		Assert.That(reported, Does.Contain("Execution failed"));
		Assert.That(api.Methods, Does.Not.Contain("sendCarbonTransaction"));
	}
}
