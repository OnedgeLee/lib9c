using System.Collections.Immutable;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Store.Trie;
using Libplanet.Types.Assets;
using Libplanet.Types.Consensus;

namespace Libplanet.Extensions.RemoteBlockChainStates
{
    public class RemoteAccount : IAccount
    {
        IAccountState _accountState;

        public RemoteAccount(IAccountState accountState)
        {
            _accountState = accountState;
        }

        public IAccountDelta Delta => throw new NotImplementedException();

        public IImmutableSet<(Address, Currency)> TotalUpdatedFungibleAssets => throw new NotImplementedException();

        public ITrie Trie => throw new NotImplementedException();

        public IAccount BurnAsset(IActionContext context, Address owner, FungibleAssetValue value)
        {
            throw new NotImplementedException();
        }

        public FungibleAssetValue GetBalance(Address address, Currency currency)
        {
            return _accountState.GetBalance(address, currency);
        }

        public IValue? GetState(Address address)
        {
            return _accountState.GetState(address);
        }

        public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses)
        {
            return _accountState.GetStates(addresses);
        }

        public FungibleAssetValue GetTotalSupply(Currency currency)
        {
            return _accountState.GetTotalSupply(currency);
        }

        public ValidatorSet GetValidatorSet()
        {
            return _accountState.GetValidatorSet();
        }

        public IAccount MintAsset(IActionContext context, Address recipient, FungibleAssetValue value)
        {
            throw new NotImplementedException();
        }

        public IAccount SetState(Address address, IValue state)
        {
            throw new NotImplementedException();
        }

        public IAccount SetValidator(Validator validator)
        {
            throw new NotImplementedException();
        }

        public IAccount TransferAsset(IActionContext context, Address sender, Address recipient, FungibleAssetValue value, bool allowNegativeBalance = false)
        {
            throw new NotImplementedException();
        }
    }
}
