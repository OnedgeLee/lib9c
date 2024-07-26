using System.Collections.Immutable;
using System.Numerics;
using Bencodex;
using Libplanet.Crypto;
using Libplanet.Types.Assets;

namespace Nekoyume.Delegation
{
    public interface IDelegator : IBencodable
    {
        Address Address { get; }

        ImmutableSortedSet<Address> Delegatees { get; }

        Delegation Delegate(
            IDelegatee<IDelegator> delegatee,
            FungibleAssetValue fav,
            Delegation delegation);

        Delegation Undelegate(
            IDelegatee<IDelegator> delegatee,
            BigInteger share,
            long height,
            Delegation delegation);

        Delegation Redelegate(
            IDelegatee<IDelegator> srcDelegatee,
            IDelegatee<IDelegator> dstDelegatee,
            BigInteger share,
            long height,
            Delegation delegation);

        void Claim(IDelegatee<IDelegator> delegatee);
    }
}
