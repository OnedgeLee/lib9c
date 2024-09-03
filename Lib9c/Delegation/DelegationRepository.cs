#nullable enable
using System;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Types.Assets;

namespace Nekoyume.Delegation
{
    public class DelegationRepository : IDelegationRepository
    {
        private readonly Address delegateeAccountAddress = Addresses.Delegatee;
        private readonly Address delegatorAccountAddress = Addresses.Delegator;
        private readonly Address bondAccountAddress = Addresses.Bond;
        private readonly Address unbondLockInAccountAddress = Addresses.UnbondLockIn;
        private readonly Address rebondGraceAccountAddress = Addresses.RebondGrace;
        private readonly Address unbondingSetAccountAddress = Addresses.UnbondingSet;
        private readonly Address lumpSumRewardsRecordAccountAddress = Addresses.LumpSumRewardsRecord;

        private IWorld _world;
        private IActionContext _context;
        private IAccount _delegateeAccount;
        private IAccount _delegatorAccount;
        private IAccount _bondAccount;
        private IAccount _unbondLockInAccount;
        private IAccount _rebondGraceAccount;
        private IAccount _unbondingSetAccount;
        private IAccount _lumpSumRewardsRecordAccount;

        public DelegationRepository(IWorld world, IActionContext context)
        {
            _world = world;
            _context = context;
            _delegateeAccount = world.GetAccount(delegateeAccountAddress);
            _delegatorAccount = world.GetAccount(delegatorAccountAddress);
            _bondAccount = world.GetAccount(bondAccountAddress);
            _unbondLockInAccount = world.GetAccount(unbondLockInAccountAddress);
            _rebondGraceAccount = world.GetAccount(rebondGraceAccountAddress);
            _unbondingSetAccount = world.GetAccount(unbondingSetAccountAddress);
            _lumpSumRewardsRecordAccount = world.GetAccount(lumpSumRewardsRecordAccountAddress);
        }

        public virtual IWorld World => _world
            .SetAccount(delegateeAccountAddress, _delegateeAccount)
            .SetAccount(delegatorAccountAddress, _delegatorAccount)
            .SetAccount(bondAccountAddress, _bondAccount)
            .SetAccount(unbondLockInAccountAddress, _unbondLockInAccount)
            .SetAccount(rebondGraceAccountAddress, _rebondGraceAccount)
            .SetAccount(unbondingSetAccountAddress, _unbondingSetAccount)
            .SetAccount(lumpSumRewardsRecordAccountAddress, _lumpSumRewardsRecordAccount);

        public virtual IDelegatee GetDelegatee(Address address, Address accountAddress)
            => GetDelegateeMetadata(address, accountAddress);

        public virtual IDelegator GetDelegator(Address address, Address accountAddress)
            => GetDelegatorMetadata(address, accountAddress);

        public DelegateeMetadata GetDelegateeMetadata(Address address, Address accountAddress)
        {
            IValue? value = _delegateeAccount.GetState(
                DelegationAddress.DelegateeMetadataAddress(address, accountAddress));
            return value is IValue bencoded
                ? new DelegateeMetadata(address, accountAddress, bencoded, this)
                : throw new InvalidOperationException("DelegateeMetadata not found.");
        }

        public DelegatorMetadata GetDelegatorMetadata(Address address, Address accountAddress)
        {
            IValue? value = _delegatorAccount.GetState(
                DelegationAddress.DelegatorMetadataAddress(address, accountAddress));
            return value is IValue bencoded
                ? new DelegatorMetadata(address, accountAddress, bencoded, this)
                : throw new InvalidOperationException("DelegatorMetadata not found.");
        }

        public virtual Bond GetBond(IDelegatee delegatee, Address delegatorAddress)
        {
            Address address = delegatee.BondAddress(delegatorAddress);
            IValue? value = _bondAccount.GetState(address);
            return value is IValue bencoded
                ? new Bond(address, bencoded)
                : new Bond(address);
        }

        public virtual UnbondLockIn GetUnbondLockIn(IDelegatee delegatee, Address delegatorAddress)
        {
            Address address = delegatee.UnbondLockInAddress(delegatorAddress);
            IValue? value = _unbondLockInAccount.GetState(address);
            return value is IValue bencoded
                ? new UnbondLockIn(address, delegatee.MaxUnbondLockInEntries, bencoded, this)
                : new UnbondLockIn(address, delegatee.MaxUnbondLockInEntries, delegatee.DelegationPoolAddress, delegatorAddress, this);
        }

