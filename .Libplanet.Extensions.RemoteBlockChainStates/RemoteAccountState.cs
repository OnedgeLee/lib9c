using System.Security.Cryptography;
using Bencodex;
using Bencodex.Types;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Libplanet.Action.State;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;

namespace Libplanet.Extensions.RemoteBlockChainStates;

public class RemoteAccountState : IAccountState
{
    private readonly Uri _explorerEndpoint;
    private readonly GraphQLHttpClient _graphQlHttpClient;

    public RemoteAccountState(Uri explorerEndpoint, Address address, BlockHash? offset)
    {
        _explorerEndpoint = explorerEndpoint;
        _graphQlHttpClient =
            new GraphQLHttpClient(_explorerEndpoint, new SystemTextJsonSerializer());
        var response = _graphQlHttpClient.SendQueryAsync<GetAccountStateResponseType>(
            new GraphQLRequest(
                @"query GetAccount($address: Address!, $offsetBlockHash: ID!)
                {
                    stateQuery
                    {
                        accountState(address: $address, offsetBlockHash: $offsetBlockHash)
                    }
                }",
                operationName: "GetWorld",
                variables: new
                {
                    address = address is { } addr
                        ? addr.ToString()
                        : throw new NotSupportedException(),
                    offset = offset is { } hash
                        ? ByteUtil.Hex(hash.ByteArray)
                        : throw new NotSupportedException(),
                })).Result;
        Address = new Address(response.Data.StateQuery.AccountState.Address);
        StateRootHash = HashDigest<SHA256>.FromString(response.Data.StateQuery.AccountState.StateRootHash);
        BlockHash = Types.Blocks.BlockHash.FromString(response.Data.StateQuery.AccountState.BlockHash);
    }

    public Address Address { get; }

    public HashDigest<SHA256>? StateRootHash { get; }

    public BlockHash? BlockHash { get; }


    public IValue? GetState(Address address) =>
        GetStates(new[] { address }).First();

