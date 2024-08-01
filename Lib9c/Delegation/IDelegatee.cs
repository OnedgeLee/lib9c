using System.Collections.Immutable;
using System.Numerics;
using Bencodex;
using Libplanet.Crypto;
using Libplanet.Types.Assets;

namespace Nekoyume.Delegation
{
    public interface IDelegatee<T> : IBencodable
        where T : IDelegator
    {
        Address Address { get; }

        Currency Currency { get; }

        Address PoolAddress { get; }

        long UnbondingPeriod { get; }

        Address RewardPoolAddress { get; }

        ImmutableSortedSet<Address> Delegators { get; }

        FungibleAssetValue TotalDelegated { get; }

        BigInteger TotalShares { get; }

        Delegation Bond(T delegator, FungibleAssetValue fav, Delegation delegation);

        Delegation Unbond(T delegator, BigInteger share , Delegation delegation);

        void Distribute();

        Address BondAddress(Address delegatorAddress);

        Address UnbondLockInAddress(Address delegatorAddress);

        Address RebondGraceAddress(Address delegatorAddress);
    }
}
