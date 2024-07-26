using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Bencodex;
using Bencodex.Types;
using Libplanet.Crypto;
using Libplanet.Types.Assets;

namespace Nekoyume.Delegation
{
    public class RebondGrace : IBencodable
    {
        public RebondGrace(Address address, int maxEntries)
        {
            Address = address;
            MaxEntries = maxEntries;
            Entries = ImmutableSortedDictionary<long, ImmutableList<RebondGraceEntry>>.Empty;
        }

        public RebondGrace(Address address, int maxEntries, IValue bencoded)
            : this(address, maxEntries, (List)bencoded)
        {
        }

        public RebondGrace(Address address, int maxEntries, List bencoded)
        {
            Address = address;
            MaxEntries = maxEntries;
            Entries = bencoded
                .Select(kv => kv is List list
                    ? new KeyValuePair<long, ImmutableList<RebondGraceEntry>>(
                        (Integer)list[0],
                        ((List)list[1]).Select(e => new RebondGraceEntry(e)).ToImmutableList())
                    : throw new InvalidCastException(
                        $"Unable to cast object of type '{kv.GetType()}' to type '{typeof(List)}'."))
                .ToImmutableSortedDictionary();
        }

        public RebondGrace(Address address, int maxEntries, IEnumerable<RebondGraceEntry> entries)
            : this(address, maxEntries)
        {
            foreach (var entry in entries)
            {
                AddEntry(entry);
            }
        }

        public Address Address { get; }

        public int MaxEntries { get; }

        public bool IsFull => Entries.Values.Sum(e => e.Count) >= MaxEntries;

        public bool IsEmpty => Entries.IsEmpty;

        public ImmutableSortedDictionary<long, ImmutableList<RebondGraceEntry>> Entries { get; private set; }


        public IValue Bencoded
            => new List(
                Entries.Select(
                    sortedDict => new List(
                        (Integer)sortedDict.Key,
                        new List(sortedDict.Value.Select(e => e.Bencoded)))));

        public void Grace(Address rebondeeAddress, FungibleAssetValue initialGraceFAV, long creationHeight, long releaseHeight)
            => AddEntry(new RebondGraceEntry(rebondeeAddress, initialGraceFAV, creationHeight, releaseHeight));

        public void Release(long height)
        {
            foreach (var (completionHeight, entries) in Entries)
            {
                if (completionHeight <= height)
                {
                    Entries = Entries.Remove(completionHeight);
                }
                else
                {
                    break;
                }
            }
        }

        private void AddEntry(RebondGraceEntry entry)
        {
            if (IsFull)
            {
                throw new InvalidOperationException("Cannot add more entries.");
            }

            if (Entries.TryGetValue(entry.ReleaseHeight, out var entries))
            {
                Entries = Entries.SetItem(entry.ReleaseHeight, entries.Add(entry));
            }
            else
            {
                Entries = Entries.Add(entry.ReleaseHeight, ImmutableList<RebondGraceEntry>.Empty.Add(entry));
            }
        }

        public class RebondGraceEntry : IBencodable
        {
            public RebondGraceEntry(
                Address rebondeeAddress,
                FungibleAssetValue initialGraceFAV,
                long creationHeight,
                long releaseHeight)
            {
                RebondeeAddress = rebondeeAddress;
                InitialGraceFAV = initialGraceFAV;
                GraceFAV = initialGraceFAV;
                CreationHeight = creationHeight;
                ReleaseHeight = releaseHeight;
            }

            public RebondGraceEntry(IValue bencoded)
                : this((List)bencoded)
            {
            }

            private RebondGraceEntry(List bencoded)
                : this(
                      new Address(bencoded[0]),
                      new FungibleAssetValue(bencoded[1]),
                      new FungibleAssetValue(bencoded[2]),
                      (Integer)bencoded[3],
                      (Integer)bencoded[4])
            {
            }

            private RebondGraceEntry(
                Address rebondeeAddress,
                FungibleAssetValue initialGraceFAV,
                FungibleAssetValue graceFAV,
                long creationHeight,
                long releaseHeight)
            {
                RebondeeAddress = rebondeeAddress;
                InitialGraceFAV = initialGraceFAV;
                GraceFAV = graceFAV;
                CreationHeight = creationHeight;
                ReleaseHeight = releaseHeight;
            }

            public Address RebondeeAddress { get; }

            public FungibleAssetValue InitialGraceFAV { get; }

            public FungibleAssetValue GraceFAV { get; private set; }

            public long CreationHeight { get; }

            public long ReleaseHeight { get; }

            public IValue Bencoded => List.Empty
                .Add(RebondeeAddress.Bencoded)
                .Add(InitialGraceFAV.Serialize())
                .Add(GraceFAV.Serialize())
                .Add(CreationHeight)
                .Add(ReleaseHeight);
        }
    }
}
