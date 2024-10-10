using System;
using Bencodex;
using Bencodex.Types;
using Nekoyume.Action;
using Nekoyume.TypedAddress;

namespace Nekoyume.Model.Guild
{
    public class GuildExitReservation : IBencodable
    {
        private const string StateTypeName = "guild_exit_reservation";
        private const long StateVersion = 1;

        public readonly AgentAddress AgentAddress;

        public readonly long UnbondingId;

        public readonly GuildAddress? NewGuildAddress;

        public GuildExitReservation(AgentAddress agentAddress, long unbondingId, GuildAddress? newGuildAddress)
        {
            AgentAddress = agentAddress;
            UnbondingId = unbondingId;
            NewGuildAddress = newGuildAddress;
        }

        public GuildExitReservation(List list)
        {
            if (list[0] is not Text text || text != StateTypeName || list[1] is not Integer integer)
            {
                throw new InvalidCastException();
            }

            if (integer > StateVersion)
            {
                throw new FailedLoadStateException("Un-deserializable state.");
            }

            AgentAddress = new AgentAddress(list[2]);
            UnbondingId = (Integer)list[3];
            NewGuildAddress = list[4] is not Null ? new GuildAddress(list[4]) : null;
        }

        public List Bencoded => new(
            (Text)StateTypeName,
            (Integer)StateVersion,
            AgentAddress.Bencoded,
            (Integer)UnbondingId,
            NewGuildAddress?.Bencoded ?? Null.Value);

        IValue IBencodable.Bencoded => Bencoded;
    }
}
