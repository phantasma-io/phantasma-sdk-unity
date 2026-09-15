# Phantasma Unity SDK
This is the UPM Lib Unity SDK for Phantasma to interact with the Phantasma Blockchain.

# Import it to your project.
Inside Unity, go to `Window > Package Manager`. 
Then on the Top left corner press the `+` button, add package from git URL
Use the package URL for the package you want to import:

- Core: `https://github.com/phantasma-io/phantasma-sdk-unity.git?path=/PhantasmaPhoenix.Unity.Core`
- Link Client: `https://github.com/phantasma-io/phantasma-sdk-unity.git?path=/PhantasmaPhoenix.Unity.LinkClient`

# How to connect to the Wallet via the SDK
Setup your scene, Add the PhantasmaLinkClient prefab to your scene.
* If you're developing in a local node, change the "Nexus" to `localnet`
* If you're deploying it to the testnet, change the "Nexus" to `testnet`
* If you're deploying it to the mainnet, change the "Nexus" to `mainnet`
* Change the DappID to your Dapp "contract name", this is what will appear when a user log's in to your Dapp.
* Recommended version is 2
* Wallet Endpoint default:`localhost:7090` (Don't change it)
* Regarding the Platform and Signature, For `Phantasma` -> `ED25519`, for `Ethereum` -> `ECDSA`

# How to connect to the Wallet via the SDK (Android)
The same thing as the normal method, but you need to added the PhantasmaLinkClientPlugin Prefab to your Scene.

And that's it, just build for Android, you're done!

# Sending a transaction

`PhantasmaAPI.SignAndSendCarbonTransaction` plans the fee, signs and broadcasts in one call. Build the
message with the helpers of `PhantasmaPhoenix.Protocol.Carbon`, then hand it over.

```csharp
var api = new PhantasmaAPI("https://testnet.phantasma.info/rpc");
var keys = PhantasmaKeys.FromWIF(wif);

var message = NativeTxHelper.TransferFungible(new TransferFungibleParams
{
	From = new Bytes32(keys.PublicKey),
	To = new Bytes32(recipientPublicKey),
	TokenId = 1,
	Amount = 1000
});

StartCoroutine(api.SignAndSendCarbonTransaction(keys, message,
	(txHash, encodedTx) => Debug.Log($"Sent, hash {txHash}"),
	(errorType, errorMessage) => Debug.LogError($"[{errorType}] {errorMessage}")));
```

## The fee

A message whose `maxGas` is zero is priced against the chain's current gas configuration before it is
signed. The chain refuses an offer of zero, and it aborts a transaction that spends more than it
offered, so the offer has to be right.

A message whose `maxGas` you set yourself is signed as it is, and the gas configuration is not read.

## Several signatures

Some transactions need more than one witness. A transfer that names a gas payer needs the payer and
the owner. Pass one key per witness, in any order, and the overload that takes options:

```csharp
StartCoroutine(api.SignAndSendCarbonTransaction(
	new IKeyPair[] { payerKeys, ownerKeys }, message, null,
	(txHash, encodedTx) => Debug.Log($"Sent, hash {txHash}"),
	(errorType, errorMessage) => Debug.LogError($"[{errorType}] {errorMessage}")));
```

## Creating a token

A token creation spends the policy fee before the contract looks at the symbol, and that fee is the
largest single price in the protocol. Sending a symbol that is already taken pays it for nothing.

So a token creation is checked against the chain first. The symbol is looked up, and a symbol that
resolves to a token is refused before anything is signed. A symbol that does not resolve comes back as
an ordinary error, and an error alone never means the symbol is free, so a second lookup asks for the
gas token by its id as a control. A node that answers the control is serving token lookups, and its
refusal about your symbol is then a real absence. A node that answers neither question has established
nothing, and the creation is refused for that reason.

Pass `preflight: false` to skip the check. Nothing else is affected by it.

## Burning an NFT

A burn returns whatever the NFT holds at its own address, and the chain charges for every returned
asset. The wrapper reads that from the chain before it prices the burn.

If you already know what the NFT holds, state it and the chain is not asked:

```csharp
var options = new PlanAndSignOptions { Infusions = infusedAssets };
```

An empty list is a statement too. It says the NFT holds nothing.

## Errors

Everything that goes wrong reaches `errorHandlingCallback`: a refused pre-flight, a fee that cannot be
planned, a key set that does not match the witnesses the message names, and any network or node
failure. Nothing is broadcast when one of those happens.

# Any further questions
- Contact Phantasma Force Team
