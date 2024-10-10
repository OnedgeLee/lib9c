using System.Collections.Immutable;
using System.Numerics;
using Libplanet.Crypto;
using Libplanet.Types.Assets;

namespace Nekoyume.Delegation
{
    public interface IUnbonding
    {
        Address Address { get; }

        long LowestExpireHeight { get; }

        bool IsFull { get; }

        bool IsEmpty { get; }

        IUnbonding GetEntriesToRelease(long height);

        IUnbonding Release(long height, out ImmutableList<long> releasedEntryIds);

        IUnbonding Slash(
            BigInteger slashFactor,
            long infractionHeight,
            long height,
            out FungibleAssetValue? slashedFAV);
    }
}