        public virtual UnbondLockIn GetUnlimitedUnbondLockIn(Address address)
        {
            IValue? value = _unbondLockInAccount.GetState(address);
            return value is IValue bencoded
                ? new UnbondLockIn(address, int.MaxValue, bencoded, this)
                : throw new InvalidOperationException("UnbondLockIn not found.");
        }

        public virtual RebondGrace GetRebondGrace(IDelegatee delegatee, Address delegatorAddress)
        {
            Address address = delegatee.RebondGraceAddress(delegatorAddress);
            IValue? value = _rebondGraceAccount.GetState(address);
            return value is IValue bencoded
                ? new RebondGrace(address, delegatee.MaxRebondGraceEntries, bencoded, this)
                : new RebondGrace(address, delegatee.MaxRebondGraceEntries, this);
        }

        public virtual RebondGrace GetUnlimitedRebondGrace(Address address)
        {
            IValue? value = _rebondGraceAccount.GetState(address);
            return value is IValue bencoded
                ? new RebondGrace(address, int.MaxValue, bencoded, this)
                : throw new InvalidOperationException("RebondGrace not found.");
        }

        public virtual UnbondingSet GetUnbondingSet()
            => _unbondingSetAccount.GetState(UnbondingSet.Address) is IValue bencoded
                ? new UnbondingSet(bencoded, this)
                : new UnbondingSet(this);

        public virtual LumpSumRewardsRecord? GetLumpSumRewardsRecord(IDelegatee delegatee, long height)
        {
            Address address = delegatee.LumpSumRewardsRecordAddress(height);
            IValue? value = _lumpSumRewardsRecordAccount.GetState(address);
            return value is IValue bencoded
                ? new LumpSumRewardsRecord(address, bencoded)
                : null;
        }

        public virtual LumpSumRewardsRecord? GetCurrentLumpSumRewardsRecord(IDelegatee delegatee)
        {
            Address address = delegatee.CurrentLumpSumRewardsRecordAddress();
            IValue? value = _lumpSumRewardsRecordAccount.GetState(address);
            return value is IValue bencoded
                ? new LumpSumRewardsRecord(address, bencoded)
                : null;
        }

        public virtual FungibleAssetValue GetBalance(Address address, Currency currency)
            => _world.GetBalance(address, currency);

        public virtual void SetBond(Bond bond)
        {
            _bondAccount = bond.IsEmpty
                ? _bondAccount.RemoveState(bond.Address)
                : _bondAccount.SetState(bond.Address, bond.Bencoded);
        }

        public virtual void SetUnbondLockIn(UnbondLockIn unbondLockIn)
        {
            _unbondLockInAccount = unbondLockIn.IsEmpty
                ? _unbondLockInAccount.RemoveState(unbondLockIn.Address)
                : _unbondLockInAccount.SetState(unbondLockIn.Address, unbondLockIn.Bencoded);
        }

        public virtual void SetRebondGrace(RebondGrace rebondGrace)
        {
            _rebondGraceAccount = rebondGrace.IsEmpty
                ? _rebondGraceAccount.RemoveState(rebondGrace.Address)
                : _rebondGraceAccount.SetState(rebondGrace.Address, rebondGrace.Bencoded);
        }

        public virtual void SetUnbondingSet(UnbondingSet unbondingSet)
        {
            _unbondingSetAccount = unbondingSet.IsEmpty
                ? _unbondingSetAccount.RemoveState(UnbondingSet.Address)
                : _unbondingSetAccount.SetState(UnbondingSet.Address, unbondingSet.Bencoded);
        }

        public virtual void SetLumpSumRewardsRecord(LumpSumRewardsRecord lumpSumRewardsRecord)
        {
            _lumpSumRewardsRecordAccount = _lumpSumRewardsRecordAccount.SetState(
                lumpSumRewardsRecord.Address, lumpSumRewardsRecord.Bencoded);
        }

        public virtual void AddLumpSumRewards(IDelegatee delegatee, long height, FungibleAssetValue rewards)
        {
            LumpSumRewardsRecord record = GetCurrentLumpSumRewardsRecord(delegatee)
                ?? new LumpSumRewardsRecord(
                    delegatee.CurrentLumpSumRewardsRecordAddress(),
                    height,
                    delegatee.TotalShares,
                    delegatee.RewardCurrency);
            record = record.AddLumpSumRewards(rewards);
            SetLumpSumRewardsRecord(record);
        }

        public void TransferAsset(Address sender, Address recipient, FungibleAssetValue value)
            => _world = _world.TransferAsset(_context, sender, recipient, value);
    }
}
