namespace Lib9c.Tests.Action
{
    using System;
    using System.Globalization;
    using System.Linq;
    using Bencodex.Types;
    using Libplanet.Action;
    using Libplanet.Action.State;
    using Libplanet.Crypto;
    using Libplanet.Types.Assets;
    using Nekoyume;
    using Nekoyume.Action;
    using Nekoyume.Extensions;
    using Nekoyume.Helper;
    using Nekoyume.Model;
    using Nekoyume.Model.Elemental;
    using Nekoyume.Model.Item;
    using Nekoyume.Model.Mail;
    using Nekoyume.Model.State;
    using Nekoyume.TableData.Crystal;
    using Serilog;
    using Xunit;
    using Xunit.Abstractions;
    using static Lib9c.SerializeKeys;

    public class CombinationEquipment16Test
    {
        private readonly Address _agentAddress;
        private readonly Address _avatarAddress;
        private readonly Address _slotAddress;
        private readonly TableSheets _tableSheets;
        private readonly IRandom _random;
        private readonly IAccount _initialState;
        private readonly AgentState _agentState;
        private readonly AvatarState _avatarState;

        public CombinationEquipment16Test(ITestOutputHelper outputHelper)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.TestOutput(outputHelper)
                .CreateLogger();

            _agentAddress = new PrivateKey().Address;
            _avatarAddress = _agentAddress.Derive("avatar");
            _slotAddress = _avatarAddress.Derive(
                string.Format(
                    CultureInfo.InvariantCulture,
                    CombinationSlotState.DeriveFormat,
                    0
                )
            );
            var sheets = TableSheetsImporter.ImportSheets();
            _random = new TestRandom();
            _tableSheets = new TableSheets(sheets);

            _agentState = new AgentState(_agentAddress);
            _agentState.avatarAddresses[0] = _avatarAddress;

            var gameConfigState = new GameConfigState();

            _avatarState = new AvatarState(
                _avatarAddress,
                _agentAddress,
                1,
                _tableSheets.GetAvatarSheets(),
                gameConfigState,
                default
            );

#pragma warning disable CS0618
            // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
            var gold = new GoldCurrencyState(Currency.Legacy("NCG", 2, null));
#pragma warning restore CS0618

            var combinationSlotState = new CombinationSlotState(
                _slotAddress,
                0);

            _initialState = new Account(MockState.Empty)
                .SetState(_slotAddress, combinationSlotState.Serialize())
                .SetState(GoldCurrencyState.Address, gold.Serialize());

            foreach (var (key, value) in sheets)
            {
                _initialState =
                    _initialState.SetState(Addresses.TableSheet.Derive(key), value.Serialize());
            }
        }

