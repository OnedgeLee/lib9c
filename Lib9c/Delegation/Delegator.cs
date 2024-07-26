using System;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Types.Assets;

namespace Nekoyume.Delegation
{
    public abstract class Delegator : IDelegator
    {
        public Delegator(Address address)
        {
            Address = address;
            Delegatees = ImmutableSortedSet<Address>.Empty;
        }

        public Address Address { get; }

        public ImmutableSortedSet<Address> Delegatees { get; private set; }

        public IValue Bencoded
            => new List(Delegatees.Select(a => a.Bencoded));

        public Delegation Delegate(
            IDelegatee<IDelegator> delegatee,
            FungibleAssetValue fav,
            Delegation delegation)
        {
            delegation = delegatee.Bond(this, fav, delegation);
            Delegatees = Delegatees.Add(delegatee.Address);

            return delegation;
        }

        public Delegation Undelegate(
            IDelegatee<IDelegator> delegatee,
            BigInteger share,
            long height,
            Delegation delegation)
        {
            if (delegation.UnbondLockIn.IsFull)
            {
                throw new InvalidOperationException("Undelegation is full.");
            }

            delegation = delegatee.Unbond(this, share, delegation);

            if (!(delegation.IncompleteUnbond is FungibleAssetValue unbondToLockIn))
            {
                throw new NullReferenceException("Bonding FAV is null.");
            }

            delegation.DoUnbondLockIn(unbondToLockIn, height, height + delegatee.UnbondingPeriod);
            delegation.Complete();

            return delegation;
        }

        public Delegation Redelegate(
            IDelegatee<IDelegator> srcDelegatee,
            IDelegatee<IDelegator> dstDelegatee,
            BigInteger share,
            long height,
            Delegation delegation)
        {
            delegation = srcDelegatee.Unbond(this, share, delegation);

            if (!(delegation.IncompleteUnbond is FungibleAssetValue unbondToGrace))
            {
                throw new NullReferenceException("Bonding FAV is null.");
            }

            delegation = dstDelegatee.Bond(this, unbondToGrace, delegation);
            delegation.DoRebondGrace(dstDelegatee.Address, unbondToGrace, height, height + srcDelegatee.UnbondingPeriod);
            delegation.Complete();

            return delegation;
        }

        public void Claim(IDelegatee<IDelegator> delegatee)
        {
            // TODO: Implement this
        }
    }
}
