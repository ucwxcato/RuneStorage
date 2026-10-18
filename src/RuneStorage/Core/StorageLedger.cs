using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace RuneStorage.Storage
{
    internal readonly struct StorageKey : IEquatable<StorageKey>, IComparable<StorageKey>
    {
        public string PrefabName { get; }
        public string Fingerprint { get; }

        private StorageKey(string prefabName, string fingerprint)
        {
            PrefabName = prefabName;
            Fingerprint = fingerprint;
        }

        public static bool TryCreate(string prefabName, byte[] canonicalIdentity, out StorageKey key)
        {
            key = default;
            if (string.IsNullOrWhiteSpace(prefabName) || canonicalIdentity == null)
            {
                return false;
            }

            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] digest = sha256.ComputeHash(canonicalIdentity);
                key = new StorageKey(prefabName, BitConverter.ToString(digest).Replace("-", string.Empty));
                return true;
            }
        }

        public bool Matches(byte[] canonicalIdentity)
        {
            return TryCreate(PrefabName, canonicalIdentity, out StorageKey expected) && Equals(expected);
        }

        public int CompareTo(StorageKey other)
        {
            int prefabOrder = string.Compare(PrefabName, other.PrefabName, StringComparison.Ordinal);
            return prefabOrder != 0
                ? prefabOrder
                : string.Compare(Fingerprint, other.Fingerprint, StringComparison.Ordinal);
        }

        public bool Equals(StorageKey other)
        {
            return string.Equals(PrefabName, other.PrefabName, StringComparison.Ordinal)
                && string.Equals(Fingerprint, other.Fingerprint, StringComparison.Ordinal);
        }

        public override bool Equals(object obj)
        {
            return obj is StorageKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((PrefabName == null ? 0 : StringComparer.Ordinal.GetHashCode(PrefabName)) * 397)
                    ^ (Fingerprint == null ? 0 : StringComparer.Ordinal.GetHashCode(Fingerprint));
            }
        }

        public override string ToString()
        {
            return PrefabName + ":" + Fingerprint;
        }
    }

    internal enum StorageMutationResult
    {
        Success,
        InvalidAmount,
        InvalidSnapshot,
        ItemCapacityExceeded,
        KeyCapacityExceeded,
        ItemNotFound,
        InsufficientStoredAmount
    }

    internal sealed class StorageEntrySnapshot
    {
        private readonly byte[] _canonicalIdentity;
        private readonly byte[] _serializedItemSnapshot;

        public StorageKey Key { get; }
        public int Amount { get; }
        public byte[] CanonicalIdentity => (byte[])_canonicalIdentity.Clone();
        public byte[] SerializedItemSnapshot => (byte[])_serializedItemSnapshot.Clone();

        internal StorageEntrySnapshot(StorageKey key, int amount, byte[] canonicalIdentity, byte[] serializedItemSnapshot)
        {
            Key = key;
            Amount = amount;
            _canonicalIdentity = (byte[])canonicalIdentity.Clone();
            _serializedItemSnapshot = (byte[])serializedItemSnapshot.Clone();
        }
    }

    internal sealed class StorageLedger
    {
        public const int StageOneItemCapacity = 20000;
        public const int StageOneKeyCapacity = 256;

        private readonly Dictionary<StorageKey, Entry> _entries = new Dictionary<StorageKey, Entry>();
        private int _itemCount;

        public int ItemCapacity { get; }
        public int KeyCapacity { get; }
        public int ItemCount => _itemCount;
        public int KeyCount => _entries.Count;

        public StorageLedger(int itemCapacity = StageOneItemCapacity, int keyCapacity = StageOneKeyCapacity)
        {
            if (itemCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(itemCapacity));
            if (keyCapacity <= 0) throw new ArgumentOutOfRangeException(nameof(keyCapacity));

            ItemCapacity = itemCapacity;
            KeyCapacity = keyCapacity;
        }

        public StorageMutationResult TryAdd(
            StorageKey key,
            byte[] canonicalIdentity,
            byte[] serializedItemSnapshot,
            int amount)
        {
            if (amount <= 0) return StorageMutationResult.InvalidAmount;
            if (serializedItemSnapshot == null || !key.Matches(canonicalIdentity))
            {
                return StorageMutationResult.InvalidSnapshot;
            }

            if (amount > ItemCapacity - _itemCount) return StorageMutationResult.ItemCapacityExceeded;

            if (_entries.TryGetValue(key, out Entry existing))
            {
                if (!BytesEqual(existing.CanonicalIdentity, canonicalIdentity))
                {
                    return StorageMutationResult.InvalidSnapshot;
                }

                existing.Amount += amount;
            }
            else
            {
                if (_entries.Count >= KeyCapacity) return StorageMutationResult.KeyCapacityExceeded;
                _entries.Add(key, new Entry(canonicalIdentity, serializedItemSnapshot, amount));
            }

            _itemCount += amount;
            return StorageMutationResult.Success;
        }

        public StorageMutationResult TryRemove(StorageKey key, int amount, out byte[] serializedItemSnapshot)
        {
            serializedItemSnapshot = null;
            if (amount <= 0) return StorageMutationResult.InvalidAmount;
            if (!_entries.TryGetValue(key, out Entry entry)) return StorageMutationResult.ItemNotFound;
            if (amount > entry.Amount) return StorageMutationResult.InsufficientStoredAmount;

            serializedItemSnapshot = (byte[])entry.SerializedItemSnapshot.Clone();
            entry.Amount -= amount;
            _itemCount -= amount;
            if (entry.Amount == 0) _entries.Remove(key);

            return StorageMutationResult.Success;
        }

        public IReadOnlyList<StorageEntrySnapshot> GetEntriesSnapshot()
        {
            return _entries
                .OrderBy(pair => pair.Key)
                .Select(pair => new StorageEntrySnapshot(
                    pair.Key,
                    pair.Value.Amount,
                    pair.Value.CanonicalIdentity,
                    pair.Value.SerializedItemSnapshot))
                .ToArray();
        }

        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left, right)) return true;
            if (left == null || right == null || left.Length != right.Length) return false;

            for (int i = 0; i < left.Length; i++)
            {
                if (left[i] != right[i]) return false;
            }

            return true;
        }

        private sealed class Entry
        {
            public byte[] CanonicalIdentity { get; }
            public byte[] SerializedItemSnapshot { get; }
            public int Amount { get; set; }

            public Entry(byte[] canonicalIdentity, byte[] serializedItemSnapshot, int amount)
            {
                CanonicalIdentity = (byte[])canonicalIdentity.Clone();
                SerializedItemSnapshot = (byte[])serializedItemSnapshot.Clone();
                Amount = amount;
            }
        }
    }
}
