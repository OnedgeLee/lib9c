#nullable enable
using System;
using Bencodex.Types;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Nekoyume.Action;
using Nekoyume.Delegation;
using Nekoyume.Extensions;

namespace Nekoyume.Module.Delegation
{
    public static class UnbondingSetModule
    {
        public static UnbondingSet GetUnbondingSet(this IWorldState world)
            => TryGetUnbondingSet(world, out var unbondingSet)
                ? unbondingSet!
                : new UnbondingSet();

        public static bool TryGetUnbondingSet(
            this IWorldState world, out UnbondingSet? unbondingSet)
        {
            try
            {
                var value = world.GetAccountState(Addresses.UnbondingSet)
                    .GetState(UnbondingSet.Address);
                if (!(value is List list))
                {
                    unbondingSet = null;
                    return false;
                }

                unbondingSet = new UnbondingSet(list);
                return true;
            }
            catch
            {
                unbondingSet = null;
                return false;
            }
        }

        public static IWorld SetUnbondingSet(this IWorld world, UnbondingSet unbondingSet)
            => world.MutateAccount(
                Addresses.UnbondingSet,
                account => account.SetState(UnbondingSet.Address, unbondingSet.Bencoded));

        public static IWorld ReleaseUnbondigSet(this IWorld world, IActionContext context, UnbondingSet unbondingSet)
        {
            var releasedUnbondings = unbondingSet.ReleaseUnbondings(
                context.BlockIndex,
                (address, type) => world.GetAccount(AccountAddress(type)).GetState(address)
                    ?? throw new FailedLoadStateException(
                        $"Tried to release unbonding on {address}, but unbonding does not exist."));

            foreach (var unbonding in releasedUnbondings)
            {
                world = unbonding switch
                {
                    UnbondLockIn unbondLockIn => world.SetUnbondLockIn(unbondLockIn),
                    RebondGrace rebondGrace => world.SetRebondGrace(rebondGrace),
                    _ => throw new ArgumentException("Invalid unbonding type.")
                };

                unbondingSet = unbondingSet.SetUnbonding(unbonding);
            }

            world = SetUnbondingSet(world, unbondingSet);

            return world;
        }

        private static Address AccountAddress(Type type) => type switch
        {
            var t when t == typeof(UnbondLockIn) => Addresses.UnbondLockIn,
            var t when t == typeof(RebondGrace) => Addresses.RebondGrace,
            _ => throw new ArgumentException("Invalid unbonding type.")
        };
    }
}