        [Theory]
        // Tutorial recipe.
        [InlineData(null, false, false, true, true, false, 3, 0, true, 1L, 1, null, true, false, false, false)]
        // Migration AvatarState.
        [InlineData(null, false, false, true, true, true, 3, 0, true, 1L, 1, null, true, false, false, false)]
        // SubRecipe
        [InlineData(null, true, true, true, true, false, 27, 0, true, 1L, 6, 376, true, false, false, false)]
        // 3rd sub recipe, not Mimisbrunnr Equipment.
        [InlineData(null, true, true, true, true, false, 349, 0, true, 1L, 28, 101520003, true, false, false, false)]
        // Purchase CRYSTAL.
        [InlineData(null, true, true, true, true, false, 3, 0, true, 1L, 1, null, false, false, true, false)]
        // Purchase CRYSTAL with calculate previous cost.
        [InlineData(null, true, true, true, true, false, 3, 0, true, 100_800L, 1, null, false, false, true, true)]
        // Arena round not found
        [InlineData(null, false, false, true, true, false, 3, 0, true, 0L, 1, null, true, false, false, false)]
        // UnlockEquipmentRecipe not executed.
        [InlineData(typeof(FailedLoadStateException), false, true, true, true, false, 11, 0, true, 0L, 6, 1, true, false, false, false)]
        // CRYSTAL not paid.
        [InlineData(typeof(InvalidRecipeIdException), true, false, true, true, false, 11, 0, true, 0L, 6, 1, true, false, false, false)]
        // AgentState not exist.
        [InlineData(typeof(FailedLoadStateException), true, true, false, true, false, 3, 0, true, 0L, 1, null, true, false, false, false)]
        // AvatarState not exist.
        [InlineData(typeof(FailedLoadStateException), true, true, true, false, false, 3, 0, true, 0L, 1, null, true, false, false, false)]
        [InlineData(typeof(FailedLoadStateException), true, true, true, false, true, 3, 0, true, 0L, 1, null, true, false, false, false)]
        // Tutorial not cleared.
        [InlineData(typeof(NotEnoughClearedStageLevelException), true, true, true, true, false, 1, 0, true, 0L, 1, null, true, false, false, false)]
        // CombinationSlotState not exist.
        [InlineData(typeof(FailedLoadStateException), true, true, true, true, false, 3, 5, true, 0L, 1, null, true, false, false, false)]
        // CombinationSlotState locked.
        [InlineData(typeof(CombinationSlotUnlockException), true, true, true, true, false, 3, 0, false, 0L, 1, null, true, false, false, false)]
        // Stage not cleared.
        [InlineData(typeof(NotEnoughClearedStageLevelException), true, true, true, true, false, 3, 0, true, 0L, 6, null, true, false, false, false)]
        // Not enough material.
        [InlineData(typeof(NotEnoughMaterialException), true, true, true, true, false, 3, 0, true, 0L, 1, null, false, false, false, false)]
        public void Execute(
            Type exc,
            bool unlockIdsExist,
            bool crystalUnlock,
            bool agentExist,
            bool avatarExist,
            bool migrationRequired,
            int stageId,
            int slotIndex,
            bool slotUnlock,
            long blockIndex,
            int recipeId,
            int? subRecipeId,
            bool enoughMaterial,
            bool ncgBalanceExist,
            bool payByCrystal,
            bool previousCostStateExist
        )
        {
            var context = new ActionContext();
            IAccount state = _initialState;
            if (unlockIdsExist)
            {
                var unlockIds = List.Empty.Add(1.Serialize());
                if (crystalUnlock)
                {
                    for (int i = 2; i < recipeId + 1; i++)
                    {
                        unlockIds = unlockIds.Add(i.Serialize());
                    }
                }

                state = state.SetState(_avatarAddress.Derive("recipe_ids"), unlockIds);
            }

            if (agentExist)
            {
                state = state.SetState(_agentAddress, _agentState.Serialize());

                if (avatarExist)
                {
                    _avatarState.worldInformation = new WorldInformation(
                        0,
                        _tableSheets.WorldSheet,
                        stageId);

                    if (enoughMaterial)
                    {
                        var row = _tableSheets.EquipmentItemRecipeSheet[recipeId];
                        var materialRow = _tableSheets.MaterialItemSheet[row.MaterialId];
                        var material = ItemFactory.CreateItem(materialRow, _random);
                        _avatarState.inventory.AddItem(material, row.MaterialCount);

                        if (subRecipeId.HasValue)
                        {
                            var subRow = _tableSheets.EquipmentItemSubRecipeSheetV2[subRecipeId.Value];

                            foreach (var materialInfo in subRow.Materials)
                            {
                                var subMaterial = ItemFactory.CreateItem(
                                    _tableSheets.MaterialItemSheet[materialInfo.Id], _random);
                                _avatarState.inventory.AddItem(subMaterial, materialInfo.Count);
                            }

                            if (ncgBalanceExist && subRow.RequiredGold > 0)
                            {
                                state = state.MintAsset(
                                    context,
                                    _agentAddress,
                                    subRow.RequiredGold * state.GetGoldCurrency());
                            }
                        }
                    }

                    if (migrationRequired)
                    {
                        state = state.SetState(_avatarAddress, _avatarState.Serialize());
                    }
                    else
                    {
                        var inventoryAddress = _avatarAddress.Derive(LegacyInventoryKey);
                        var worldInformationAddress =
                            _avatarAddress.Derive(LegacyWorldInformationKey);
                        var questListAddress = _avatarAddress.Derive(LegacyQuestListKey);

                        state = state
                            .SetState(_avatarAddress, _avatarState.SerializeV2())
                            .SetState(inventoryAddress, _avatarState.inventory.Serialize())
                            .SetState(
                                worldInformationAddress,
                                _avatarState.worldInformation.Serialize())
                            .SetState(questListAddress, _avatarState.questList.Serialize());
                    }

                    if (!slotUnlock)
                    {
                        // Lock slot.
                        state = state.SetState(
                            _slotAddress,
                            new CombinationSlotState(_slotAddress, stageId + 1).Serialize()
                        );
                    }
                }
            }

            int expectedCrystal = 0;
            if (payByCrystal)
            {
                var crystalBalance = 0;
                var row = _tableSheets.EquipmentItemRecipeSheet[recipeId];
                var costSheet = _tableSheets.CrystalMaterialCostSheet;
                crystalBalance += costSheet[row.MaterialId].CRYSTAL * row.MaterialCount;

                if (subRecipeId.HasValue)
                {
                    var subRow = _tableSheets.EquipmentItemSubRecipeSheetV2[subRecipeId.Value];

                    foreach (var materialInfo in subRow.Materials)
                    {
                        if (costSheet.ContainsKey(materialInfo.Id))
                        {
                            crystalBalance += costSheet[materialInfo.Id].CRYSTAL * row.MaterialCount;
                        }
                    }
                }

                if (previousCostStateExist)
                {
                    var previousCostAddress = Addresses.GetWeeklyCrystalCostAddress(6);
                    var previousCostState = new CrystalCostState(previousCostAddress, crystalBalance * CrystalCalculator.CRYSTAL * 2);
                    var beforePreviousCostAddress = Addresses.GetWeeklyCrystalCostAddress(5);
                    var beforePreviousCostState = new CrystalCostState(beforePreviousCostAddress, crystalBalance * CrystalCalculator.CRYSTAL);

                    state = state
                        .SetState(previousCostAddress, previousCostState.Serialize())
                        .SetState(beforePreviousCostAddress, beforePreviousCostState.Serialize());
                }

                expectedCrystal = crystalBalance;
                state = state.MintAsset(context, _agentAddress, expectedCrystal * CrystalCalculator.CRYSTAL);
            }

            var dailyCostAddress =
                Addresses.GetDailyCrystalCostAddress((int)(blockIndex / CrystalCostState.DailyIntervalIndex));
            var weeklyInterval = _tableSheets.CrystalFluctuationSheet.Values.First(r =>
                r.Type == CrystalFluctuationSheet.ServiceType.Combination).BlockInterval;
            var weeklyCostAddress = Addresses.GetWeeklyCrystalCostAddress((int)(blockIndex / weeklyInterval));

            Assert.Null(state.GetState(dailyCostAddress));
            Assert.Null(state.GetState(weeklyCostAddress));

            var action = new CombinationEquipment16
            {
                avatarAddress = _avatarAddress,
                slotIndex = slotIndex,
                recipeId = recipeId,
                subRecipeId = subRecipeId,
                payByCrystal = payByCrystal,
                useHammerPoint = false,
            };

            if (exc is null)
            {
                var nextState = action.Execute(new ActionContext
                {
                    PreviousState = state,
                    Signer = _agentAddress,
                    BlockIndex = blockIndex,
                    RandomSeed = _random.Seed,
                });

                var currency = nextState.GetGoldCurrency();
                Assert.Equal(0 * currency, nextState.GetBalance(_agentAddress, currency));

                var slotState = nextState.GetCombinationSlotState(_avatarAddress, 0);
                Assert.NotNull(slotState.Result);
                Assert.NotNull(slotState.Result.itemUsable);

                var equipment = (Equipment)slotState.Result.itemUsable;
                if (subRecipeId.HasValue)
                {
                    Assert.True(equipment.optionCountFromCombination > 0);

                    if (ncgBalanceExist)
                    {
                        var arenaSheet = _tableSheets.ArenaSheet;
                        var arenaData = arenaSheet.GetRoundByBlockIndex(blockIndex);
                        var feeStoreAddress = Addresses.GetBlacksmithFeeAddress(arenaData.ChampionshipId, arenaData.Round);
                        Assert.Equal(450 * currency, nextState.GetBalance(feeStoreAddress, currency));
                    }
                }
                else
                {
                    Assert.Equal(0, equipment.optionCountFromCombination);
                }

                var nextAvatarState = nextState.GetAvatarStateV2(_avatarAddress);
                var mail = nextAvatarState.mailBox.OfType<CombinationMail>().First();

                Assert.Equal(equipment, mail.attachment.itemUsable);
                Assert.Equal(payByCrystal, !(nextState.GetState(dailyCostAddress) is null));
                Assert.Equal(payByCrystal, !(nextState.GetState(weeklyCostAddress) is null));

                if (payByCrystal)
                {
                    var dailyCostState = nextState.GetCrystalCostState(dailyCostAddress);
                    var weeklyCostState = nextState.GetCrystalCostState(weeklyCostAddress);

                    Assert.Equal(0 * CrystalCalculator.CRYSTAL, nextState.GetBalance(_agentAddress, CrystalCalculator.CRYSTAL));
                    Assert.Equal(1, dailyCostState.Count);
                    Assert.Equal(expectedCrystal * CrystalCalculator.CRYSTAL, dailyCostState.CRYSTAL);
                    Assert.Equal(1, weeklyCostState.Count);
                    Assert.Equal(expectedCrystal * CrystalCalculator.CRYSTAL, weeklyCostState.CRYSTAL);
                }

                Assert.Equal(expectedCrystal * CrystalCalculator.CRYSTAL, nextState.GetBalance(Addresses.MaterialCost, CrystalCalculator.CRYSTAL));
            }
            else
            {
                Assert.Throws(exc, () => action.Execute(new ActionContext
                {
                    PreviousState = state,
                    Signer = _agentAddress,
                    BlockIndex = blockIndex,
                    RandomSeed = _random.Seed,
                }));
            }
        }

