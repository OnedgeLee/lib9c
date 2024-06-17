﻿namespace Lib9c.Tests.Action.DPoS
{
    using System;
    using System.Collections.Immutable;
    using System.Linq;
    using Libplanet.Action.State;
    using Libplanet.Crypto;
    using Libplanet.Types.Blocks;
    using Libplanet.Types.Consensus;
    using Nekoyume.Action.DPoS;
    using Nekoyume.Action.DPoS.Misc;
    using Nekoyume.Action.DPoS.Sys;
    using Nekoyume.Model.State;
    using Nekoyume.Module;
    using Xunit;

    public class WithdrawDelegatorTest : PoSTest
    {
        [Fact]
        public void Execute()
        {
            var validatorPrivateKey = new PrivateKey();
            var address = validatorPrivateKey.Address;
            var validatorAddress =
                Nekoyume.Action.DPoS.Model.Validator.DeriveAddress(address);
            IWorld states = InitialState;
            var rewardAddress =
                Nekoyume.Action.DPoS.Model.ValidatorRewards.DeriveAddress(
                    validatorAddress,
                    GovernanceToken);
            var amount = GovernanceToken * 100;
            states = states.TransferAsset(
                new ActionContext(),
                GoldCurrencyState.Address,
                address,
                amount);
            states = new PromoteValidator(validatorPrivateKey.PublicKey, amount: amount).Execute(
                new ActionContext { PreviousState = states, Signer = address });
            states = new UpdateValidators().Execute(new ActionContext { PreviousState = states });
            states = new RecordProposer().Execute(
                new ActionContext { PreviousState = states, Miner = address });
            var bytes = new byte[32];
            new Random().NextBytes(bytes);
            var blockHash = new BlockHash(bytes);
            var power = states.GetValidatorSet()
                .GetValidatorsPower(
                    new[] { validatorPrivateKey.PublicKey }.ToList());
            Assert.Equal(10000, power);

            // Mint and allocate rewards
            states = states.TransferAsset(
                new ActionContext(),
                GoldCurrencyState.Address,
                ReservedAddress.RewardPool,
                GovernanceToken * 5);
            states = new AllocateReward().Execute(
                new ActionContext
                {
                    PreviousState = states,
                    BlockIndex = 1,
                    LastCommit = new BlockCommit(
                        0,
                        0,
                        blockHash,
                        new[]
                        {
                            new VoteMetadata(
                                0,
                                0,
                                blockHash,
                                DateTimeOffset.UtcNow,
                                validatorPrivateKey.PublicKey,
                                power,
                                VoteFlag.PreCommit).Sign(validatorPrivateKey),
                        }.ToImmutableArray()),
                });
            Assert.Equal(
                0,
                states.GetBalance(address, GovernanceToken).RawValue);
            Assert.Equal(
                428,
                states.GetBalance(rewardAddress, GovernanceToken).RawValue);
            states = new WithdrawDelegator(validatorAddress).Execute(
                new ActionContext
                    { PreviousState = states, BlockIndex = 2, Signer = address });
            Assert.Equal(
                5 * GovernanceToken,
                states.GetBalance(address, GovernanceToken));
            Assert.Equal(
                0,
                states.GetBalance(rewardAddress, GovernanceToken).RawValue);
        }
    }
}
