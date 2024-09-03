#nullable enable
using System.Numerics;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Types.Assets;

namespace Nekoyume.Delegation
{
    public abstract class Delegator<T, TSelf> : DelegatorMetadata
        where T : Delegatee<TSelf, T>
        where TSelf : Delegator<T, TSelf>
    {
        public Delegator(Address address, Address accountAddress, IDelegationRepository? repository = null)
            : base(address, accountAddress, repository)
        {
        }

        public Delegator(Address address, Address accountAddress, IValue bencoded, IDelegationRepository? repository = null)
            : base(address, accountAddress, bencoded, repository)
        {
        }

        public Delegator(Address address, Address accountAddress, List bencoded, IDelegationRepository? repository = null)
            : base(address, accountAddress, bencoded, repository)
        {
        }

        public virtual void Delegate(
            T delegatee, FungibleAssetValue fav, long height)
            => Delegate((DelegateeMetadata)delegatee, fav, height);

        public virtual void Undelegate(
            T delegatee, BigInteger share, long height)
            => Undelegate((DelegateeMetadata)delegatee, share, height);
        public virtual void Redelegate(
            T srcDelegatee, T dstDelegatee, BigInteger share, long height)
            => Redelegate((DelegateeMetadata)srcDelegatee, (DelegateeMetadata)dstDelegatee, share, height);

        public virtual void CancelUndelegate(
            T delegatee, FungibleAssetValue fav, long height)
            => CancelUndelegate((DelegateeMetadata)delegatee, fav, height);

        public virtual void ClaimReward(
            T delegatee, long height)
            => ClaimReward((DelegateeMetadata)delegatee, height);
    }
}
