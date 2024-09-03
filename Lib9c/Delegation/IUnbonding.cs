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

        IUnbonding Release(long height);

        IUnbonding Slash(BigInteger slashFactor, long infractionHeight, out FungibleAssetValue? slashedFAV);
    }
}
