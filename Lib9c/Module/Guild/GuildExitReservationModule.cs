#nullable enable
using System;
using System.Diagnostics.CodeAnalysis;
using Bencodex.Types;
using Nekoyume.Action;
using Nekoyume.Extensions;
using Nekoyume.Model.Guild;
using Nekoyume.TypedAddress;

namespace Nekoyume.Module.Guild
{
    public static class GuildExitReservationModule
    {
        public static GuildExitReservation GetGuildExitReservation(
            this GuildRepository repository, AgentAddress agentAddress)
        {
            var value = repository.World.GetAccountState(Addresses.GuildExitReservation).GetState(agentAddress);
            if (value is List list)
            {
                return new GuildExitReservation(list);
            }

            throw new FailedLoadStateException("There is no such guild.");
        }

        public static bool TryGetGuildExitReservation(this GuildRepository repository,
            AgentAddress agentAddress, [NotNullWhen(true)] out GuildExitReservation? guildExitReservation)
        {
            try
            {
                guildExitReservation = repository.GetGuildExitReservation(agentAddress);
                return true;
            }
            catch
            {
                guildExitReservation = null;
                return false;
            }
        }

        public static void ReserveGuildExit(
            this GuildRepository repository, AgentAddress agentAddress, long unbondingId, GuildAddress? newGuildAddress)
        {
            if (repository.GetJoinedGuild(agentAddress) is not { } guildAddress)
            {
                throw new InvalidOperationException("The signer does not joined the guild.");
            }

            if (!repository.TryGetGuild(guildAddress, out var guild))
            {
                throw new InvalidOperationException("There is no such guild.");
            }

            if (newGuildAddress is not null && !repository.TryGetGuild(guildAddress, out var newGuild))
            {
                throw new InvalidOperationException("There is no such guild.");
            }

            if (guild.GuildMasterAddress == agentAddress)
            {
                throw new InvalidOperationException("Guild master cannot exit the guild.");
            }

            repository.SetGuildExitReservation(agentAddress, new GuildExitReservation(agentAddress, unbondingId, newGuildAddress));
        }

        public static void CancelGuildExitReservation(
            this GuildRepository repository, AgentAddress agentAddress)
        {
            if (!repository.TryGetGuildExitReservation(agentAddress, out _))
            {
                throw new InvalidOperationException("There is not guild exit reservation.");
            }

            repository.RemoveGuildExitReservation(agentAddress);
        }

        public static void AcceptGuildExitReservation(
            this GuildRepository repository, AgentAddress agentAddress, long height)
        {
            if (!repository.TryGetGuildExitReservation(agentAddress, out var guildExitReservation))
            {
                throw new InvalidOperationException("There is not guild exit reservation.");
            }

            if (repository.GetJoinedGuild(guildExitReservation.AgentAddress) is not { } guildAddress)
            {
                throw new InvalidOperationException(
                    "There is no such guild now. It may be removed. Please cancel and apply another guild.");
            }

            if (!repository.TryGetGuild(guildAddress, out var guild))
            {
                throw new InvalidOperationException("There is no such guild.");
            }

            if (guild.GuildMasterAddress == agentAddress)
            {
                throw new InvalidOperationException("Guild master cannot exit the guild.");
            }

            repository.RemoveGuildExitReservation(agentAddress);
            repository.RemoveGuildParticipant(agentAddress);
            repository.DecreaseGuildMemberCount(guildAddress);
            repository.LeaveGuildWithUndelegate(agentAddress, height);
            if (guildExitReservation.NewGuildAddress is { } newGuildAddress)
            {
                repository.ApplyGuild(agentAddress, newGuildAddress);
            }
        }

        public static void SetGuildExitReservation(
            this GuildRepository repository, AgentAddress agentAddress, GuildExitReservation guildExitReservation)
        {
            repository.UpdateWorld(
                repository.World.MutateAccount(
                    Addresses.GuildExitReservation,
                    account => account.SetState(agentAddress, guildExitReservation.Bencoded)));
        }

        private static void RemoveGuildExitReservation(
            this GuildRepository repository, AgentAddress agentAddress)
        {
            repository.UpdateWorld(
                repository.World.MutateAccount(
                    Addresses.GuildExitReservation,
                    account => account.RemoveState(agentAddress)));
        }
    }
}
