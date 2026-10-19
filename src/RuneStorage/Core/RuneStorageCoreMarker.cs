using UnityEngine;

namespace RuneStorage
{
    internal sealed class RuneStorageCoreMarker : MonoBehaviour
    {
        private bool _loadIssueLogged;
        private int _lastLoggedTier;

        internal bool IsReadOnly { get; private set; }
        internal string StatusMessage { get; private set; }

        internal void SetReadOnly(string reason)
        {
            IsReadOnly = true;
            StatusMessage = reason;

            if (_loadIssueLogged) return;
            _loadIssueLogged = true;
            Plugin.Log.LogError($"RuneStorage Core '{name}' is read-only: {reason}");
        }

        internal void ClearReadOnly()
        {
            IsReadOnly = false;
            StatusMessage = null;
            _loadIssueLogged = false;
        }

        internal bool ShouldLogTier(int tier)
        {
            if (_lastLoggedTier == tier) return false;
            _lastLoggedTier = tier;
            return true;
        }
    }
}
