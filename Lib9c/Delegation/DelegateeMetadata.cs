#nullable enable
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Numerics;
using Bencodex.Types;
using Bencodex;
using Libplanet.Crypto;
using Libplanet.Types.Assets;

namespace Nekoyume.Delegation
{
    public class DelegateeMetadata : IDelegatee
    {
        private readonly IDelegationRepository? _repository;
        private ImmutableSortedSet<UnbondingRef> _unbondingRefs;

        public DelegateeMetadata(
            Address address,
            Address accountAddress,
            Currency delegationCurrency,
            Currency rewardCurrency,
            Address delegationPoolAddress,
            long unbondingPeriod,
            int maxUnbondLockInEntries,
            int maxRebondGraceEntries,
            IDelegationRepository? repository = null)
        {
            Address = address;
            AccountAddress = accountAddress;
            DelegationCurrency = delegationCurrency;
            RewardCurrency = rewardCurrency;
            DelegationPoolAddress = delegationPoolAddress;
            UnbondingPeriod = unbondingPeriod;
            MaxUnbondLockInEntries = maxUnbondLockInEntries;
            MaxRebondGraceEntries = maxRebondGraceEntries;
            Delegators = ImmutableSortedSet<Address>.Empty;
            TotalDelegated = DelegationCurrency * 0;
            TotalShares = BigInteger.Zero;
            Jailed = false;
            JailedUntil = -1L;
            Tombstoned = false;
            _unbondingRefs = ImmutableSortedSet<UnbondingRef>.Empty;
            _repository = repository;
        }

        public DelegateeMetadata(
            Address address,
            Address accountAddress,
            IValue bencoded,
            IDelegationRepository? repository = null)
            : this(address, accountAddress, (List)bencoded, repository)
        {
        }

        public DelegateeMetadata(
            Address address,
            Address accountAddress,
            List bencoded,
            IDelegationRepository? repository = null)
            : this(
                  address,
                  accountAddress,
                  new Currency((Binary)bencoded[0]),
                  new Currency((Binary)bencoded[1]),
                  new Address((Binary)bencoded[2]),
                  (Integer)bencoded[3],
                  (Integer)bencoded[4],
                  (Integer)bencoded[5],
                  ((List)bencoded[6]).Select(item => new Address(item)),
                  new FungibleAssetValue(bencoded[7]),
                  (Integer)bencoded[8],
                  (Bencodex.Types.Boolean)bencoded[9],
                  (Integer)bencoded[10],
                  (Bencodex.Types.Boolean)bencoded[11],
                  ((List)bencoded[12]).Select(item => new UnbondingRef(item)),
                  repository)
        {
        }

        private DelegateeMetadata(
            Address address,
            Address accountAddress,
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
            IEnumerable<UnbondingRef> unbondingRefs,
            IDelegationRepository? repository)
        {
            if (!totalDelegated.Currency.Equals(DelegationCurrency))
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

            Address = address;
            AccountAddress = accountAddress;
            DelegationCurrency = delegationCurrency;
            RewardCurrency = rewardCurrency;
            DelegationPoolAddress = delegationPoolAddress;
            UnbondingPeriod = unbondingPeriod;
            MaxUnbondLockInEntries = maxUnbondLockInEntries;
            MaxRebondGraceEntries = maxRebondGraceEntries;
            Delegators = delegators.ToImmutableSortedSet();
            TotalDelegated = totalDelegated;
            TotalShares = totalShares;
            Jailed = jailed;
            JailedUntil = jailedUntil;
            Tombstoned = tombstoned;
            _unbondingRefs = unbondingRefs.ToImmutableSortedSet();
            _repository = repository;
        }

        public Address Address { get; }

        public Address AccountAddress { get; }

        public Address HeaderAddress
            => DelegationAddress.DelegateeMetadataAddress(Address, AccountAddress);

        public Currency DelegationCurrency { get; }

        public Currency RewardCurrency { get; }

        public Address DelegationPoolAddress { get; }

        public long UnbondingPeriod { get; }

        public int MaxUnbondLockInEntries { get; }

        public int MaxRebondGraceEntries { get; }

