#nullable enable
using System.Numerics;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Types.Assets;

namespace Nekoyume.Delegation
{
    public abstract class Delegatee<T, TSelf> : DelegateeMetadata
        where T : Delegator<TSelf, T>
        where TSelf : Delegatee<T, TSelf>
    {
        public Delegatee(
            Address address,
            Address accountAddress,
            Currency delegationCurrency,
            Currency rewardCurrency,
            Address delegationPoolAddress,
            long unbondingPeriod,
            int maxUnbondLockInEntries,
            int maxRebondGraceEntries,
            IDelegationRepository? repository = null)
            : base(
                  address,
                  accountAddress,
                  delegationCurrency,
                  rewardCurrency,
                  delegationPoolAddress,
                  unbondingPeriod,
                  maxUnbondLockInEntries,
                  maxRebondGraceEntries)
        {
        }

        public Delegatee(
            Address address,
            Address accountAddress,
            IValue bencoded,
            IDelegationRepository? repository = null)
            : base(address, accountAddress, bencoded, repository)
        {
        }

        public Delegatee(
            Address address,
            Address accountAddress,
            List bencoded,
            IDelegationRepository? repository = null)
            : base(address, accountAddress, bencoded, repository)
        {
        }

        public virtual BigInteger Bond(T delegator, FungibleAssetValue fav, long height)
            => Bond((DelegatorMetadata)delegator, fav, height);

        public virtual FungibleAssetValue Unbond(T delegator, BigInteger share, long height)
            => Unbond((DelegatorMetadata)delegator, share, height);

        public virtual void DistributeReward(T delegator, long height)
            => DistributeReward((DelegatorMetadata)delegator, height);
    }
}
