#nullable enable
using Bencodex;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;

namespace Nekoyume.Delegation
{
    public class DelegateeMetadata : IDelegateeMetadata
    {
        private Address? _address;

        public DelegateeMetadata(
            Address delegateeAddress,
            Address delegateeAccountAddress,
            Currency delegationCurrency,
            Currency rewardCurrency,
            Address delegationPoolAddress,
            long unbondingPeriod,
            int maxUnbondLockInEntries,
            int maxRebondGraceEntries)
            : this(
                  delegateeAddress,
                  delegateeAccountAddress,
                  delegationCurrency,
                  rewardCurrency,
                  delegationPoolAddress,
                  unbondingPeriod,
                  maxUnbondLockInEntries,
                  maxRebondGraceEntries,
                  ImmutableSortedSet<Address>.Empty,
                  delegationCurrency * 0,
                  BigInteger.Zero,
                  false,
                  -1L,
                  false,
                  ImmutableSortedSet<UnbondingRef>.Empty)
        {   
        }

        public DelegateeMetadata(
            Address delegateeAddress,
            Address delegateeAccountAddress,
            IValue bencoded)
            : this(delegateeAddress, delegateeAccountAddress, (List)bencoded)
        {
        }

        public DelegateeMetadata(
            Address address,
            Address accountAddress,
            List bencoded)
            : this(
                  address,
                  accountAddress,
                  new Currency(bencoded[0]),
                  new Currency(bencoded[1]),
                  new Address(bencoded[2]),
                  (Integer)bencoded[3],
                  (Integer)bencoded[4],
                  (Integer)bencoded[5],
                  ((List)bencoded[6]).Select(item => new Address(item)),
                  new FungibleAssetValue(bencoded[7]),
                  (Integer)bencoded[8],
                  (Bencodex.Types.Boolean)bencoded[9],
                  (Integer)bencoded[10],
                  (Bencodex.Types.Boolean)bencoded[11],
                  ((List)bencoded[12]).Select(item => new UnbondingRef(item)))
        {
        }

        private DelegateeMetadata(
            Address delegateeAddress,
            Address delegateeAccountAddress,
            Currency delegationCurrency,
            Currency rewardCurrency,
            Address delegationPoolAddress,
            long unbondingPeriod,
            int maxUnbondLockInEntries,
            int maxRebondGraceEntries,
            IEnumerable<Address> delegators,
            FungibleAssetValue totalDelegated,
            BigInteger totalShares,
            bool jailed,
            long jailedUntil,
            bool tombstoned,
            IEnumerable<UnbondingRef> unbondingRefs)
        {
            if (!totalDelegated.Currency.Equals(delegationCurrency))
            {
                throw new InvalidOperationException("Invalid currency.");
            }

            if (totalDelegated.Sign < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalDelegated),
                    totalDelegated,
                    "Total delegated must be non-negative.");
            }