        public Address RewardCollectorAddress
            => DelegationAddress.RewardCollectorAddress(Address, AccountAddress);

        public Address RewardDistributorAddress
            => DelegationAddress.RewardDistributorAddress(Address, AccountAddress);

        public ImmutableSortedSet<Address> Delegators { get; private set; }

        public FungibleAssetValue TotalDelegated { get; private set; }

        public BigInteger TotalShares { get; private set; }

        public bool Jailed { get; private set; }

        public long JailedUntil { get; private set; }

        public bool Tombstoned { get; private set; }

        public IDelegationRepository? Repository => _repository;

        public virtual List Bencoded => List.Empty
            .Add(DelegationCurrency.Serialize())
            .Add(RewardCurrency.Serialize())
            .Add(DelegationPoolAddress.Bencoded)
            .Add(UnbondingPeriod)
            .Add(MaxUnbondLockInEntries)
            .Add(MaxRebondGraceEntries)
            .Add(new List(Delegators.Select(delegator => delegator.Bencoded)))
            .Add(TotalDelegated.Serialize())
            .Add(TotalShares)
            .Add(Jailed)
            .Add(JailedUntil)
            .Add(Tombstoned)
            .Add(new List(_unbondingRefs.Select(unbondingRef => unbondingRef.Bencoded)));

        IValue IBencodable.Bencoded => Bencoded;

        public event EventHandler<FungibleAssetValue>? DelegationChanged;

        public BigInteger ShareToBond(FungibleAssetValue fav)
            => TotalShares.IsZero
                ? fav.RawValue
                : TotalShares * fav.RawValue / TotalDelegated.RawValue;

        public FungibleAssetValue FAVToUnbond(BigInteger share)
            => TotalShares == share
                ? TotalDelegated
                : (TotalDelegated * share).DivRem(TotalShares).Quotient;

        public BigInteger Bond(DelegatorMetadata delegator, FungibleAssetValue fav, long height)
        {
            CannotMutateRelationsWithoutRepository(delegator);
            DistributeReward(delegator, height);

            if (!fav.Currency.Equals(DelegationCurrency))
            {
                throw new InvalidOperationException(
                    "Cannot bond with invalid currency.");
            }

            Bond bond = _repository!.GetBond(this, delegator.Address);
            BigInteger share = ShareToBond(fav);
            bond = bond.AddShare(share);
            Delegators = Delegators.Add(delegator.Address);
            TotalShares += share;
            TotalDelegated += fav;
            _repository.SetBond(bond);
            StartNewRewardPeriod(height);
            DelegationChanged?.Invoke(this, TotalDelegated);

            return share;
        }

        BigInteger IDelegatee.Bond(IDelegator delegator, FungibleAssetValue fav, long height)
            => Bond((DelegatorMetadata)delegator, fav, height);

        public FungibleAssetValue Unbond(DelegatorMetadata delegator, BigInteger share, long height)
        {
            CannotMutateRelationsWithoutRepository(delegator);
            DistributeReward(delegator, height);
            if (TotalShares.IsZero || TotalDelegated.RawValue.IsZero)
            {
                throw new InvalidOperationException(
                    "Cannot unbond without bonding.");
            }

            Bond bond = _repository!.GetBond(this, delegator.Address);
            FungibleAssetValue fav = FAVToUnbond(share);
            bond = bond.SubtractShare(share);
            if (bond.Share.IsZero)
            {
                Delegators = Delegators.Remove(delegator.Address);
            }

            TotalShares -= share;
            TotalDelegated -= fav;
            _repository.SetBond(bond);
            StartNewRewardPeriod(height);
            DelegationChanged?.Invoke(this, TotalDelegated);

            return fav;
        }

        FungibleAssetValue IDelegatee.Unbond(IDelegator delegator, BigInteger share, long height)
            => Unbond((DelegatorMetadata)delegator, share, height);

