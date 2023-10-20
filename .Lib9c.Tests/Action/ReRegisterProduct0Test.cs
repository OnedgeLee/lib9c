namespace Lib9c.Tests.Action
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Bencodex.Types;
    using Lib9c.Model.Order;
    using Libplanet.Action.State;
    using Libplanet.Crypto;
    using Libplanet.Types.Assets;
    using Nekoyume;
    using Nekoyume.Action;
    using Nekoyume.Action.Extensions;
    using Nekoyume.Battle;
    using Nekoyume.Model;
    using Nekoyume.Model.Item;
    using Nekoyume.Model.Market;
    using Nekoyume.Model.State;
    using Nekoyume.Module;
    using Serilog;
    using Xunit;
    using Xunit.Abstractions;

    public class ReRegisterProduct0Test
    {
        private const long ProductPrice = 100;
        private readonly Address _agentAddress;
        private readonly Address _avatarAddress;
        private readonly Currency _currency;
        private readonly AvatarState _avatarState;
        private readonly TableSheets _tableSheets;
        private readonly GoldCurrencyState _goldCurrencyState;
        private readonly GameConfigState _gameConfigState;
        private IWorld _initialWorld;

        public ReRegisterProduct0Test(ITestOutputHelper outputHelper)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.TestOutput(outputHelper)
                .CreateLogger();

            _initialWorld = new MockWorld();
            var sheets = TableSheetsImporter.ImportSheets();
            foreach (var (key, value) in sheets)
            {
                _initialWorld = LegacyModule.SetState(
                    _initialWorld,
                    Addresses.TableSheet.Derive(key),
                    value.Serialize());
            }

            _tableSheets = new TableSheets(sheets);

#pragma warning disable CS0618
            // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
            _currency = Currency.Legacy("NCG", 2, null);
#pragma warning restore CS0618
            _goldCurrencyState = new GoldCurrencyState(_currency);

            var shopState = new ShopState();

            _agentAddress = new PrivateKey().ToAddress();
            var agentState = new AgentState(_agentAddress);
            _avatarAddress = new PrivateKey().ToAddress();
            var rankingMapAddress = new PrivateKey().ToAddress();
            _gameConfigState = new GameConfigState((Text)_tableSheets.GameConfigSheet.Serialize());
            _avatarState = new AvatarState(
                _avatarAddress,
                _agentAddress,
                0,
                _tableSheets.GetAvatarSheets(),
                _gameConfigState,
                rankingMapAddress)
            {
                worldInformation = new WorldInformation(
                    0,
                    _tableSheets.WorldSheet,
                    GameConfig.RequireClearedStageLevel.ActionsInShop),
            };
            agentState.avatarAddresses[0] = _avatarAddress;

            _initialWorld = LegacyModule.SetState(
                _initialWorld,
                GoldCurrencyState.Address,
                _goldCurrencyState.Serialize());
            _initialWorld = LegacyModule.SetState(
                _initialWorld,
                Addresses.Shop,
                shopState.Serialize());
            _initialWorld = AgentModule.SetAgentState(_initialWorld, _agentAddress, agentState);
            _initialWorld = LegacyModule.SetState(
                _initialWorld,
                Addresses.GameConfig,
                _gameConfigState.Serialize());
            _initialWorld = AvatarModule.SetAvatarState(
                _initialWorld,
                _avatarAddress,
                _avatarState,
                true,
                true,
                true,
                true);
        }

        [Theory]
        [InlineData(ItemType.Equipment, "F9168C5E-CEB2-4faa-B6BF-329BF39FA1E4", 1, 1, 1)]
        [InlineData(ItemType.Costume, "936DA01F-9ABD-4d9d-80C7-02AF85C822A8", 1, 1, 1)]
        [InlineData(ItemType.Material, "15396359-04db-68d5-f24a-d89c18665900", 1, 1, 1)]
        [InlineData(ItemType.Material, "15396359-04db-68d5-f24a-d89c18665900", 2, 1, 2)]
        [InlineData(ItemType.Material, "15396359-04db-68d5-f24a-d89c18665900", 2, 2, 3)]
        public void Execute_BackwardCompatibility(
            ItemType itemType,
            string guid,
            int itemCount,
            int inventoryCount,
            int expectedCount
        )
        {
            var avatarState = AvatarModule.GetAvatarState(_initialWorld, _avatarAddress);
            ITradableItem tradableItem;
            var itemId = new Guid(guid);
            var orderId = Guid.NewGuid();
            var updateSellOrderId = Guid.NewGuid();
            ItemSubType itemSubType;
            const long requiredBlockIndex = Order.ExpirationInterval;
            switch (itemType)
            {
                case ItemType.Equipment:
                {
                    var itemUsable = ItemFactory.CreateItemUsable(
                        _tableSheets.EquipmentItemSheet.First,
                        itemId,
                        requiredBlockIndex);
                    tradableItem = (ITradableItem)itemUsable;
                    itemSubType = itemUsable.ItemSubType;
                    break;
                }

                case ItemType.Costume:
                {
                    var costume = ItemFactory.CreateCostume(_tableSheets.CostumeItemSheet.First, itemId);
                    costume.Update(requiredBlockIndex);
                    tradableItem = costume;
                    itemSubType = costume.ItemSubType;
                    break;
                }

                default:
                {
                    var material = ItemFactory.CreateTradableMaterial(
                        _tableSheets.MaterialItemSheet.OrderedList.First(r => r.ItemSubType == ItemSubType.Hourglass));
                    itemSubType = material.ItemSubType;
                    material.RequiredBlockIndex = requiredBlockIndex;
                    tradableItem = material;
                    break;
                }
            }

            var shardedShopAddress = ShardedShopStateV2.DeriveAddress(itemSubType, orderId);
            var shopState = new ShardedShopStateV2(shardedShopAddress);
            var order = OrderFactory.Create(
                _agentAddress,
                _avatarAddress,
                orderId,
                new FungibleAssetValue(_goldCurrencyState.Currency, 100, 0),
                tradableItem.TradableId,
                requiredBlockIndex,
                itemSubType,
                itemCount
            );

            var orderDigestList = new OrderDigestListState(OrderDigestListState.DeriveAddress(_avatarAddress));
            var prevState = _initialWorld;

            if (inventoryCount > 1)
            {
                for (int i = 0; i < inventoryCount; i++)
                {
                    // Different RequiredBlockIndex for divide inventory slot.
                    if (tradableItem is ITradableFungibleItem tradableFungibleItem)
                    {
                        var tradable = (TradableMaterial)tradableFungibleItem.Clone();
                        tradable.RequiredBlockIndex = tradableItem.RequiredBlockIndex - i;
                        avatarState.inventory.AddItem(tradable, 2 - i);
                    }
                }
            }
            else
            {
                avatarState.inventory.AddItem((ItemBase)tradableItem, itemCount);
            }

            var sellItem = order.Sell(avatarState);
            var orderDigest = order.Digest(avatarState, _tableSheets.CostumeStatSheet);
            shopState.Add(orderDigest, requiredBlockIndex);
            orderDigestList.Add(orderDigest);

            Assert.True(avatarState.inventory.TryGetLockedItem(new OrderLock(orderId), out _));

            Assert.Equal(inventoryCount, avatarState.inventory.Items.Count);
            Assert.Equal(expectedCount, avatarState.inventory.Items.Sum(i => i.count));

            Assert.Single(shopState.OrderDigestList);
            Assert.Single(orderDigestList.OrderDigestList);

            Assert.Equal(requiredBlockIndex * 2, sellItem.RequiredBlockIndex);

            prevState = AvatarModule.SetAvatarState(
                prevState,
                _avatarAddress,
                avatarState,
                true,
                true,
                true,
                true);
            prevState = LegacyModule.SetState(
                prevState,
                Addresses.GetItemAddress(itemId),
                sellItem.Serialize());
            prevState = LegacyModule.SetState(
                prevState,
                Order.DeriveAddress(order.OrderId),
                order.Serialize());
            prevState = LegacyModule.SetState(
                prevState,
                orderDigestList.Address,
                orderDigestList.Serialize());
            prevState = LegacyModule.SetState(prevState, shardedShopAddress, shopState.Serialize());

            var currencyState = LegacyModule.GetGoldCurrency(prevState);
            var price = new FungibleAssetValue(currencyState, ProductPrice, 0);

            var updateSellInfo = new UpdateSellInfo(
                orderId,
                updateSellOrderId,
                itemId,
                itemSubType,
                price,
                itemCount
            );

            var action = new UpdateSell
            {
                sellerAvatarAddress = _avatarAddress,
                updateSellInfos = new[] { updateSellInfo },
            };

            var expectedState = action.Execute(new ActionContext
            {
                BlockIndex = 101,
                PreviousState = prevState,
                RandomSeed = 0,
                Rehearsal = false,
                Signer = _agentAddress,
            }).GetAccount(ReservedAddresses.LegacyAccount);

            var updateSellShopAddress = ShardedShopStateV2.DeriveAddress(itemSubType, updateSellOrderId);
            var nextShopState = new ShardedShopStateV2((Dictionary)expectedState.GetState(updateSellShopAddress));
            Assert.Equal(1, nextShopState.OrderDigestList.Count);
            Assert.NotEqual(orderId, nextShopState.OrderDigestList.First().OrderId);
            Assert.Equal(updateSellOrderId, nextShopState.OrderDigestList.First().OrderId);
            Assert.Equal(itemId, nextShopState.OrderDigestList.First().TradableId);
            Assert.Equal(requiredBlockIndex + 101, nextShopState.OrderDigestList.First().ExpiredBlockIndex);

            var productType = tradableItem is TradableMaterial
                ? ProductType.Fungible
                : ProductType.NonFungible;
            var reRegister = new ReRegisterProduct
            {
                AvatarAddress = _avatarAddress,
                ReRegisterInfos = new List<(IProductInfo, IRegisterInfo)>
                {
                    (
                        new ItemProductInfo
                        {
                            AgentAddress = _agentAddress,
                            AvatarAddress = _avatarAddress,
                            Legacy = true,
                            Price = order.Price,
                            ProductId = orderId,
                            Type = productType,
                        },
                        new RegisterInfo
                        {
                            AvatarAddress = _avatarAddress,
                            ItemCount = itemCount,
                            Price = updateSellInfo.price,
                            TradableId = order.TradableId,
                            Type = productType,
                        }
                    ),
                },
            };

            var actualWorld = reRegister.Execute(new ActionContext
            {
                BlockIndex = 101,
                PreviousState = prevState,
                RandomSeed = 0,
                Rehearsal = false,
                Signer = _agentAddress,
            });

            var actualAccount = actualWorld.GetAccount(ReservedAddresses.LegacyAccount);

            var targetShopState = new ShardedShopStateV2((Dictionary)actualAccount.GetState(shardedShopAddress));
            var nextOrderDigestListState = new OrderDigestListState((Dictionary)actualAccount.GetState(orderDigestList.Address));
            Assert.Empty(targetShopState.OrderDigestList);
            Assert.Empty(nextOrderDigestListState.OrderDigestList);
            var productsState =
                new ProductsState(
                    (List)actualAccount.GetState(ProductsState.DeriveAddress(_avatarAddress)));
            var productId = Assert.Single(productsState.ProductIds);
            var product =
                ProductFactory.DeserializeProduct((List)actualAccount.GetState(Product.DeriveAddress(productId)));
            Assert.Equal(productId, product.ProductId);
            Assert.Equal(productType, product.Type);
            Assert.Equal(order.Price, product.Price);

            var nextAvatarState = AvatarModule.GetAvatarState(actualWorld, _avatarAddress);
            Assert.Equal(_gameConfigState.ActionPointMax - ReRegisterProduct.CostAp, nextAvatarState.actionPoint);
        }

        [Fact]
        public void Execute_Throw_ListEmptyException()
        {
            var action = new ReRegisterProduct
            {
                AvatarAddress = _avatarAddress,
                ReRegisterInfos = new List<(IProductInfo, IRegisterInfo)>(),
            };

            var actionContext = new ActionContext();
            actionContext.PreviousState = new MockWorld();
            Assert.Throws<ListEmptyException>(() => action.Execute(actionContext));
        }

        [Fact]
        public void Execute_Throw_ArgumentOutOfRangeException()
        {
            var reRegisterInfos = new List<(IProductInfo, IRegisterInfo)>();
            for (int i = 0; i < ReRegisterProduct.Capacity + 1; i++)
            {
                reRegisterInfos.Add((new ItemProductInfo(), new RegisterInfo()));
            }

            var action = new ReRegisterProduct
            {
                AvatarAddress = _avatarAddress,
                ReRegisterInfos = reRegisterInfos,
            };

            var actionContext = new ActionContext();
            actionContext.PreviousState = new MockWorld();
            Assert.Throws<ArgumentOutOfRangeException>(() => action.Execute(actionContext));
        }
    }
}
