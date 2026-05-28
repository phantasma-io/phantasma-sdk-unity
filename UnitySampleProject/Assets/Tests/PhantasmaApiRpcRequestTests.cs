using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using PhantasmaPhoenix.Cryptography;
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

            if (callback != null)
            {
                var result = NextResult is T typed ? typed : default;
                callback(result);
            }

            yield break;
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
        var privateKey = new byte[PhantasmaKeys.PrivateKeyLength];
        for (var i = 0; i < privateKey.Length; i++)
        {
            privateKey[i] = (byte)(i + 1);
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

    [Test]
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
}
