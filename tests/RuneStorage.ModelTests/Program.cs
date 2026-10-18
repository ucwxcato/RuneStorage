using System;
using System.Linq;
using RuneStorage.Storage;

internal static class Program
{
    private static int Main()
    {
        try
        {
            StackableEntriesAggregateAndRespectItemLimit();
            DistinctKeysRespectKeyLimit();
            FailedMutationsDoNotChangeLedger();
            WithdrawalsAreExactAndRemoveEmptyKeys();
            EntrySnapshotsAreDeterministicAndDefensive();
            Console.WriteLine("RuneStorage ledger model tests passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void StackableEntriesAggregateAndRespectItemLimit()
    {
        var ledger = new StorageLedger();
        byte[] identity = { 4, 8, 15, 16, 23, 42 };
        byte[] serializedItem = { 33, 34, 35 };
        byte[] equivalentSerializedItem = { 35, 34, 33 };
        StorageKey key = CreateKey("Wood", identity);

        Equal(StorageMutationResult.Success, ledger.TryAdd(key, identity, serializedItem, 12000));
        Equal(StorageMutationResult.Success, ledger.TryAdd(key, identity, equivalentSerializedItem, 8000));
        Equal(20000, ledger.ItemCount);
        Equal(1, ledger.KeyCount);
        SequenceEqual(serializedItem, ledger.GetEntriesSnapshot()[0].SerializedItemSnapshot);
        Equal(StorageMutationResult.ItemCapacityExceeded, ledger.TryAdd(key, identity, serializedItem, 1));
        Equal(20000, ledger.ItemCount);
    }

    private static void DistinctKeysRespectKeyLimit()
    {
        var ledger = new StorageLedger();
        for (int i = 0; i < StorageLedger.StageOneKeyCapacity; i++)
        {
            byte[] identity = BitConverter.GetBytes(i);
            Equal(StorageMutationResult.Success, ledger.TryAdd(
                CreateKey("Item" + i, identity), identity, identity, 1));
        }

        Equal(StorageLedger.StageOneKeyCapacity, ledger.KeyCount);
        byte[] overflow = { 1 };
        Equal(StorageMutationResult.KeyCapacityExceeded, ledger.TryAdd(
            CreateKey("Overflow", overflow), overflow, overflow, 1));
        Equal(StorageLedger.StageOneKeyCapacity, ledger.KeyCount);
        Equal(StorageLedger.StageOneKeyCapacity, ledger.ItemCount);
    }

    private static void FailedMutationsDoNotChangeLedger()
    {
        var ledger = new StorageLedger(itemCapacity: 10, keyCapacity: 2);
        byte[] identity = { 1, 2, 3 };
        byte[] serializedItem = { 4, 5, 6 };
        StorageKey key = CreateKey("Stone", identity);

        Equal(StorageMutationResult.InvalidAmount, ledger.TryAdd(key, identity, serializedItem, 0));
        Equal(StorageMutationResult.InvalidSnapshot, ledger.TryAdd(key, new byte[] { 9 }, serializedItem, 1));
        Equal(StorageMutationResult.Success, ledger.TryAdd(key, identity, serializedItem, 4));
        Equal(StorageMutationResult.ItemCapacityExceeded, ledger.TryAdd(key, identity, serializedItem, 7));
        Equal(4, ledger.ItemCount);
        Equal(1, ledger.KeyCount);
    }

    private static void WithdrawalsAreExactAndRemoveEmptyKeys()
    {
        var ledger = new StorageLedger();
        byte[] identity = { 7, 7, 7 };
        byte[] serializedItem = { 8, 8, 8 };
        StorageKey key = CreateKey("FineWood", identity);
        Equal(StorageMutationResult.Success, ledger.TryAdd(key, identity, serializedItem, 50));
        Equal(StorageMutationResult.InsufficientStoredAmount, ledger.TryRemove(key, 51, out _));
        Equal(50, ledger.ItemCount);
        Equal(StorageMutationResult.Success, ledger.TryRemove(key, 20, out byte[] returned));
        SequenceEqual(serializedItem, returned);
        Equal(30, ledger.ItemCount);
        Equal(1, ledger.KeyCount);
        Equal(StorageMutationResult.Success, ledger.TryRemove(key, 30, out _));
        Equal(0, ledger.ItemCount);
        Equal(0, ledger.KeyCount);
        Equal(StorageMutationResult.ItemNotFound, ledger.TryRemove(key, 1, out _));
    }

    private static void EntrySnapshotsAreDeterministicAndDefensive()
    {
        var ledger = new StorageLedger();
        byte[] zIdentity = { 9 };
        byte[] aIdentity = { 1 };
        byte[] zSnapshot = { 19 };
        byte[] aSnapshot = { 11 };
        Equal(StorageMutationResult.Success, ledger.TryAdd(CreateKey("Zulu", zIdentity), zIdentity, zSnapshot, 1));
        Equal(StorageMutationResult.Success, ledger.TryAdd(CreateKey("Ash", aIdentity), aIdentity, aSnapshot, 2));

        var entries = ledger.GetEntriesSnapshot();
        Equal("Ash", entries[0].Key.PrefabName);
        Equal("Zulu", entries[1].Key.PrefabName);
        byte[] leakedSnapshot = entries[0].SerializedItemSnapshot;
        leakedSnapshot[0] = 99;
        SequenceEqual(aSnapshot, ledger.GetEntriesSnapshot()[0].SerializedItemSnapshot);
    }

    private static StorageKey CreateKey(string prefabName, byte[] snapshot)
    {
        if (!StorageKey.TryCreate(prefabName, snapshot, out StorageKey key))
        {
            throw new InvalidOperationException("Test fixture key creation failed.");
        }

        return key;
    }

    private static void Equal<T>(T expected, T actual)
    {
        if (!Equals(expected, actual))
        {
            throw new InvalidOperationException("Expected " + expected + ", got " + actual + ".");
        }
    }

    private static void SequenceEqual(byte[] expected, byte[] actual)
    {
        if (actual == null || !expected.SequenceEqual(actual))
        {
            throw new InvalidOperationException("Byte sequences differ.");
        }
    }
}