        [Theory]
        [InlineData(null, false, 1, 1)]
        [InlineData(null, false, 0, 1)]
        [InlineData(typeof(NotEnoughFungibleAssetValueException), true, 1, 1)]
        [InlineData(null, true, 1, 1)]
        [InlineData(typeof(NotEnoughHammerPointException), true, 1, 1)]
        public void ExecuteWithCheckingHammerPointState(
            Type exc,
            bool doSuperCraft,
            int subRecipeIndex,
            int recipeId)
        {
            var context = new ActionContext();
            IAccount state = _initialState;
            var unlockIds = List.Empty.Add(1.Serialize());
            for (int i = 2; i < recipeId + 1; i++)
            {
                unlockIds = unlockIds.Add(i.Serialize());
            }

            state = state.SetState(_avatarAddress.Derive("recipe_ids"), unlockIds);
            state = state.SetState(_agentAddress, _agentState.Serialize());
            _avatarState.worldInformation = new WorldInformation(0, _tableSheets.WorldSheet, 200);
            var row = _tableSheets.EquipmentItemRecipeSheet[recipeId];
            var materialRow = _tableSheets.MaterialItemSheet[row.MaterialId];
            var material = ItemFactory.CreateItem(materialRow, _random);
            _avatarState.inventory.AddItem(material, row.MaterialCount);
            int? subRecipeId = row.SubRecipeIds[subRecipeIndex];
            if (exc?.FullName?.Contains(nameof(ArgumentException)) ?? false)
            {
                subRecipeId = row.SubRecipeIds.Last();
            }

            var subRow = _tableSheets.EquipmentItemSubRecipeSheetV2[subRecipeId.Value];
            foreach (var materialInfo in subRow.Materials)
            {
                var subMaterial = ItemFactory.CreateItem(
                    _tableSheets.MaterialItemSheet[materialInfo.Id], _random);
                _avatarState.inventory.AddItem(subMaterial, materialInfo.Count);
            }

            if (subRow.RequiredGold > 0)
            {
                state = state.MintAsset(
                    context,
                    _agentAddress,
                    subRow.RequiredGold * state.GetGoldCurrency());
            }

            var inventoryAddress = _avatarAddress.Derive(LegacyInventoryKey);
            var worldInformationAddress =
                _avatarAddress.Derive(LegacyWorldInformationKey);
            var questListAddress = _avatarAddress.Derive(LegacyQuestListKey);
            state = state
                .SetState(_avatarAddress, _avatarState.SerializeV2())
                .SetState(inventoryAddress, _avatarState.inventory.Serialize())
                .SetState(
                    worldInformationAddress,
                    _avatarState.worldInformation.Serialize())
                .SetState(questListAddress, _avatarState.questList.Serialize());
            var hammerPointAddress =
                Addresses.GetHammerPointStateAddress(_avatarAddress, recipeId);
            if (doSuperCraft)
            {
                var hammerPointState = new HammerPointState(hammerPointAddress, recipeId);
                var hammerPointSheet = _tableSheets.CrystalHammerPointSheet;
                hammerPointState.AddHammerPoint(
                    hammerPointSheet[recipeId].MaxPoint,
                    hammerPointSheet);
                state = state.SetState(hammerPointAddress, hammerPointState.Serialize());
                if (exc is null)
                {
                    var costCrystal = CrystalCalculator.CRYSTAL *
                                      hammerPointSheet[recipeId].CRYSTAL;
                    state = state.MintAsset(
                        context,
                        _agentAddress,
                        costCrystal);
                }
                else if (exc.FullName!.Contains(nameof(NotEnoughHammerPointException)))
                {
                    hammerPointState.ResetHammerPoint();
                    state = state.SetState(hammerPointAddress, hammerPointState.Serialize());
                }
            }

            var action = new CombinationEquipment16
            {
                avatarAddress = _avatarAddress,
                slotIndex = 0,
                recipeId = recipeId,
                subRecipeId = subRecipeId,
                payByCrystal = false,
                useHammerPoint = doSuperCraft,
            };
            if (exc is null)
            {
                var nextState = action.Execute(new ActionContext
                {
                    PreviousState = state,
                    Signer = _agentAddress,
                    BlockIndex = 1,
                    RandomSeed = _random.Seed,
                });

                Assert.True(nextState.TryGetState(hammerPointAddress, out List serialized));
                var hammerPointState =
                    new HammerPointState(hammerPointAddress, serialized);
                if (!doSuperCraft)
                {
                    Assert.Equal(subRow.RewardHammerPoint, hammerPointState.HammerPoint);
                }
                else
                {
                    Assert.Equal(0, hammerPointState.HammerPoint);
                    var slotState = nextState.GetCombinationSlotState(_avatarAddress, 0);
                    Assert.NotNull(slotState.Result);
                    Assert.NotNull(slotState.Result.itemUsable);
                    Assert.NotEmpty(slotState.Result.itemUsable.Skills);
                }
            }
            else
            {
                Assert.Throws(exc, () =>
                {
                    action.Execute(new ActionContext
                    {
                        PreviousState = state,
                        Signer = _agentAddress,
                        BlockIndex = 1,
                        RandomSeed = _random.Seed,
                    });
                });
            }
        }

        [Fact]
        public void AddAndUnlockOption()
        {
            var subRecipe = _tableSheets.EquipmentItemSubRecipeSheetV2.Last;
            Assert.NotNull(subRecipe);
            var equipment = (Necklace)ItemFactory.CreateItemUsable(
                _tableSheets.EquipmentItemSheet[10411000],
                Guid.NewGuid(),
                default);
            Assert.Equal(0, equipment.optionCountFromCombination);
            CombinationEquipment16.AddAndUnlockOption(
                _agentState,
                null,
                equipment,
                _random,
                subRecipe,
                _tableSheets.EquipmentItemOptionSheet,
                _tableSheets.PetOptionSheet,
                _tableSheets.SkillSheet
            );
            Assert.True(equipment.optionCountFromCombination > 0);
        }
    }
}