        public void DistributeReward(DelegatorMetadata delegator, long height)
        {
            CannotMutateRelationsWithoutRepository(delegator);
            BigInteger share = _repository!.GetBond(this, delegator.Address).Share;
            IEnumerable<LumpSumRewardsRecord> lumpSumRewardsRecords =
                GetLumpSumRewardsRecords(delegator.LastRewardHeight);
            FungibleAssetValue reward = CalculateReward(share, lumpSumRewardsRecords);
            if (reward.Sign > 0)
            {
                _repository.TransferAsset(RewardDistributorAddress, delegator.Address, reward);
            }

            delegator.UpdateLastRewardHeight(height);
        }

        void IDelegatee.DistributeReward(IDelegator delegator, long height)
            => DistributeReward((DelegatorMetadata)delegator, height);

        public void CollectRewards(long height)
        {
            CannotMutateRelationsWithoutRepository();
            FungibleAssetValue rewards = _repository!.GetBalance(RewardCollectorAddress, RewardCurrency);
            _repository!.AddLumpSumRewards(this, height, rewards);
            _repository!.TransferAsset(RewardCollectorAddress, RewardDistributorAddress, rewards);
        }

        public void Slash(BigInteger slashFactor, long infractionHeight)
        {
            CannotMutateRelationsWithoutRepository();

            FungibleAssetValue? fav = null;
            foreach (var item in _unbondingRefs)
            {
                var unbonding = UnbondingFactory.GetUnbondingFromRef(item, _repository);

                unbonding = unbonding.Slash(slashFactor, infractionHeight, out var slashedFAV);

                if (slashedFAV.HasValue)
                {
                    fav = fav.HasValue
                        ? fav.Value + slashedFAV.Value
                        : slashedFAV.Value;
                }

                if (unbonding.IsEmpty)
                {
                    RemoveUnbondingRef(item);
                }

                switch (unbonding)
                {
                    case UnbondLockIn unbondLockIn:
                        _repository!.SetUnbondLockIn(unbondLockIn);
                        break;
                    case RebondGrace rebondGrace:
                        _repository!.SetRebondGrace(rebondGrace);
                        break;
                    default:
                        throw new InvalidOperationException("Invalid unbonding type.");
                }
            }

            if (fav.HasValue)
            {
                TotalDelegated -= fav.Value;
            }

            DelegationChanged?.Invoke(this, TotalDelegated);
        }

        void IDelegatee.Slash(BigInteger slashFactor, long infractionHeight)
            => Slash(slashFactor, infractionHeight);

        public void JailUntil(long height)
        {
            CannotMutateRelationsWithoutRepository();
            JailedUntil = height;
            Jailed = true;
        }

        void IDelegatee.JailUntil(long height)
            => JailUntil(height);

