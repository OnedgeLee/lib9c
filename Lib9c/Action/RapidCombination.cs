using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using Bencodex.Types;
using Lib9c.Abstractions;
using Libplanet;
using Libplanet.Action;
using Nekoyume.Extensions;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using Nekoyume.TableData.Pet;
using Serilog;
using static Lib9c.SerializeKeys;

namespace Nekoyume.Action
{
    /// <summary>
    /// Hard forked at https://github.com/planetarium/lib9c/pull/
    /// </summary>
    [Serializable]
    [ActionType("rapid_combination9")]
    public class RapidCombination : GameAction, IRapidCombinationV1
    {
        public Address avatarAddress;
        public int slotIndex;

        public Address AvatarAddress => avatarAddress;
        public int SlotIndex => slotIndex;

        public override IAccountStateDelta Execute(IActionContext context)
        {
            var states = context.PreviousStates;
            var slotAddress = avatarAddress.Derive(
                string.Format(
                    CultureInfo.InvariantCulture,
                    CombinationSlotState.DeriveFormat,
                    slotIndex
                )
            );
            var inventoryAddress = avatarAddress.Derive(LegacyInventoryKey);
            var worldInformationAddress = avatarAddress.Derive(LegacyWorldInformationKey);
            var questListAddress = avatarAddress.Derive(LegacyQuestListKey);
            if (context.Rehearsal)
            {
                return states
                    .SetState(avatarAddress, MarkChanged)
                    .SetState(inventoryAddress, MarkChanged)
                    .SetState(worldInformationAddress, MarkChanged)
                    .SetState(questListAddress, MarkChanged)
                    .SetState(slotAddress, MarkChanged);
            }

            var addressesHex = GetSignerAndOtherAddressesHex(context, avatarAddress);
            var started = DateTimeOffset.UtcNow;
            Log.Debug("{AddressesHex}RapidCombination exec started", addressesHex);

            if (!states.TryGetAgentAvatarStatesV2(
                context.Signer,
                avatarAddress,
                out var agentState,
                out var avatarState,
                out _))
            {
                throw new FailedLoadStateException($"{addressesHex}Aborted as the avatar state of the signer was failed to load.");
            }

            var slotState = states.GetCombinationSlotState(avatarAddress, slotIndex);
            if (slotState?.Result is null)
            {
                throw new CombinationSlotResultNullException($"{addressesHex}CombinationSlot Result is null. ({avatarAddress}), ({slotIndex})");
            }

            if(!avatarState.worldInformation.IsStageCleared(slotState.UnlockStage))
            {
                avatarState.worldInformation.TryGetLastClearedStageId(out var current);
                throw new NotEnoughClearedStageLevelException(addressesHex, slotState.UnlockStage, current);
            }

            var diff = slotState.Result.itemUsable.RequiredBlockIndex - context.BlockIndex;
            if (diff <= 0)
            {
                throw new RequiredBlockIndexException($"{addressesHex}Already met the required block index. context block index: {context.BlockIndex}, required block index: {slotState.Result.itemUsable.RequiredBlockIndex}");
            }

            var gameConfigState = states.GetGameConfigState();
            if (gameConfigState is null)
            {
                throw new FailedLoadStateException($"{addressesHex}Aborted as the GameConfigState was failed to load.");
            }

            var actionableBlockIndex = slotState.StartBlockIndex +
                                       states.GetGameConfigState().RequiredAppraiseBlock;
            if (context.BlockIndex < actionableBlockIndex)
            {
                throw new AppraiseBlockNotReachedException(
                    $"{addressesHex}Aborted as Item appraisal block section. " +
                    $"context block index: {context.BlockIndex}, actionable block index : {actionableBlockIndex}");
            }

            int costHourglassCount = 0;

            
            if (slotState.PetId > 0)
            {
                var petStateAddress = PetState.DeriveAddress(avatarAddress, slotState.PetId);
                if (!states.TryGetState(petStateAddress, out List rawState))
                {
                    throw new FailedLoadStateException($"{addressesHex}Aborted as the {nameof(PetState)} was failed to load.");
                }

                var petState = new PetState(rawState);
                var petOptionSheet = states.GetSheet<PetOptionSheet>();
                if (!petOptionSheet.TryGetValue(petState.PetId, out var optionRow) ||
                    !optionRow.LevelOptionMap.TryGetValue(petState.Level, out var optionInfo))
                {
                    Log.Debug("{AddressesHex} Pet option not found. (Id : {petId}, Level : {petLevel}). Skip applying pet option.",
                        addressesHex, petState.PetId, petState.Level);
                }
                else
                {
                    if (optionInfo.OptionType == Model.Pet.PetOptionType.IncreaseBlockPerHourglassByValue)
                    {
                        var hgPerBlock = (decimal)gameConfigState.HourglassPerBlock
                            + optionInfo.OptionValue;
                        costHourglassCount = RapidCombination0.CalculateHourglassCount(hgPerBlock, diff);
                    }
                    else
                    {
                        costHourglassCount = RapidCombination0.CalculateHourglassCount(gameConfigState, diff);
                    }
                }
            }
            else
            {
                costHourglassCount = RapidCombination0.CalculateHourglassCount(gameConfigState, diff);
            }

            var materialItemSheet = states.GetSheet<MaterialItemSheet>();
            var row = materialItemSheet.Values.First(r => r.ItemSubType == ItemSubType.Hourglass);
            var hourGlass = ItemFactory.CreateMaterial(row);
            if (!avatarState.inventory.RemoveFungibleItem(hourGlass, context.BlockIndex, costHourglassCount))
            {
                throw new NotEnoughMaterialException(
                    $"{addressesHex}Aborted as the player has no enough material ({row.Id} * {costHourglassCount})");
            }

            if (slotState.TryGetResultId(out var resultId) &&
                avatarState.mailBox.All(mail => mail.id != resultId) &&
                slotState.TryGetMail(
                    context.BlockIndex,
                    context.BlockIndex,
                    out var combinationMail,
                    out var itemEnhanceMail))
            {
                if (combinationMail != null)
                {
                    avatarState.Update(combinationMail);
                }
                else if (itemEnhanceMail != null)
                {
                    avatarState.Update(itemEnhanceMail);
                }
            }

            slotState.UpdateV2(context.BlockIndex, hourGlass, costHourglassCount);
            avatarState.UpdateFromRapidCombinationV2(
                (RapidCombination5.ResultModel)slotState.Result,
                context.BlockIndex);

            var ended = DateTimeOffset.UtcNow;
            Log.Debug("{AddressesHex}RapidCombination Total Executed Time: {Elapsed}", addressesHex, ended - started);
            return states
                .SetState(avatarAddress, avatarState.SerializeV2())
                .SetState(inventoryAddress, avatarState.inventory.Serialize())
                .SetState(worldInformationAddress, avatarState.worldInformation.Serialize())
                .SetState(questListAddress, avatarState.questList.Serialize())
                .SetState(slotAddress, slotState.Serialize());
        }

        protected override IImmutableDictionary<string, IValue> PlainValueInternal =>
            new Dictionary<string, IValue>
            {
                ["avatarAddress"] = avatarAddress.Serialize(),
                ["slotIndex"] = slotIndex.Serialize(),
            }.ToImmutableDictionary();

        protected override void LoadPlainValueInternal(IImmutableDictionary<string, IValue> plainValue)
        {
            avatarAddress = plainValue["avatarAddress"].ToAddress();
            slotIndex = plainValue["slotIndex"].ToInteger();
        }
    }
}
