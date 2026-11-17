using System.Collections.Generic;

namespace RuneStorage
{
    // Per-Core operation guard shared by storage transfers and crafting. Valheim runs game logic on one
    // thread, so this is a re-entrancy guard: a second mutation for the same Core is refused while one is running.
    internal static class RuneStorageTransactions
    {
        private static readonly HashSet<int> ActiveCoreOperations = new HashSet<int>();

        internal static bool TryBeginCoreOperation(RuneStorageCoreMarker marker, out int operationId)
        {
            operationId = marker != null ? marker.GetInstanceID() : 0;
            if (marker == null) return false;

            lock (ActiveCoreOperations)
            {
                return ActiveCoreOperations.Add(operationId);
            }
        }

        internal static void EndCoreOperation(int operationId)
        {
            lock (ActiveCoreOperations)
            {
                ActiveCoreOperations.Remove(operationId);
            }
        }
    }
}