        public void Unjail(long height)
        {
            CannotMutateRelationsWithoutRepository();
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

        void IDelegatee.Unjail(long height)
            => Unjail(height);

        public void Tombstone()
        {
            Tombstoned = true;
        }

        void IDelegatee.Tombstone()
            => Tombstone();

        public void AddUnbondingRef(UnbondingRef unbondingRef)
        {
            _unbondingRefs = _unbondingRefs.Add(unbondingRef);
        }

        public void RemoveUnbondingRef(UnbondingRef unbondingRef)
        {
            _unbondingRefs = _unbondingRefs.Remove(unbondingRef);
        }

        public Address BondAddress(Address delegatorAddress)
            => DelegationAddress.BondAddress(Address, AccountAddress, delegatorAddress);

        public Address UnbondLockInAddress(Address delegatorAddress)
            => DelegationAddress.UnbondLockInAddress(Address, AccountAddress, delegatorAddress);

        public virtual Address RebondGraceAddress(Address delegatorAddress)
            => DelegationAddress.RebondGraceAddress(Address, AccountAddress, delegatorAddress);

        public virtual Address CurrentLumpSumRewardsRecordAddress()
            => DelegationAddress.CurrentLumpSumRewardsRecordAddress(Address, AccountAddress);

        public virtual Address LumpSumRewardsRecordAddress(long height)
            => DelegationAddress.LumpSumRewardsRecordAddress(Address, AccountAddress, height);

        public override bool Equals(object? obj)
            => obj is IDelegatee other && Equals(other);

        public virtual bool Equals(IDelegatee? other)
            => ReferenceEquals(this, other)
            || (other is DelegateeMetadata delegatee
            && (GetType() != delegatee.GetType())
            && Address.Equals(delegatee.Address)
            && AccountAddress.Equals(delegatee.AccountAddress)
            && DelegationCurrency.Equals(delegatee.DelegationCurrency)
            && RewardCurrency.Equals(delegatee.RewardCurrency)
            && DelegationPoolAddress.Equals(delegatee.DelegationPoolAddress)
            && UnbondingPeriod == delegatee.UnbondingPeriod
            && RewardCollectorAddress.Equals(delegatee.RewardCollectorAddress)
            && RewardDistributorAddress.Equals(delegatee.RewardDistributorAddress)
            && Delegators.SequenceEqual(delegatee.Delegators)
            && TotalDelegated.Equals(delegatee.TotalDelegated)
            && TotalShares.Equals(delegatee.TotalShares)
            && Jailed == delegatee.Jailed
            && _unbondingRefs.SequenceEqual(delegatee._unbondingRefs));

        public override int GetHashCode()
            => Address.GetHashCode();

        private void StartNewRewardPeriod(long height)
        {
            CannotMutateRelationsWithoutRepository();
            LumpSumRewardsRecord? currentRecord = _repository!.GetCurrentLumpSumRewardsRecord(this);
            long? lastStartHeight = null;
            if (currentRecord is LumpSumRewardsRecord lastRecord)
            {
                lastStartHeight = lastRecord.StartHeight;
                if (lastStartHeight == height)
                {
                    currentRecord = new(
                        currentRecord.Address,
                        currentRecord.StartHeight,
                        TotalShares,
                        RewardCurrency,
                        currentRecord.LastStartHeight);

                    _repository.SetLumpSumRewardsRecord(currentRecord);
                    return;
                }

                _repository.SetLumpSumRewardsRecord(
                    lastRecord.MoveAddress(
                        LumpSumRewardsRecordAddress(lastRecord.StartHeight)));
            }

            LumpSumRewardsRecord newRecord = new(
                CurrentLumpSumRewardsRecordAddress(),
                height,
                TotalShares,
                RewardCurrency,
                lastStartHeight);

            _repository.SetLumpSumRewardsRecord(newRecord);
        }

        private FungibleAssetValue CalculateReward(
            BigInteger share,
            IEnumerable<LumpSumRewardsRecord> lumpSumRewardsRecords)
        {
            FungibleAssetValue reward = RewardCurrency * 0;
            long? linkedStartHeight = null;

            foreach (LumpSumRewardsRecord record in lumpSumRewardsRecords)
            {
                if (!(record.StartHeight is long startHeight))
                {
                    throw new ArgumentException("lump sum reward record wasn't started.");
                }

                if (linkedStartHeight is long startHeightFromHigher
                    && startHeightFromHigher != startHeight)
                {
                    throw new ArgumentException("lump sum reward record was started.");
                }

                reward += record.RewardsDuringPeriod(share);
                linkedStartHeight = record.LastStartHeight;

                if (linkedStartHeight == -1)
                {
                    break;
                }
            }

            return reward;
        }

        private List<LumpSumRewardsRecord> GetLumpSumRewardsRecords(long? lastRewardHeight)
        {
            CannotMutateRelationsWithoutRepository();
            List<LumpSumRewardsRecord> records = new();
            if (lastRewardHeight is null
                || !(_repository!.GetCurrentLumpSumRewardsRecord(this) is LumpSumRewardsRecord record))
            {
                return records;
            }

            while (record.StartHeight >= lastRewardHeight)
            {
                records.Add(record);

                if (!(record.LastStartHeight is long lastStartHeight))
                {
                    break;
                }

                record = _repository.GetLumpSumRewardsRecord(this, lastStartHeight)
                    ?? throw new InvalidOperationException(
                        $"Lump sum rewards record for #{lastStartHeight} is missing");
            }

            return records;
        }

        private void CannotMutateRelationsWithoutRepository(DelegatorMetadata delegator)
        {
            CannotMutateRelationsWithoutRepository();
            if (!_repository!.Equals(delegator.Repository))
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
