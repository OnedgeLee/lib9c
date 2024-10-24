using Libplanet.Action.State;
using Libplanet.Action;
using Libplanet.Crypto;
using Nekoyume.Action;
using Nekoyume.Delegation;

namespace Nekoyume.Model.Guild
{
    public class GuildValidatorRepository : DelegationRepository
    {
        public GuildValidatorRepository(IWorld world, IActionContext actionContext)
            : base(
                  world: world,
                  actionContext: actionContext,
                  delegateeAccountAddress: Addresses.Guild,
                  delegatorAccountAddress: Addresses.GuildParticipant,
                  delegateeMetadataAccountAddress: Addresses.GuildMetadata,
                  delegatorMetadataAccountAddress: Addresses.GuildParticipantMetadata,
                  bondAccountAddress: Addresses.GuildBond,
                  unbondLockInAccountAddress: Addresses.GuildUnbondLockIn,
                  rebondGraceAccountAddress: Addresses.GuildRebondGrace,
                  unbondingSetAccountAddress: Addresses.GuildUnbondingSet,
                  lumpSumRewardRecordAccountAddress: Addresses.GuildLumpSumRewardsRecord)
        {
        }

        public GuildValidatorDelegatee GetGuildValidatorDelegatee(Address address)
        {
            try
            {
                return new GuildValidatorDelegatee(address, this);
            }
            catch (FailedLoadStateException)
            {
                return new GuildValidatorDelegatee(address, address, this);
            }
        }

        public override IDelegatee GetDelegatee(Address address)
            => GetGuildValidatorDelegatee(address);


        public GuildValidatorDelegator GetGuildValidatorDelegator(Address address)
        {
            try
            {
                return new GuildValidatorDelegator(address, this);
            }
            catch (FailedLoadStateException)
            {
                    return new GuildValidatorDelegator(address, address, this);
            }
        }

        public override IDelegator GetDelegator(Address address)
            => GetGuildValidatorDelegator(address);

        public void SetGuildValidatorDelegatee(GuildValidatorDelegatee guildValidatorDelegatee)
        {
            SetDelegateeMetadata(guildValidatorDelegatee.Metadata);
        }

        public override void SetDelegatee(IDelegatee delegatee)
            => SetGuildValidatorDelegatee(delegatee as GuildValidatorDelegatee);

        public void SetGuildValidatorDelegator(GuildValidatorDelegator guildValidatorDelegator)
        {
            SetDelegatorMetadata(guildValidatorDelegator.Metadata);
        }

        public override void SetDelegator(IDelegator delegator)
            => SetGuildValidatorDelegator(delegator as GuildValidatorDelegator);
    }
}