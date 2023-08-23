namespace Lib9c.Tests.Util
{
    using System.Collections.Generic;
    using System.IO;
    using Lib9c.Tests.Action;
    using Libplanet.Action.State;
    using Libplanet.Crypto;
    using Libplanet.Types.Assets;
    using Nekoyume;
    using Nekoyume.Action.Extensions;
    using Nekoyume.Model.State;
    using Nekoyume.TableData;

    public static class InitializeUtil
    {
        public static (
            TableSheets tableSheets,
            Address agentAddr,
            Address avatarAddr,
            IWorld initialStatesWithAvatarStateV1,
            IWorld initialStatesWithAvatarStateV2
            ) InitializeStates(
                Address? adminAddr = null,
                Address? agentAddr = null,
                int avatarIndex = 0,
                bool isDevEx = false,
                Dictionary<string, string> sheetsOverride = null)
        {
            adminAddr ??= new PrivateKey().ToAddress();
            var context = new ActionContext();
            var states = new MockAccount().SetState(
                Addresses.Admin,
                new AdminState(adminAddr.Value, long.MaxValue).Serialize());

            var goldCurrency = Currency.Legacy(
                "NCG",
                2,
                minters: default
            );
            var goldCurrencyState = new GoldCurrencyState(goldCurrency);
            states = states
                .SetState(goldCurrencyState.address, goldCurrencyState.Serialize())
                .MintAsset(context, goldCurrencyState.address, goldCurrency * 1_000_000_000);

            var tuple = InitializeTableSheets(states, isDevEx, sheetsOverride);
            states = tuple.states;
            var tableSheets = new TableSheets(tuple.sheets);
            var gameConfigState = new GameConfigState(tuple.sheets[nameof(GameConfigSheet)]);
            states = states.SetState(gameConfigState.address, gameConfigState.Serialize());

            agentAddr ??= new PrivateKey().ToAddress();
            var avatarAddr = Addresses.GetAvatarAddress(agentAddr.Value, avatarIndex);
            var agentState = new AgentState(agentAddr.Value);
            var avatarState = new AvatarState(
                avatarAddr,
                agentAddr.Value,
                0,
                tableSheets.GetAvatarSheets(),
                new GameConfigState(),
                avatarAddr.Derive("ranking_map"));
            agentState.avatarAddresses.Add(avatarIndex, avatarAddr);

            var initialStatesWithAvatarStateV1 = states
                .SetState(agentAddr.Value, agentState.Serialize())
                .SetState(avatarAddr, avatarState.Serialize());
            var initialStatesWithAvatarStateV2 = states
                .SetState(agentAddr.Value, agentState.Serialize())
                .SetState(avatarAddr, avatarState.SerializeV2())
                .SetState(
                    avatarAddr.Derive(SerializeKeys.LegacyInventoryKey),
                    avatarState.inventory.Serialize())
                .SetState(
                    avatarAddr.Derive(SerializeKeys.LegacyWorldInformationKey),
                    avatarState.worldInformation.Serialize())
                .SetState(
                    avatarAddr.Derive(SerializeKeys.LegacyQuestListKey),
                    avatarState.questList.Serialize());

            return (
                tableSheets,
                agentAddr.Value,
                avatarAddr,
                new MockWorld(initialStatesWithAvatarStateV1),
                new MockWorld(initialStatesWithAvatarStateV2));
        }

        private static (IWorld world, Dictionary<string, string> sheets)
            InitializeTableSheets(
                IWorld world,
                bool isDevEx = false,
                Dictionary<string, string> sheetsOverride = null)
        {
            var sheets = TableSheetsImporter.ImportSheets(
                isDevEx
                    ? Path.GetFullPath("../../").Replace(
                        Path.Combine(".Lib9c.DevExtensions.Tests", "bin"),
                        Path.Combine("Lib9c", "TableCSV"))
                    : null
            );
            if (sheetsOverride != null)
            {
                foreach (var (key, value) in sheetsOverride)
                {
                    sheets[key] = value;
                }
            }

            foreach (var (key, value) in sheets)
            {
                world = world.SetAccount(
                    world.GetAccount(ReservedAddresses.LegacyAccount).SetState(
                        Addresses.TableSheet.Derive(key),
                        value.Serialize()));
            }

            return (world, sheets);
        }
    }
}
