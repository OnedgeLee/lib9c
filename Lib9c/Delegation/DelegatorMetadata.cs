#nullable enable
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using Bencodex;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Types.Assets;

namespace Nekoyume.Delegation
{
    public class DelegatorMetadata : IDelegator
    {
        private readonly IDelegationRepository? _repository;

        public DelegatorMetadata(
            Address address,
            Address accountAddress,
            IDelegationRepository? repository = null)
            : this(
                  address,
                  accountAddress,
                  ImmutableSortedSet<Address>.Empty,
                  null,
                  repository)
        {
        }

        public DelegatorMetadata(
            Address address,
            Address accountAddress,
            IValue bencoded,
            IDelegationRepository? repository = null)
            : this(address, accountAddress, (List)bencoded, repository)
        {
        }

        public DelegatorMetadata(
            Address address,
            Address accountAddress,
            List bencoded,
            IDelegationRepository? repository = null)
            : this(
                address,
                accountAddress,
                ((List)bencoded[0]).Select(item => new Address(item)).ToImmutableSortedSet(),
                bencoded[1] is Integer lastRewardHeight ? lastRewardHeight : null,
                repository)
        {
        }

        private DelegatorMetadata(
            Address address,
            Address accountAddress,
            ImmutableSortedSet<Address> delegatees,
            long? lastRewardHeight,
            IDelegationRepository? repository)
        {
            Address = address;
            AccountAddress = accountAddress;
            Delegatees = delegatees;
            LastRewardHeight = lastRewardHeight;
            _repository = repository;
        }

        public Address Address { get; }

        public Address AccountAddress { get; }

        public ImmutableSortedSet<Address> Delegatees { get; private set; }

        public long? LastRewardHeight { get; private set; }

        public IDelegationRepository? Repository => _repository;

        public virtual List Bencoded
            => List.Empty
                .Add(new List(Delegatees.Select(a => a.Bencoded)))
                .Add(LastRewardHeight is long height ? (Integer)height : Null.Value);

        IValue IBencodable.Bencoded => Bencoded;

        public void Delegate(
            DelegateeMetadata delegatee, FungibleAssetValue fav, long height)
        {
            CannotMutateRelationsWithoutRepository(delegatee);
            if (fav.Sign <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fav), fav, "Fungible asset value must be positive.");
            }

