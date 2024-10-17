namespace Lib9c.Tests.Action.ValidatorDelegation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Nekoyume.Action;
using Nekoyume.Action.ValidatorDelegation;
using Nekoyume.ValidatorDelegation;
using Xunit;

public class UndelegateValidatorTest : ValidatorDelegationTestBase
{
    private interface IUndelegateValidatorFixture
    {
        ValidatorInfo ValidatorInfo { get; }

        DelegatorInfo[] DelegatorInfos { get; }
    }

    public static IEnumerable<object[]> RandomSeeds => new List<object[]>
    {
        new object[] { Random.Shared.Next() },
        new object[] { Random.Shared.Next() },
        new object[] { Random.Shared.Next() },
        new object[] { Random.Shared.Next() },
        new object[] { Random.Shared.Next() },
        new object[] { Random.Shared.Next() },
    };

    [Fact]
    public void Serialization()
    {
        var address = new PrivateKey().Address;
        var share = BigInteger.One;
        var action = new UndelegateValidator(address, share);
        var plainValue = action.PlainValue;

        var deserialized = new UndelegateValidator();
        deserialized.LoadPlainValue(plainValue);
        Assert.Equal(address, deserialized.ValidatorDelegatee);
        Assert.Equal(share, deserialized.Share);
    }