    public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses)
    {
        var response = _graphQlHttpClient.SendQueryAsync<GetStatesResponseType>(
            new GraphQLRequest(
                @"query GetStates($addresses: [Address!]!, $accountAddress: Address!, $offsetBlockHash: ID!)
                {
                    stateQuery
                    {
                        states(addresses: $addresses, accountAddress: $accountAddress, offsetBlockHash: $offsetBlockHash)
                    }
                }",
                operationName: "GetStates",
                variables: new
                {
                    addresses = addresses.Select(x => x.ToString()).ToArray(),
                    accountAddress = Address.ToString(),
                    offsetBlockHash = BlockHash is { } hash
                        ? ByteUtil.Hex(hash.ByteArray)
                        : throw new NotSupportedException(),
                })).Result;
        var codec = new Codec();
        return response.Data.StateQuery.States
            .Select(nullableState => nullableState is { } state ? codec.Decode(state) : null).ToList();
    }

    public FungibleAssetValue GetBalance(Address address, Currency currency)
    {
        object? currencyInput = currency.TotalSupplyTrackable ? new
        {
            ticker = currency.Ticker,
            decimalPlaces = currency.DecimalPlaces,
            minters = currency.Minters?.Select(addr => addr.ToString()).ToArray(),
            totalSupplyTrackable = currency.TotalSupplyTrackable,
            maximumSupplyMajorUnit = currency.MaximumSupply.Value.MajorUnit,
            maximumSupplyMinorUnit = currency.MaximumSupply.Value.MinorUnit,
        } : new
        {
            ticker = currency.Ticker,
            decimalPlaces = currency.DecimalPlaces,
            minters = currency.Minters?.Select(addr => addr.ToString()).ToArray(),
            totalSupplyTrackable = currency.TotalSupplyTrackable,
        };
        var response = _graphQlHttpClient.SendQueryAsync<GetBalanceResponseType>(
            new GraphQLRequest(
        @"query GetBalance($owner: Address!, $currency: CurrencyInput!, $offsetBlockHash: ID!)
            {
                stateQuery
                {
                    balance(owner: $owner, currency: $currency, offsetBlockHash: $offsetBlockHash)
                    {
                        string
                    }
                }
            }",
            operationName: "GetBalance",
            variables: new
            {
                owner = address.ToString(),
                currency = currencyInput,
                offsetBlockHash = BlockHash is { } hash
                    ? ByteUtil.Hex(hash.ByteArray)
                    : throw new NotSupportedException(),
            })).Result;

        return FungibleAssetValue.Parse(currency, response.Data.StateQuery.Balance.String.Split()[0]);
    }

    public FungibleAssetValue GetTotalSupply(Currency currency)
    {
        object? currencyInput = currency.TotalSupplyTrackable ? new
        {
            ticker = currency.Ticker,
            decimalPlaces = currency.DecimalPlaces,
            minters = currency.Minters.Select(addr => addr.ToString()).ToArray(),
            totalSupplyTrackable = currency.TotalSupplyTrackable,
            maximumSupplyMajorUnit = currency.MaximumSupply.Value.MajorUnit,
            maximumSupplyMinorUnit = currency.MaximumSupply.Value.MinorUnit,
        } : new
        {
            ticker = currency.Ticker,
            decimalPlaces = currency.DecimalPlaces,
            minters = currency.Minters.Select(addr => addr.ToString()).ToArray(),
            totalSupplyTrackable = currency.TotalSupplyTrackable,
        };
        var response = _graphQlHttpClient.SendQueryAsync<GetTotalSupplyResponseType>(
            new GraphQLRequest(
                @"query GetTotalSupply(currency: CurrencyInput!, $offsetBlockHash: ID!)
            {
                stateQuery
                {
                    totalSupply(currency: $currency, offsetBlockHash: $offsetBlockHash)
                    {
                        string
                    }
                }
            }",
                operationName: "GetTotalSupply",
                variables: new
                {
                    currency = currencyInput,
                    offsetBlockHash = BlockHash is { } hash
                        ? ByteUtil.Hex(hash.ByteArray)
                        : throw new NotSupportedException(),
                })).Result;

        return FungibleAssetValue.Parse(currency, response.Data.StateQuery.TotalSupply.String.Split()[0]);
    }

    public ValidatorSet GetValidatorSet()
    {
        var response = _graphQlHttpClient.SendQueryAsync<GetValidatorsResponseType>(
            new GraphQLRequest(
                @"query GetValidators($offsetBlockHash: ID!)
            {
                stateQuery
                {
                    validators(offsetBlockHash: $offsetBlockHash)
                    {
                        publicKey
                        power
                    }
                }
            }",
                operationName: "GetValidators",
                variables: new
                {
                    offsetBlockHash = BlockHash is { } hash
                        ? ByteUtil.Hex(hash.ByteArray)
                        : throw new NotSupportedException(),
                })).Result;

        return new ValidatorSet(response.Data.StateQuery.Validators
            .Select(x =>
                new Validator(new PublicKey(ByteUtil.ParseHex(x.PublicKey)), x.Power))
            .ToList());
    }

    private class GetAccountStateResponseType
    {
        public StateQueryWithAccountStateType StateQuery { get; set; }
    }

    private class StateQueryWithAccountStateType
    {
        public AccountStateType AccountState { get; set; }
    }

    public class AccountStateType
    {
        public string Address { get; set; }

        public string StateRootHash { get; set; }

        public string BlockHash { get; set; }
    }

    private class GetStatesResponseType
    {
        public StateQueryWithStatesType StateQuery { get; set; }
    }

    private class StateQueryWithStatesType
    {
        public byte[][] States { get; set; }
    }

    private class GetBalanceResponseType
    {
        public StateQueryWithBalanceType StateQuery { get; set; }
    }

    private class StateQueryWithBalanceType
    {
        public FungibleAssetValueWithStringType Balance { get; set; }
    }

    private class FungibleAssetValueWithStringType
    {
        public string String { get; set; }
    }

    private class GetTotalSupplyResponseType
    {
        public StateQueryWithTotalSupplyType StateQuery { get; set; }
    }

    private class StateQueryWithTotalSupplyType
    {
        public FungibleAssetValueWithStringType TotalSupply { get; set; }
    }

    private class GetValidatorsResponseType
    {
        public StateQueryWithValidatorsType StateQuery { get; set; }
    }

    private class StateQueryWithValidatorsType
    {
        public ValidatorType[] Validators { get; set; }
    }

    private class ValidatorType
    {
        public string PublicKey { get; set; }
        public long Power { get; set; }
    }

    public bool Legacy { get; }
    public IAccount GetAccount(Address address)
    {
        throw new NotImplementedException();
    }

    
}