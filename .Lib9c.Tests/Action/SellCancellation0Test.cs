namespace Lib9c.Tests.Action
{
    using System;
    using System.Linq;
    using Libplanet.Action.State;
    using Libplanet.Crypto;
    using Libplanet.Types.Assets;
    using Nekoyume;
    using Nekoyume.Action;
    using Nekoyume.Model;
    using Nekoyume.Model.Item;
    using Nekoyume.Model.State;
    using Serilog;
    using Xunit;
    using Xunit.Abstractions;

    public class SellCancellation0Test
    {
        private readonly IAccount _initialState;
        private readonly Address _agentAddress;
        private readonly Address _avatarAddress;

        public SellCancellation0Test(ITestOutputHelper outputHelper)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.TestOutput(outputHelper)
                .CreateLogger();

            _initialState = new MockStateDelta();
            var sheets = TableSheetsImporter.ImportSheets();
            foreach (var (key, value) in sheets)
            {
                _initialState = _initialState
                    .SetState(Addresses.TableSheet.Derive(key), value.Serialize());
            }

            var tableSheets = new TableSheets(sheets);

#pragma warning disable CS0618
            // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
            var currency = Currency.Legacy("NCG", 2, null);
#pragma warning restore CS0618
            var goldCurrencyState = new GoldCurrencyState(currency);

            _agentAddress = new PrivateKey().ToAddress();
            var agentState = new AgentState(_agentAddress);
            _avatarAddress = new PrivateKey().ToAddress();
            var rankingMapAddress = new PrivateKey().ToAddress();
            var avatarState = new AvatarState(
                _avatarAddress,
                _agentAddress,
                0,
                tableSheets.GetAvatarSheets(),
                new GameConfigState(),
                rankingMapAddress)
            {
                worldInformation = new WorldInformation(
                    0,
                    tableSheets.WorldSheet,
                    GameConfig.RequireClearedStageLevel.ActionsInShop),
            };
            agentState.avatarAddresses[0] = _avatarAddress;

            var equipment = ItemFactory.CreateItemUsable(
                tableSheets.EquipmentItemSheet.First,
                Guid.NewGuid(),
                0);
            var shopState = new ShopState();
            shopState.Register(new ShopItem(
                _agentAddress,
                _avatarAddress,
                Guid.NewGuid(),
                new FungibleAssetValue(goldCurrencyState.Currency, 100, 0),
                (ITradableItem)equipment));

            _initialState = _initialState
                .SetState(GoldCurrencyState.Address, goldCurrencyState.Serialize())
                .SetState(Addresses.Shop, shopState.Serialize())
                .SetState(_agentAddress, agentState.Serialize())
                .SetState(_avatarAddress, avatarState.Serialize());
        }

        [Fact]
        public void Execute()
        {
            var shopState = _initialState.GetShopState();
            Assert.Single(shopState.Products);

            var (_, shopItem) = shopState.Products.FirstOrDefault();
            Assert.NotNull(shopItem);

            var avatarState = _initialState.GetAvatarState(_avatarAddress);
            Assert.False(avatarState.inventory.TryGetNonFungibleItem(
                shopItem.ItemUsable.ItemId,
                out ItemUsable _));

            var sellCancellationAction = new SellCancellation0
            {
                productId = shopItem.ProductId,
                sellerAvatarAddress = _avatarAddress,
            };
            var nextState = sellCancellationAction.Execute(new ActionContext
            {
                BlockIndex = 0,
                PreviousState = _initialState,
                Random = new TestRandom(),
                Rehearsal = false,
                Signer = _agentAddress,
            });

            var nextShopState = nextState.GetShopState();
            Assert.Empty(nextShopState.Products);

            var nextAvatarState = nextState.GetAvatarState(_avatarAddress);
            Assert.True(nextAvatarState.inventory.TryGetNonFungibleItem(
                shopItem.ItemUsable.ItemId,
                out ItemUsable _));
        }
    }
}