            if (totalShares.Sign < 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(totalShares),
                    totalShares,
                    "Total shares must be non-negative.");
            }

            DelegateeAddress = delegateeAddress;
            DelegateeAccountAddress = delegateeAccountAddress;
            DelegationCurrency = delegationCurrency;
            RewardCurrency = rewardCurrency;
            DelegationPoolAddress = delegationPoolAddress;
            UnbondingPeriod = unbondingPeriod;
            MaxUnbondLockInEntries = maxUnbondLockInEntries;
            MaxRebondGraceEntries = maxRebondGraceEntries;
            Delegators = delegators.ToImmutableSortedSet();
            TotalDelegatedFAV = totalDelegated;
            TotalShares = totalShares;
            Jailed = jailed;
            JailedUntil = jailedUntil;
            Tombstoned = tombstoned;
            UnbondingRefs = unbondingRefs.ToImmutableSortedSet();
        }

        public Address DelegateeAddress { get; }

        public Address DelegateeAccountAddress { get; }

        public Address Address
            => _address ??= DelegationAddress.DelegateeMetadataAddress(
                DelegateeAddress,
                DelegateeAccountAddress);

        public Currency DelegationCurrency { get; }

        public Currency RewardCurrency { get; }

        public Address DelegationPoolAddress { get; }

        public long UnbondingPeriod { get; }

        public int MaxUnbondLockInEntries { get; }

        public int MaxRebondGraceEntries { get; }

        public Address RewardCollectorAddress
            => DelegationAddress.RewardCollectorAddress(Address);

        public Address RewardDistributorAddress
            => DelegationAddress.RewardDistributorAddress(Address);

        public ImmutableSortedSet<Address> Delegators { get; private set; }

        public FungibleAssetValue TotalDelegatedFAV { get; private set; }

        public BigInteger TotalShares { get; private set; }

        public bool Jailed { get; private set; }

        public long JailedUntil { get; private set; }

        public bool Tombstoned { get; private set; }

        public ImmutableSortedSet<UnbondingRef> UnbondingRefs { get; private set; }

        public List Bencoded => List.Empty
            .Add(DelegationCurrency.Serialize())
            .Add(RewardCurrency.Serialize())
            .Add(DelegationPoolAddress.Bencoded)
            .Add(UnbondingPeriod)
            .Add(MaxUnbondLockInEntries)
            .Add(MaxRebondGraceEntries)
            .Add(new List(Delegators.Select(delegator => delegator.Bencoded)))
            .Add(TotalDelegatedFAV.Serialize())
            .Add(TotalShares)
            .Add(Jailed)
            .Add(JailedUntil)
            .Add(Tombstoned)
            .Add(new List(UnbondingRefs.Select(unbondingRef => unbondingRef.Bencoded)));

        IValue IBencodable.Bencoded => Bencoded;

        public BigInteger ShareFromFAV(FungibleAssetValue fav)
            => TotalShares.IsZero
                ? fav.RawValue
                : TotalShares * fav.RawValue / TotalDelegatedFAV.RawValue;

        public FungibleAssetValue FAVFromShare(BigInteger share)
            => TotalShares == share
                ? TotalDelegatedFAV
                : (TotalDelegatedFAV * share).DivRem(TotalShares).Quotient;

        public void AddDelegator(Address delegatorAddress)
        {
            Delegators = Delegators.Add(delegatorAddress);
        }

        public void RemoveDelegator(Address delegatorAddress)
        {
            Delegators = Delegators.Remove(delegatorAddress);
        }

        public void AddDelegatedFAV(FungibleAssetValue fav)
        {
            TotalDelegatedFAV += fav;
        }

        public void RemoveDelegatedFAV(FungibleAssetValue fav)
        {
            TotalDelegatedFAV -= fav;
        }

        public void AddShare(BigInteger share)
        {
            TotalShares += share;
        }

        public void RemoveShare(BigInteger share)
        {
            TotalShares -= share;
        }

        public void JailUntil(long height)
        {
            JailedUntil = height;
            Jailed = true;
        }

        public void Unjail(long height)
        {
            if (Tombstoned)
            {
                throw new InvalidOperationException("Cannot unjail tombstoned delegatee.");
            }

            if (JailedUntil > height)
            {
                throw new InvalidOperationException("Cannot unjail before jailed until.");
            }

            JailedUntil = -1L;
            Jailed = false;
        }

        public void Tombstone()
        {
            JailUntil(long.MaxValue);
            Tombstoned = true;
        }

        public void AddUnbondingRef(UnbondingRef unbondingRef)
        {
            UnbondingRefs = UnbondingRefs.Add(unbondingRef);
        }

        public void RemoveUnbondingRef(UnbondingRef unbondingRef)
        {
            UnbondingRefs = UnbondingRefs.Remove(unbondingRef);
        }

        public Address BondAddress(Address delegatorAddress)
            => DelegationAddress.BondAddress(Address, delegatorAddress);

        public Address UnbondLockInAddress(Address delegatorAddress)
            => DelegationAddress.UnbondLockInAddress(Address, delegatorAddress);

        public virtual Address RebondGraceAddress(Address delegatorAddress)
            => DelegationAddress.RebondGraceAddress(Address, delegatorAddress);

        public virtual Address CurrentLumpSumRewardsRecordAddress()
            => DelegationAddress.CurrentLumpSumRewardsRecordAddress(Address);

        public virtual Address LumpSumRewardsRecordAddress(long height)
            => DelegationAddress.LumpSumRewardsRecordAddress(Address, height);

        public override bool Equals(object? obj)
            => obj is IDelegateeMetadata other && Equals(other);

        public virtual bool Equals(IDelegateeMetadata? other)
            => ReferenceEquals(this, other)
            || (other is DelegateeMetadata delegatee
            && (GetType() != delegatee.GetType())
            && DelegateeAddress.Equals(delegatee.DelegateeAddress)
            && DelegateeAccountAddress.Equals(delegatee.DelegateeAccountAddress)
            && DelegationCurrency.Equals(delegatee.DelegationCurrency)
            && RewardCurrency.Equals(delegatee.RewardCurrency)
            && DelegationPoolAddress.Equals(delegatee.DelegationPoolAddress)
            && UnbondingPeriod == delegatee.UnbondingPeriod
            && RewardCollectorAddress.Equals(delegatee.RewardCollectorAddress)
            && RewardDistributorAddress.Equals(delegatee.RewardDistributorAddress)
            && Delegators.SequenceEqual(delegatee.Delegators)
            && TotalDelegatedFAV.Equals(delegatee.TotalDelegatedFAV)
            && TotalShares.Equals(delegatee.TotalShares)
            && Jailed == delegatee.Jailed
            && UnbondingRefs.SequenceEqual(delegatee.UnbondingRefs));

        public override int GetHashCode()
            => DelegateeAddress.GetHashCode();
    }
}