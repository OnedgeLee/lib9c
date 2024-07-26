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
    public class UnbondLockIn : IBencodable
    {
        public UnbondLockIn(Address address, int maxEntries)
        {
            Address = address;
            MaxEntries = maxEntries;
            Entries = ImmutableSortedDictionary<long, ImmutableList<UnbondLockInEntry>>.Empty;
        }

        public UnbondLockIn(Address address, int maxEntries, IValue bencoded)
            : this(address, maxEntries, (List)bencoded)
        {
        }

        public UnbondLockIn(Address address, int maxEntries, List bencoded)
        {
            Address = address;
            MaxEntries = maxEntries;
            Entries = bencoded
                .Select(kv => kv is List list
                    ? new KeyValuePair<long, ImmutableList<UnbondLockInEntry>>(
                        (Integer)list[0],
                        ((List)list[1]).Select(e => new UnbondLockInEntry(e)).ToImmutableList())
                    : throw new InvalidCastException(
                        $"Unable to cast object of type '{kv.GetType()}' to type '{typeof(List)}'."))
                .ToImmutableSortedDictionary();
        }

        public UnbondLockIn(Address address, int maxEntries, IEnumerable<UnbondLockInEntry> entries)
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

        public ImmutableSortedDictionary<long, ImmutableList<UnbondLockInEntry>> Entries { get; private set; }

        public IValue Bencoded
            => new List(
                Entries.Select(
                    sortedDict => new List(
                        (Integer)sortedDict.Key,
                        new List(sortedDict.Value.Select(e => e.Bencoded)))));

        public void LockIn(FungibleAssetValue lockInFAV, long creationHeight, long releaseHeight)
            => AddEntry(new UnbondLockInEntry(lockInFAV, creationHeight, releaseHeight));

        public void Cancel(FungibleAssetValue cancellingFAV, long height)
        {
            if (Cancellable(height) < cancellingFAV)
            {
                throw new InvalidOperationException("Cannot cancel more than locked-in FAV.");
            }

            foreach (var (releaseHeight, entries) in Entries.Reverse())
            {
                if (releaseHeight <= height)
                {
                    throw new InvalidOperationException("Cannot cancel released undelegation.");
                }

                foreach (var entry in entries.Select((value, index) => (value, index)).Reverse())
                {
                    if (cancellingFAV.RawValue < 0)
                    {
                        throw new InvalidOperationException("Insufficient undelegation to cancel");
                    }

                    if (entry.value.LockInFAV.Abs() < cancellingFAV)
                    {
                        cancellingFAV += entry.value.LockInFAV;
                        Entries = Entries.SetItem(
                            releaseHeight,
                            Entries[releaseHeight].RemoveAt(entry.index));
                    }
                    else
                    {
                        entry.value.Cancel(cancellingFAV);
                        Entries = Entries.SetItem(
                            releaseHeight,
                            Entries[releaseHeight].SetItem(entry.index, entry.value));
                        break;
                    }
                }

                if (entries.IsEmpty)
                {
                    Entries = Entries.Remove(releaseHeight);
                }
            }
        }

        public FungibleAssetValue? Release(long height)
        {
            FungibleAssetValue? releaseFAV = null;
            foreach (var (completionHeight, entries) in Entries)
            {
                if (completionHeight <= height)
                {
                    FungibleAssetValue entriesFAV = entries
                        .Select(e => e.LockInFAV)
                        .Aggregate((accum, next) => accum + next);
                    releaseFAV = releaseFAV is null
                        ? entriesFAV
                        : releaseFAV + entriesFAV;
                    Entries = Entries.Remove(completionHeight);
                }
                else
                {
                    break;
                }
            }

            return releaseFAV;
        }

        private FungibleAssetValue Cancellable(long height)
            => - Entries
                .Where(kv => kv.Key > height)
                .SelectMany(kv => kv.Value)
                .Select(e => e.LockInFAV)
                .Aggregate((accum, next) => accum + next);

        private void AddEntry(UnbondLockInEntry entry)
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
                Entries = Entries.Add(entry.ReleaseHeight, ImmutableList<UnbondLockInEntry>.Empty.Add(entry));
            }
        }

        public class UnbondLockInEntry : IBencodable
        {
            public UnbondLockInEntry(
                FungibleAssetValue lockInFAV,
                long creationHeight,
                long releaseHeight)
                : this(lockInFAV, lockInFAV, creationHeight, releaseHeight)
            {
            }

            public UnbondLockInEntry(IValue bencoded)
                : this((List)bencoded)
            {
            }

            private UnbondLockInEntry(List bencoded)
                : this(
                      new FungibleAssetValue(bencoded[0]),
                      new FungibleAssetValue(bencoded[1]),
                      (Integer)bencoded[2],
                      (Integer)bencoded[3])
            {
            }

            private UnbondLockInEntry(
                FungibleAssetValue initialLockInFAV,
                FungibleAssetValue lockInFAV,
                long creationHeight,
                long releaseHeight)
            {
                InitialLockInFAV = initialLockInFAV;
                LockInFAV = lockInFAV;
                CreationHeight = creationHeight;
                ReleaseHeight = releaseHeight;
            }

            public FungibleAssetValue InitialLockInFAV { get; private set; }

            public FungibleAssetValue LockInFAV { get; private set; }

            public long CreationHeight { get; }

            public long ReleaseHeight { get; }

            public IValue Bencoded => List.Empty
                .Add(InitialLockInFAV.Serialize())
                .Add(LockInFAV.Serialize())
                .Add(CreationHeight)
                .Add(ReleaseHeight);

            public void Cancel(FungibleAssetValue cancellingFAV)
            {
                if (LockInFAV.Abs() <= cancellingFAV)
                {
                    throw new InvalidOperationException("Cannot cancel more than locked-in FAV.");
                }

                InitialLockInFAV += cancellingFAV;
                LockInFAV += cancellingFAV;
            }
        }
    }
}
