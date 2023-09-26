using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Nekoyume.Action.Extensions;
using Nekoyume.Battle;
using Nekoyume.Model;
using Nekoyume.Model.Item;
using Nekoyume.Model.Market;
using Nekoyume.Model.State;
using Nekoyume.Module;
using Nekoyume.TableData;
using static Lib9c.SerializeKeys;

namespace Nekoyume.Action
{
    [ActionType("register_product2")]
    public class RegisterProduct : GameAction
    {
        public const int CostAp = 5;
        public const int Capacity = 100;
        public Address AvatarAddress;
        public IEnumerable<IRegisterInfo> RegisterInfos;
        public bool ChargeAp;

        public override IWorld Execute(IActionContext context)
        {
            context.UseGas(1);
            if (context.Rehearsal)
            {
                return context.PreviousState;
            }

            var world = context.PreviousState;

            if (!RegisterInfos.Any())
            {
                throw new ListEmptyException("RegisterInfos was empty");
            }

            if (RegisterInfos.Count() > Capacity)
            {
                throw new ArgumentOutOfRangeException($"{nameof(RegisterInfos)} must be less than or equal {Capacity}.");
            }

            var ncg = LegacyModule.GetGoldCurrency(world);
            foreach (var registerInfo in RegisterInfos)
            {
                registerInfo.ValidateAddress(AvatarAddress);
                registerInfo.ValidatePrice(ncg);
                registerInfo.Validate();
            }

            if (!AvatarModule.TryGetAvatarStateV2(
                    world,
                    context.Signer,
                    AvatarAddress,
                    out var avatarState,
                    out var migrationRequired))
            {
                throw new FailedLoadStateException("failed to load avatar state.");
            }

            if (!avatarState.worldInformation.IsStageCleared(
                    GameConfig.RequireClearedStageLevel.ActionsInShop))
            {
                avatarState.worldInformation.TryGetLastClearedStageId(out var current);
                throw new NotEnoughClearedStageLevelException(
                    AvatarAddress.ToHex(),
                    GameConfig.RequireClearedStageLevel.ActionsInShop,
                    current);
            }

            avatarState.UseAp(
                CostAp,
                ChargeAp,
                LegacyModule.GetSheet<MaterialItemSheet>(world),
                context.BlockIndex,
                LegacyModule.GetGameConfigState(world));
            var productsStateAddress = ProductsState.DeriveAddress(AvatarAddress);
            ProductsState productsState;
            if (LegacyModule.TryGetState(world, productsStateAddress, out List rawProducts))
            {
                productsState = new ProductsState(rawProducts);
            }
            else
            {
                productsState = new ProductsState();
                var marketState = LegacyModule.TryGetState(
                    world,
                    Addresses.Market,
                    out List rawMarketList)
                    ? new MarketState(rawMarketList)
                    : new MarketState();
                marketState.AvatarAddresses.Add(AvatarAddress);
                world = LegacyModule.SetState(world, Addresses.Market, marketState.Serialize());
            }

            var random = context.GetRandom();
            foreach (var info in RegisterInfos.OrderBy(r => r.Type).ThenBy(r => r.Price))
            {
                world = Register(context, info, avatarState, productsState, world, random);
            }
            
            world = LegacyModule.SetState(world, productsStateAddress, productsState.Serialize());

            if (migrationRequired)
            {
                world = AvatarModule.SetAvatarStateV2(world, AvatarAddress, avatarState);
            }
            else
            {
                world = AvatarModule.SetAvatarV2(world, AvatarAddress, avatarState);
                world = AvatarModule.SetInventory(world, AvatarAddress.Derive(LegacyInventoryKey), avatarState.inventory);
            }

            return world;
        }