    [Fact]
    public void Execute()
    {
        // Given
        var world = World;
        var actionContext = new ActionContext { };
        var validatorKey = new PrivateKey();
        var height = 1L;
        var validatorGold = GG * 10;
        world = EnsureToMintAsset(world, validatorKey, GG * 100, height++);
        world = EnsurePromotedValidator(world, validatorKey, validatorGold, height++);

        // When
        var expectedRepository = new ValidatorRepository(world, actionContext);
        var expectedDelegatee = expectedRepository.GetValidatorDelegatee(validatorKey.Address);
        var expectedBond = expectedRepository.GetBond(expectedDelegatee, validatorKey.Address);
        var undelegateValidator = new UndelegateValidator(validatorKey.Address, expectedBond.Share);
        actionContext = new ActionContext
        {
            PreviousState = world,
            Signer = validatorKey.Address,
            BlockIndex = height++,
        };

        world = undelegateValidator.Execute(actionContext);

        var actualRepository = new ValidatorRepository(world, actionContext);
        var actualDelegatee = actualRepository.GetValidatorDelegatee(validatorKey.Address);
        var actualValidatorList = actualRepository.GetValidatorList();
        var actualBond = actualRepository.GetBond(actualDelegatee, validatorKey.Address);

        Assert.NotEqual(expectedDelegatee.Delegators, actualDelegatee.Delegators);
        Assert.NotEqual(expectedDelegatee.Validator.Power, actualDelegatee.Validator.Power);
        Assert.Equal(BigInteger.Zero, actualDelegatee.Validator.Power);
        Assert.Empty(actualValidatorList.Validators);
        Assert.NotEqual(expectedBond.Share, actualBond.Share);
        Assert.Equal(BigInteger.Zero, actualBond.Share);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(9)]
    public void Execute_Theory(int delegatorCount)
    {
        var fixture = new StaticFixture
        {
            ValidatorInfo = new ValidatorInfo
            {
                Key = new PrivateKey(),
                Cash = GG * 10,
                Balance = GG * 100,
                SubtractShare = 10,
            },
            DelegatorInfos = Enumerable.Range(0, delegatorCount)
                .Select(_ => new DelegatorInfo
                {
                    Key = new PrivateKey(),
                    Cash = GG * 20,
                    Balance = GG * 100,
                    SubtractShare = 20,
                })
                .ToArray(),
        };
        ExecuteWithFixture(fixture);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1181126949)]
    [InlineData(793705868)]
    [InlineData(17046502)]
    public void Execute_Theory_WithStaticSeed(int randomSeed)
    {
        var fixture = new RandomFixture(randomSeed);
        ExecuteWithFixture(fixture);
    }

    [Theory]
    [MemberData(nameof(RandomSeeds))]
    public void Execute_Theory_WithRandomSeed(int randomSeed)
    {
        var fixture = new RandomFixture(randomSeed);
        ExecuteWithFixture(fixture);
    }

    [Fact]
    public void Execute_FromInvalidValidtor_Throw()
    {
        // Given
        var world = World;
        var delegatorKey = new PrivateKey();
        var validatorKey = new PrivateKey();
        var height = 1L;
        world = EnsureToMintAsset(world, validatorKey, GG * 10, height++);
        world = EnsurePromotedValidator(world, validatorKey, GG * 10, height++);
        world = EnsureToMintAsset(world, delegatorKey, GG * 10, height++);
        world = EnsureBondedDelegator(world, delegatorKey, validatorKey, GG * 10, height++);

        // When
        var actionContext = new ActionContext
        {
            PreviousState = world,
            Signer = delegatorKey.Address,
            BlockIndex = height++,
        };
        var undelegateValidator = new UndelegateValidator(new PrivateKey().Address, 10);

        // Then
        Assert.Throws<FailedLoadStateException>(
            () => undelegateValidator.Execute(actionContext));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Execute_WithNotPositiveShare_Throw(long share)
    {
        // Given
        var world = World;
        var delegatorKey = new PrivateKey();
        var validatorKey = new PrivateKey();
        var height = 1L;
        world = EnsureToMintAsset(world, validatorKey, GG * 10, height++);
        world = EnsurePromotedValidator(world, validatorKey, GG * 10, height++);
        world = EnsureToMintAsset(world, delegatorKey, GG * 10, height++);
        world = EnsureBondedDelegator(world, delegatorKey, validatorKey, GG * 10, height++);

        // When
        var actionContext = new ActionContext
        {
            PreviousState = world,
            Signer = delegatorKey.Address,
            BlockIndex = height++,
        };
        var undelegateValidator = new UndelegateValidator(validatorKey.Address, share);

        // Then
        Assert.Throws<ArgumentOutOfRangeException>(
            () => undelegateValidator.Execute(actionContext));
    }

    [Fact]
    public void Execute_WithoutDelegating_Throw()
    {
        // Given
        var world = World;
        var delegatorKey = new PrivateKey();
        var validatorKey = new PrivateKey();
        var height = 1L;
        world = EnsureToMintAsset(world, validatorKey, GG * 10, height++);
        world = EnsurePromotedValidator(world, validatorKey, GG * 10, height++);

        // When
        var actionContext = new ActionContext
        {
            PreviousState = world,
            Signer = delegatorKey.Address,
            BlockIndex = height++,
        };
        var undelegateValidator = new UndelegateValidator(
            validatorKey.Address, 10);

        // Then
        Assert.Throws<ArgumentOutOfRangeException>(
            () => undelegateValidator.Execute(actionContext));
    }

    [Fact]
    public void Execute_FromJailedValidator()
    {
        // Given
        var world = World;
        var delegatorKey = new PrivateKey();
        var validatorKey = new PrivateKey();
        var height = 1L;
        world = EnsureToMintAsset(world, validatorKey, GG * 10, height++);
        world = EnsurePromotedValidator(world, validatorKey, GG * 10, height++);
        world = EnsureToMintAsset(world, delegatorKey, GG * 10, height++);
        world = EnsureBondedDelegator(world, delegatorKey, validatorKey, GG * 10, height++);
        world = EnsureJailedValidator(world, validatorKey, ref height);

        // When
        var actionContext = new ActionContext
        {
            PreviousState = world,
            Signer = delegatorKey.Address,
            BlockIndex = height,
        };
        var expectedRepository = new ValidatorRepository(world, actionContext);
        var expectedDelegatee = expectedRepository.GetValidatorDelegatee(validatorKey.Address);
        var expectedBond = expectedRepository.GetBond(expectedDelegatee, delegatorKey.Address);

        var undelegateValidator = new UndelegateValidator(validatorKey.Address, 10);
        world = undelegateValidator.Execute(actionContext);

        // Then
        var actualRepository = new ValidatorRepository(world, actionContext);
        var actualDelegatee = actualRepository.GetValidatorDelegatee(validatorKey.Address);
        var actualBond = actualRepository.GetBond(actualDelegatee, delegatorKey.Address);

        Assert.Equal(expectedBond.Share - 10, actualBond.Share);
    }

    [Fact]
    public void Execute_FromTombstonedValidator()
    {
        // Given
        var world = World;
        var delegatorKey = new PrivateKey();
        var validatorKey = new PrivateKey();
        var height = 1L;
        world = EnsureToMintAsset(world, validatorKey, GG * 10, height++);
        world = EnsurePromotedValidator(world, validatorKey, GG * 10, height++);
        world = EnsureToMintAsset(world, delegatorKey, GG * 10, height++);
        world = EnsureBondedDelegator(world, delegatorKey, validatorKey, GG * 10, height++);
        world = EnsureTombstonedValidator(world, validatorKey, height++);

        // When
        var actionContext = new ActionContext
        {
            PreviousState = world,
            Signer = delegatorKey.Address,
            BlockIndex = height,
        };
        var expectedRepository = new ValidatorRepository(world, actionContext);
        var expectedDelegatee = expectedRepository.GetValidatorDelegatee(validatorKey.Address);
        var expectedBond = expectedRepository.GetBond(expectedDelegatee, delegatorKey.Address);

        var undelegateValidator = new UndelegateValidator(validatorKey.Address, 10);
        world = undelegateValidator.Execute(actionContext);

        // Then
        var actualRepository = new ValidatorRepository(world, actionContext);
        var actualDelegatee = actualRepository.GetValidatorDelegatee(validatorKey.Address);
        var actualBond = actualRepository.GetBond(actualDelegatee, delegatorKey.Address);

        Assert.Equal(expectedBond.Share - 10, actualBond.Share);
    }

    [Fact]
    public void Execute_CannotBeJailedDueToDelegatorUndelegating()
    {
        // Given
        var world = World;
        var validatorKey = new PrivateKey();
        var delegatorKey = new PrivateKey();
        var validatorCash = GG * 10;
        var validatorGold = GG * 100;
        var delegatorGold = GG * 10;
        var actionContext = new ActionContext { };
        var height = 1L;
        world = EnsureToMintAsset(world, validatorKey, validatorGold, height++);
        world = EnsurePromotedValidator(world, validatorKey, validatorCash, height++);
        world = EnsureUnbondingDelegator(world, validatorKey, validatorKey, 10, height++);
        world = EnsureBondedDelegator(world, validatorKey, validatorKey, validatorCash, height++);

        world = EnsureToMintAsset(world, delegatorKey, delegatorGold, height++);
        world = EnsureBondedDelegator(world, delegatorKey, validatorKey, delegatorGold, height++);
        world = EnsureUnjailedValidator(world, validatorKey, ref height);

        // When
        var expectedRepository = new ValidatorRepository(world, actionContext);
        var expectedDelegatee = expectedRepository.GetValidatorDelegatee(validatorKey.Address);
        var expectedJailed = expectedDelegatee.Jailed;

        var undelegateValidator = new UndelegateValidator(validatorKey.Address, 10);
        actionContext = new ActionContext
        {
            PreviousState = world,
            Signer = delegatorKey.Address,
            BlockIndex = height,
        };
        world = undelegateValidator.Execute(actionContext);

        // Then
        var actualRepository = new ValidatorRepository(world, actionContext);
        var actualDelegatee = actualRepository.GetValidatorDelegatee(validatorKey.Address);
        var actualJailed = actualDelegatee.Jailed;

        Assert.False(actualJailed);
        Assert.Equal(expectedJailed, actualJailed);
    }

    [Fact]
    public void Execute_JailsValidatorWhenUndelegationCausesLowDelegation()
    {
        // Given
        var world = World;
        var validatorKey = new PrivateKey();
        var validatorCash = MinimumDelegation;
        var validatorGold = MinimumDelegation;
        var actionContext = new ActionContext { };
        var height = 1L;
        world = EnsureToMintAsset(world, validatorKey, validatorGold, height++);
        world = EnsurePromotedValidator(world, validatorKey, validatorCash, height++);

        // When
        var expectedRepository = new ValidatorRepository(world, actionContext);
        var expectedDelegatee = expectedRepository.GetValidatorDelegatee(validatorKey.Address);
        var expectedJailed = expectedDelegatee.Jailed;

        var undelegateValidator = new UndelegateValidator(validatorKey.Address, 10);
        actionContext = new ActionContext
        {
            PreviousState = world,
            Signer = validatorKey.Address,
            BlockIndex = height,
        };
        world = undelegateValidator.Execute(actionContext);

        // Then
        var actualRepository = new ValidatorRepository(world, actionContext);
        var actualDelegatee = actualRepository.GetValidatorDelegatee(validatorKey.Address);
        var actualJailed = actualDelegatee.Jailed;
        var actualJailedUntil = actualDelegatee.JailedUntil;

        Assert.True(actualJailed);
        Assert.NotEqual(expectedJailed, actualJailed);
    }

    private void ExecuteWithFixture(IUndelegateValidatorFixture fixture)
    {
        // Given
        var world = World;
        var validatorKey = fixture.ValidatorInfo.Key;
        var height = 1L;
        var validatorCash = fixture.ValidatorInfo.Cash;
        var validatorBalance = fixture.ValidatorInfo.Balance;
        var delegatorKeys = fixture.DelegatorInfos.Select(i => i.Key).ToArray();
        var delegatorCashes = fixture.DelegatorInfos.Select(i => i.Cash).ToArray();
        var delegatorBalances = fixture.DelegatorInfos.Select(i => i.Balance).ToArray();
        var delegatorSubtractShares = fixture.DelegatorInfos.Select(i => i.SubtractShare).ToArray();
        var actionContext = new ActionContext { };
        world = EnsureToMintAsset(world, validatorKey, validatorBalance, height++);
        world = EnsurePromotedValidator(world, validatorKey, validatorCash, height++);
        world = EnsureToMintAssets(world, delegatorKeys, delegatorBalances, height++);
        world = EnsureBondedDelegators(
            world, delegatorKeys, validatorKey, delegatorCashes, height++);

        // When
        var expectedRepository = new ValidatorRepository(world, new ActionContext());
        var expectedValidator = expectedRepository.GetValidatorDelegatee(validatorKey.Address);
        var expectedValidatorBalance = validatorBalance - validatorCash;
        var expectedDelegatorBalances = delegatorKeys
            .Select(k => world.GetBalance(k.Address, GG)).ToArray();
        var expectedShares = delegatorCashes
            .Select((c, i) => c.RawValue - delegatorSubtractShares[i]).ToArray();
        var expectedPower = expectedValidator.Power - delegatorSubtractShares.Aggregate(
            BigInteger.Zero, (a, b) => a + b);

        for (var i = 0; i < delegatorKeys.Length; i++)
        {
            var subtractShare = fixture.DelegatorInfos[i].SubtractShare;
            var undelegateValidator = new UndelegateValidator(
                validatorKey.Address, subtractShare);
            actionContext = new ActionContext
            {
                PreviousState = world,
                Signer = delegatorKeys[i].Address,
                BlockIndex = height++,
            };
            world = undelegateValidator.Execute(actionContext);
        }

        // Then
        var actualRepository = new ValidatorRepository(world, actionContext);
        var actualValidator = actualRepository.GetValidatorDelegatee(validatorKey.Address);
        var actualValidatorBalance = world.GetBalance(validatorKey.Address, GG);
        var actualDelegatorBalances = delegatorKeys
            .Select(k => world.GetBalance(k.Address, GG)).ToArray();
        var actualPower = actualValidator.Power;

        for (var i = 0; i < delegatorKeys.Length; i++)
        {
            var actualBond = actualRepository.GetBond(actualValidator, delegatorKeys[i].Address);
            if (actualBond.Share == 0)
            {
                Assert.DoesNotContain(delegatorKeys[i].Address, actualValidator.Delegators);
            }
            else
            {
                Assert.Contains(delegatorKeys[i].Address, actualValidator.Delegators);
            }

            Assert.Equal(expectedShares[i], actualBond.Share);
            Assert.Equal(expectedDelegatorBalances[i], actualDelegatorBalances[i]);
        }

        Assert.Equal(expectedValidatorBalance, actualValidatorBalance);
        Assert.Equal(expectedPower, actualPower);
        Assert.Equal(expectedDelegatorBalances, actualDelegatorBalances);
    }

    private struct ValidatorInfo
    {
        public ValidatorInfo()
        {
        }

        public ValidatorInfo(Random random)
        {
            Balance = GetRandomGG(random);
            Cash = GetRandomCash(random, Balance);
            SubtractShare = GetRandomCash(random, Cash).RawValue;
            Assert.True(SubtractShare > 0);
        }

        public PrivateKey Key { get; set; } = new PrivateKey();

        public FungibleAssetValue Cash { get; set; } = GG * 10;

        public FungibleAssetValue Balance { get; set; } = GG * 100;

        public BigInteger SubtractShare { get; set; } = 100;
    }

    private struct DelegatorInfo
    {
        public DelegatorInfo()
        {
        }

        public DelegatorInfo(Random random)
        {
            Balance = GetRandomGG(random);
            Cash = GetRandomCash(random, Balance);
            SubtractShare = GetRandomCash(random, Cash).RawValue;
        }

        public PrivateKey Key { get; set; } = new PrivateKey();

        public FungibleAssetValue Cash { get; set; } = GG * 10;

        public FungibleAssetValue Balance { get; set; } = GG * 100;

        public BigInteger SubtractShare { get; set; } = 100;
    }

    private struct StaticFixture : IUndelegateValidatorFixture
    {
        public ValidatorInfo ValidatorInfo { get; set; }

        public DelegatorInfo[] DelegatorInfos { get; set; }
    }

    private class RandomFixture : IUndelegateValidatorFixture
    {
        private readonly Random _random;

        public RandomFixture(int randomSeed)
        {
            _random = new Random(randomSeed);
            ValidatorInfo = new ValidatorInfo(_random);
            DelegatorInfos = Enumerable.Range(0, _random.Next(1, 10))
                .Select(_ => new DelegatorInfo(_random))
                .ToArray();
        }

        public ValidatorInfo ValidatorInfo { get; }

        public DelegatorInfo[] DelegatorInfos { get; }
    }
}
