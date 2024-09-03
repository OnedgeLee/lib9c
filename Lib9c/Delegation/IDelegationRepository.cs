#nullable enable
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Types.Assets;

namespace Nekoyume.Delegation
{
    public interface IDelegationRepository
    {
        IWorld World { get; }

        IDelegatee GetDelegatee(Address address, Address accountAddress);

        IDelegator GetDelegator(Address address, Address accountAddress);

        DelegateeMetadata GetDelegateeMetadata(Address address, Address accountAddress);

        DelegatorMetadata GetDelegatorMetadata(Address address, Address accountAddress);

        Bond GetBond(IDelegatee delegatee, Address delegatorAddress);

        UnbondLockIn GetUnbondLockIn(IDelegatee delegatee, Address delegatorAddress);

        UnbondLockIn GetUnlimitedUnbondLockIn(Address address);

        RebondGrace GetRebondGrace(IDelegatee delegatee, Address delegatorAddress);

        RebondGrace GetUnlimitedRebondGrace(Address address);

        UnbondingSet GetUnbondingSet();

        LumpSumRewardsRecord? GetLumpSumRewardsRecord(IDelegatee delegatee, long height);

        LumpSumRewardsRecord? GetCurrentLumpSumRewardsRecord(IDelegatee delegatee);

        FungibleAssetValue GetBalance(Address address, Currency currency);

        void SetBond(Bond bond);

        void SetUnbondLockIn(UnbondLockIn unbondLockIn);

        void SetRebondGrace(RebondGrace rebondGrace);

        void SetUnbondingSet(UnbondingSet unbondingSet);

        void SetLumpSumRewardsRecord(LumpSumRewardsRecord lumpSumRewardsRecord);

        void AddLumpSumRewards(IDelegatee delegatee, long height, FungibleAssetValue rewards);

        void TransferAsset(Address sender, Address recipient, FungibleAssetValue value);
    }
}