        public static IWorld Register(
            IActionContext context,
            IRegisterInfo info,
            AvatarState avatarState,
            ProductsState productsState,
            IWorld world,
            IRandom random)
        {
            switch (info)
            {
                case RegisterInfo registerInfo:
                    switch (info.Type)
                    {
                        case ProductType.Fungible:
                        case ProductType.NonFungible:
                        {
                            var tradableId = registerInfo.TradableId;
                            var itemCount = registerInfo.ItemCount;
                            var type = registerInfo.Type;
                            ITradableItem tradableItem = null;
                            switch (type)
                            {
                                case ProductType.Fungible:
                                {
                                    if (avatarState.inventory.TryGetTradableItems(
                                            tradableId,
                                            context.BlockIndex,
                                            itemCount,
                                            out var items))
                                    {
                                        int totalCount = itemCount;
                                        tradableItem = (ITradableItem)items.First().item;
                                        foreach (var inventoryItem in items)
                                        {
                                            int removeCount = Math.Min(
                                                totalCount,
                                                inventoryItem.count);
                                            ITradableFungibleItem tradableFungibleItem =
                                                (ITradableFungibleItem)inventoryItem.item;
                                            if (!avatarState.inventory.RemoveTradableItem(
                                                    tradableId,
                                                    tradableFungibleItem.RequiredBlockIndex,
                                                    removeCount))
                                            {
                                                throw new ItemDoesNotExistException(
                                                    $"failed to remove tradable material {tradableId}/{itemCount}");
                                            }

                                            totalCount -= removeCount;
                                            if (totalCount < 1)
                                            {
                                                break;
                                            }
                                        }

                                        if (totalCount != 0)
                                        {
                                            throw new InvalidItemCountException();
                                        }
                                    }

                                    break;
                                }
                                case ProductType.NonFungible:
                                {
                                    if (avatarState.inventory.TryGetNonFungibleItem(
                                            tradableId,
                                            out var item) &&
                                        avatarState.inventory.RemoveNonFungibleItem(tradableId))
                                    {
                                        tradableItem = item.item as ITradableItem;
                                    }

                                    break;
                                }
                            }

                            if (tradableItem is null ||
                                tradableItem.RequiredBlockIndex > context.BlockIndex)
                            {
                                throw new ItemDoesNotExistException(
                                    $"can't find item: {tradableId}");
                            }

                            Guid productId = random.GenerateRandomGuid();
                            var product = new ItemProduct
                            {
                                ProductId = productId,
                                Price = registerInfo.Price,
                                TradableItem = tradableItem,
                                ItemCount = itemCount,
                                RegisteredBlockIndex = context.BlockIndex,
                                Type = registerInfo.Type,
                                SellerAgentAddress = context.Signer,
                                SellerAvatarAddress = registerInfo.AvatarAddress,
                            };
                            productsState.ProductIds.Add(productId);
                            world = LegacyModule.SetState(
                                world,
                                Product.DeriveAddress(productId),
                                product.Serialize());
                            break;
                        }
                    }

                    break;
                case AssetInfo assetInfo:
                {
                    Guid productId = random.GenerateRandomGuid();
                    Address productAddress = Product.DeriveAddress(productId);
                    FungibleAssetValue asset = assetInfo.Asset;
                    var product = new FavProduct
                    {
                        ProductId = productId,
                        Price = assetInfo.Price,
                        Asset = asset,
                        RegisteredBlockIndex = context.BlockIndex,
                        Type = assetInfo.Type,
                        SellerAgentAddress = context.Signer,
                        SellerAvatarAddress = assetInfo.AvatarAddress,
                    };
                    world = LegacyModule.TransferAsset(
                        world,
                        context,
                        avatarState.address,
                        productAddress,
                        asset);
                    world = LegacyModule.SetState(world, productAddress, product.Serialize());
                    productsState.ProductIds.Add(productId);
                    break;
                }
            }

            return world;
        }

        protected override IImmutableDictionary<string, IValue> PlainValueInternal =>
            new Dictionary<string, IValue>
            {
                ["r"] = new List(RegisterInfos.Select(r => r.Serialize())),
                ["a"] = AvatarAddress.Serialize(),
                ["c"] = ChargeAp.Serialize(),
            }.ToImmutableDictionary();

        protected override void LoadPlainValueInternal(IImmutableDictionary<string, IValue> plainValue)
        {
            var serialized = (List) plainValue["r"];
            RegisterInfos = serialized.Cast<List>()
                .Select(ProductFactory.DeserializeRegisterInfo).ToList();
            AvatarAddress = plainValue["a"].ToAddress();
            ChargeAp = plainValue["c"].ToBoolean();
        }
    }
}