            delegatee.Bond(this, fav, height);
            Delegatees = Delegatees.Add(delegatee.Address);
            _repository!.TransferAsset(Address, delegatee.DelegationPoolAddress, fav);
        }

        void IDelegator.Delegate(
            IDelegatee delegatee, FungibleAssetValue fav, long height)
            => Delegate((DelegateeMetadata)delegatee, fav, height);

        public void Undelegate(
            DelegateeMetadata delegatee, BigInteger share, long height)
        {
            CannotMutateRelationsWithoutRepository(delegatee);
            if (share.Sign <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(share), share, "Share must be positive.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(height), height, "Height must be positive.");
            }

            UnbondLockIn unbondLockIn = _repository!.GetUnbondLockIn(delegatee, Address);

            if (unbondLockIn.IsFull)
            {
                throw new InvalidOperationException("Undelegation is full.");
            }

            FungibleAssetValue fav = delegatee.Unbond(this, share, height);
            unbondLockIn = unbondLockIn.LockIn(
                fav, height, height + delegatee.UnbondingPeriod);

            if (!delegatee.Delegators.Contains(Address))
            {
                Delegatees = Delegatees.Remove(delegatee.Address);
            }

            delegatee.AddUnbondingRef(UnbondingFactory.ToReference(unbondLockIn));

            _repository.SetUnbondLockIn(unbondLockIn);
            _repository.SetUnbondingSet(
                _repository.GetUnbondingSet().SetUnbonding(unbondLockIn));
        }

        void IDelegator.Undelegate(
            IDelegatee delegatee, BigInteger share, long height)
            => Undelegate((DelegateeMetadata)delegatee, share, height);


        public void Redelegate(
            DelegateeMetadata srcDelegatee, DelegateeMetadata dstDelegatee, BigInteger share, long height)
        {
            CannotMutateRelationsWithoutRepository(srcDelegatee);
            CannotMutateRelationsWithoutRepository(dstDelegatee);
            if (share.Sign <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(share), share, "Share must be positive.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(height), height, "Height must be positive.");
            }

            FungibleAssetValue fav = srcDelegatee.Unbond(
                this, share, height);
            dstDelegatee.Bond(
                this, fav, height);
            RebondGrace srcRebondGrace = _repository!.GetRebondGrace(srcDelegatee, Address).Grace(
                dstDelegatee.Address,
                fav,
                height,
                height + srcDelegatee.UnbondingPeriod);

            if (!srcDelegatee.Delegators.Contains(Address))
            {
                Delegatees = Delegatees.Remove(srcDelegatee.Address);
            }

            Delegatees = Delegatees.Add(dstDelegatee.Address);

            srcDelegatee.AddUnbondingRef(UnbondingFactory.ToReference(srcRebondGrace));

            _repository.SetRebondGrace(srcRebondGrace);
            _repository.SetUnbondingSet(
                _repository.GetUnbondingSet().SetUnbonding(srcRebondGrace));
        }

        void IDelegator.Redelegate(
            IDelegatee srcDelegatee, IDelegatee dstDelegatee, BigInteger share, long height)
            => Redelegate((DelegateeMetadata)srcDelegatee, (DelegateeMetadata)dstDelegatee, share, height);

        public void CancelUndelegate(
            DelegateeMetadata delegatee, FungibleAssetValue fav, long height)
        {
            CannotMutateRelationsWithoutRepository(delegatee);
            if (fav.Sign <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(fav), fav, "Fungible asset value must be positive.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(height), height, "Height must be positive.");
            }

            UnbondLockIn unbondLockIn = _repository!.GetUnbondLockIn(delegatee, Address);

            if (unbondLockIn.IsFull)
            {
                throw new InvalidOperationException("Undelegation is full.");
            }

            delegatee.Bond(this, fav, height);
            unbondLockIn = unbondLockIn.Cancel(fav, height);
            Delegatees = Delegatees.Add(delegatee.Address);

            if (unbondLockIn.IsEmpty)
            {
                delegatee.RemoveUnbondingRef(UnbondingFactory.ToReference(unbondLockIn));
            }

            _repository.SetUnbondLockIn(unbondLockIn);
            _repository.SetUnbondingSet(
                _repository.GetUnbondingSet().SetUnbonding(unbondLockIn));
        }

        void IDelegator.CancelUndelegate(
            IDelegatee delegatee, FungibleAssetValue fav, long height)
            => CancelUndelegate((DelegateeMetadata)delegatee, fav, height);

        public void ClaimReward(
            DelegateeMetadata delegatee, long height)
        {
            CannotMutateRelationsWithoutRepository(delegatee);
            delegatee.DistributeReward(this, height);
        }

        void IDelegator.ClaimReward(IDelegatee delegatee, long height)
            => ClaimReward((DelegateeMetadata)delegatee, height);

        public void UpdateLastRewardHeight(long height)
        {
            LastRewardHeight = height;
        }

        public override bool Equals(object? obj)
            => obj is IDelegator other && Equals(other);

        public virtual bool Equals(IDelegator? other)
            => ReferenceEquals(this, other)
            || (other is DelegatorMetadata delegator
            && GetType() != delegator.GetType()
            && Address.Equals(delegator.Address)
            && Delegatees.SequenceEqual(delegator.Delegatees)
            && LastRewardHeight == delegator.LastRewardHeight);

        public override int GetHashCode()
            => Address.GetHashCode();

        private void CannotMutateRelationsWithoutRepository(DelegateeMetadata delegatee)
        {
            CannotMutateRelationsWithoutRepository();
            if (!_repository!.Equals(delegatee.Repository))
            {
                throw new InvalidOperationException(
                    "Cannot mutate with different repository.");
            }
        }

        private void CannotMutateRelationsWithoutRepository()
        {
            if (_repository is null)
            {
                throw new InvalidOperationException(
                    "Cannot mutate without repository.");
            }
        }
    }
}
