using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace RuneStorage
{
    internal sealed class RuneStorageCoreMarker : MonoBehaviour
    {
        private const string CraftRequestRpc = "RuneStorage_CraftRequest";
        private const string CraftResultRpc = "RuneStorage_CraftResult";
        private static readonly MethodInfo GetPeerMethod = AccessTools.Method(typeof(ZRoutedRpc), "GetPeer", new[] { typeof(long) });
        private bool _loadIssueLogged;
        private int _lastLoggedTier;
        private ZNetView _networkView;

        internal bool IsReadOnly { get; private set; }
        internal string StatusMessage { get; private set; }
        internal bool IsOwner => _networkView != null && _networkView.IsOwner();

        private void Start()
        {
            _networkView = GetComponent<ZNetView>();
            if (_networkView == null) _networkView = GetComponentInParent<ZNetView>();
            if (_networkView == null) _networkView = GetComponentInChildren<ZNetView>(true);
            if (_networkView == null)
            {
                Plugin.Log.LogWarning($"RuneStorage Core '{name}' has no ZNetView for crafting requests.");
                return;
            }

            _networkView.Register<string, bool, string>(CraftRequestRpc, ReceiveCraftRequest);
            _networkView.Register<string, string>(CraftResultRpc, ReceiveCraftResult);
        }

        private void OnDestroy()
        {
            if (_networkView == null) return;
            _networkView.Unregister(CraftRequestRpc);
            _networkView.Unregister(CraftResultRpc);
        }

        internal bool SendCraftRequest(string recipeIdentity, bool outputToCore, string requestId)
        {
            if (_networkView == null || string.IsNullOrEmpty(recipeIdentity) || string.IsNullOrEmpty(requestId)) return false;
            _networkView.InvokeRPC(CraftRequestRpc, recipeIdentity, outputToCore, requestId);
            return true;
        }

        private void ReceiveCraftRequest(long sender, string recipeIdentity, bool outputToCore, string requestId)
        {
            if (_networkView == null || !_networkView.IsOwner() || string.IsNullOrEmpty(requestId) || requestId.Length > 64) return;

            string result = "The craft request failed validation. No ingredients were used.";
            try
            {
                Player player = FindPlayerForPeer(sender);
                if (player == null)
                {
                    result = "The Core could not verify the requesting player. No ingredients were used.";
                }
                else if (!TryFindTerminalForPlayer(player, out RuneStorageTerminalMarker terminal, out string reason))
                {
                    result = reason;
                }
                else
                {
                    Container container = GetComponent<Container>();
                    if (container == null) container = GetComponentInChildren<Container>(true);
                    result = container == null
                        ? "The Core's native inventory could not be found. No ingredients were used."
                        : RuneStorageCrafting.Craft(player, terminal, this, recipeIdentity, outputToCore);
                }
            }
            catch (System.Exception exception)
            {
                Plugin.Log.LogError($"RuneStorage craft request validation failed: {exception}");
                result = "The craft request stopped during validation. Check both inventories before trying again.";
            }

            _networkView.InvokeRPC(sender, CraftResultRpc, requestId, result);
        }

        private void ReceiveCraftResult(long sender, string requestId, string result)
        {
            ZDO zdo = _networkView != null ? _networkView.GetZDO() : null;
            if (zdo == null || zdo.GetOwner() != sender) return;
            RuneStorageTerminalPanel.ReceiveCraftResult(this, requestId, result);
        }

        private bool TryFindTerminalForPlayer(Player player, out RuneStorageTerminalMarker terminal, out string reason)
        {
            terminal = null;
            var matches = new System.Collections.Generic.List<RuneStorageTerminalMarker>();
            foreach (RuneStorageTerminalMarker candidate in UnityEngine.Object.FindObjectsByType<RuneStorageTerminalMarker>(FindObjectsSortMode.None))
            {
                if (candidate == null || Vector3.Distance(player.transform.position, candidate.transform.position) > RuneStorageCoreAccess.TerminalUseDistance)
                {
                    continue;
                }

                if (!RuneStorageCoreAccess.TryResolve(player, candidate, out RuneStorageCoreMarker marker, out _, out _)) continue;
                if (marker == this) matches.Add(candidate);
            }

            if (matches.Count == 1)
            {
                terminal = matches[0];
                reason = null;
                return true;
            }

            reason = matches.Count == 0
                ? "Move within range of a RuneStorage Terminal connected to this Core."
                : "More than one RuneStorage Terminal is connected to this Core nearby. Use one terminal at a time.";
            return false;
        }

        private static Player FindPlayerForPeer(long peerId)
        {
            ZRoutedRpc routedRpc = ZRoutedRpc.instance;
            ZNetPeer peer = routedRpc != null && GetPeerMethod != null
                ? GetPeerMethod.Invoke(routedRpc, new object[] { peerId }) as ZNetPeer
                : null;
            if (peer == null || ZNetScene.instance == null) return null;

            GameObject character = ZNetScene.instance.FindInstance(peer.m_characterID);
            if (character == null) return null;
            Player player = character.GetComponent<Player>();
            return player != null ? player : character.GetComponentInChildren<Player>(true);
        }

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
