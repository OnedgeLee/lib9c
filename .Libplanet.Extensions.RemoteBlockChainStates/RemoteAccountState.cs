using Bencodex;
using Bencodex.Types;
using GraphQL;
using GraphQL.Client.Http;
using GraphQL.Client.Serializer.SystemTextJson;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Extensions.RemoteBlockChainStates;
using Libplanet.Store.Trie;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Consensus;

namespace Nekoyume.Blockchain
{
    public class RemoteAccountState : IAccountState
    {
        private readonly IAccountState _accountState;
        private readonly RemoteBlockState _remoteBlockStates;
        private readonly GraphQLHttpClient _graphQlHttpClient;

        public RemoteAccountState(Uri explorerEndpoint, BlockHash? blockHash, IAccountState accountState)
        {
            _accountState = accountState;
            _remoteBlockStates = new RemoteBlockState(explorerEndpoint, blockHash);
            _graphQlHttpClient = new GraphQLHttpClient(explorerEndpoint, new SystemTextJsonSerializer());
        }

        public RemoteAccountState(Uri explorerEndpoint, long? blockIndex, IAccountState accountState)
        {
            var response = _graphQlHttpClient.SendQueryAsync<GetBlockResponseType>(
                new GraphQLRequest(
                    @"query GetBlockHash($index: Long!)
                    {
                        blockQuery
                        {
                            block(index: $index)
                            {
                                hash
                            }
                        }
                    }",
                    operationName: "GetBlockHash",
                    variables: blockIndex)).Result;
            var codec = new Codec();
            var blockHash = new BlockHash(codec.Decode(response.Data.BlockQuery.Block.Hash));
            _accountState = accountState;
            _remoteBlockStates = new RemoteBlockState(explorerEndpoint, blockHash);
        }

        public ITrie Trie => throw new NotSupportedException();

        public IValue? GetState(Address address)
        {
            IValue? localState = _accountState.GetState(address);
            if (localState is { } state)
            {
                return state;
            }
            else
            {
                return _remoteBlockStates.GetState(address);
            }
        }

        public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses)
        {
            List<IValue?> states = _accountState.GetStates(addresses).ToList();
            IEnumerable<int> indicesToGetRemote = from stateWithIndex in states.Select((state, index) => (state, index))
                                    where stateWithIndex.state is null
                                    select stateWithIndex.index;
            IReadOnlyList<IValue?> remoteStates = _remoteBlockStates.GetStates(
                addresses.Where((address, index) => indicesToGetRemote.Contains(index)).ToList().AsReadOnly());
            foreach (int index in indicesToGetRemote)
            {
                states[index] = remoteStates[index];
            }
            return states.AsReadOnly();
        }

        public FungibleAssetValue GetBalance(Address address, Currency currency)
        {
            FungibleAssetValue localBalance = _accountState.GetBalance(address, currency);
            if (localBalance != currency * 0)
            {
                return localBalance;
            }
            else
            {
                return _remoteBlockStates.GetBalance(address, currency);
            }
        }

        public FungibleAssetValue GetTotalSupply(Currency currency)
        {
            FungibleAssetValue localTotalSupply = _accountState.GetTotalSupply(currency);
            if (localTotalSupply != currency * 0)
            {
                return localTotalSupply;
            }
            else
            {
                return _remoteBlockStates.GetTotalSupply(currency);
            }
        }

        public ValidatorSet GetValidatorSet()
            => _accountState.GetValidatorSet();


        public RemoteAccount ToRemoteAccount()
            => new RemoteAccount(this);

        private class GetBlockResponseType
        {
            public BlockQueryWithBlockType BlockQuery { get; set; }
        }

        private class BlockQueryWithBlockType
        {
            public BlockTypeWithHash Block { get; set; }
        }

        private class BlockTypeWithHash
        {
            public byte[] Hash { get; set; }
        }
    }
}
