using System;
using System.Collections.Generic;
using Jotunn.Managers;
using RuneStorage.Core;
using UnityEngine;
using UnityEngine.UI;

namespace RuneStorage
{
    internal static partial class RuneStorageTerminalPanel
    {
        private static bool _upgradeTabActive;
        private static Button _upgradeTabButton;
        private static Button _upgradeButton;
        private static Button _upgradeSourceButton;
        private static RuneStorageUpgradeSource _upgradeSource = RuneStorageUpgradeSource.Core;
        private static string _pendingUpgradeRequestId;
        private static Player _pendingUpgradePlayer;
        private static List<ItemDrop.ItemData> _pendingUpgradePayment;

        private static void BuildUpgradeControls(GUIManager gui)
        {
            _upgradeTabButton = CreateTabButton(gui, "Upgrade", new Vector2(382f, -159f), SelectUpgradeTab);
            _upgradeButton = CreateToolbarButton(gui, "RuneStorageUpgrade", "Upgrade", new Vector2(34f, -205f), 260f, 42f, 16, RequestUpgrade);
            _upgradeSourceButton = CreateToolbarButton(gui, "RuneStorageUpgradeSource", "Pay from: Core", new Vector2(310f, -205f), 260f, 42f, 15, ToggleUpgradeSource);
            if (_upgradeButton != null) _upgradeButton.gameObject.SetActive(false);
            if (_upgradeSourceButton != null) _upgradeSourceButton.gameObject.SetActive(false);
        }

        private static void SelectUpgradeTab()
        {
            _depositTabActive = false;
            _craftTabActive = false;
            _upgradeTabActive = true;
            SetTabSelected(_storedTabButton, false);
            SetTabSelected(_depositTabButton, false);
            SetTabSelected(_craftTabButton, false);
            SetTabSelected(_upgradeTabButton, true);

            if (_depositEverythingButton != null) _depositEverythingButton.gameObject.SetActive(false);
            SetSortControlsVisible(false);
            SetCraftControlsVisible(false);
            SetViewToggleVisible(false);
            if (_amountInput != null) _amountInput.gameObject.SetActive(false);
            if (_amountLabel != null) _amountLabel.gameObject.SetActive(false);
            if (_searchInput != null)
            {
                _searchInput.text = string.Empty;
                _searchInput.gameObject.SetActive(false);
            }

            if (_upgradeButton != null) _upgradeButton.gameObject.SetActive(true);
            if (_upgradeSourceButton != null) _upgradeSourceButton.gameObject.SetActive(true);
            SetItemListLayout(false);
            SetSearchLayout(false);

            if (_helpText != null)
            {
                _helpText.text = "Pay from the Core's stored items, or from your own inventory. Equipped items are never used. Nothing already stored is lost.";
            }

            SetStatus("Reading the Core's upgrade requirements...");
            RefreshRows();
        }

        // Called from the other tab selectors so the Upgrade controls and layout are put back.
        private static void LeaveUpgradeTab()
        {
            if (!_upgradeTabActive && (_upgradeButton == null || !_upgradeButton.gameObject.activeSelf)) return;

            _upgradeTabActive = false;
            SetTabSelected(_upgradeTabButton, false);
            if (_upgradeButton != null) _upgradeButton.gameObject.SetActive(false);
            if (_upgradeSourceButton != null) _upgradeSourceButton.gameObject.SetActive(false);
            if (_searchInput != null) _searchInput.gameObject.SetActive(true);
            if (_emptyText != null) _emptyText.rectTransform.sizeDelta = new Vector2(700f, 56f);
        }

        private static void ToggleUpgradeSource()
        {
            _upgradeSource = _upgradeSource == RuneStorageUpgradeSource.Core
                ? RuneStorageUpgradeSource.Player
                : RuneStorageUpgradeSource.Core;
            RefreshRows();
        }

        private static void RefreshUpgradeView()
        {
            if (!RuneStorageCoreAccess.TryResolve(_player, _terminal, out _, out Container container, out string reason))
            {
                ClearRows();
                _summaryText.text = "Core unavailable";
                _emptyText.rectTransform.sizeDelta = new Vector2(900f, 120f);
                _emptyText.text = reason ?? "No Core is available for upgrades.";
                _emptyText.gameObject.SetActive(true);
                if (_upgradeButton != null) _upgradeButton.interactable = false;
                return;
            }

            ClearRows();
            int tier = GetTier(container);
            int slots = Core.CoreTierCapacity.TryGetSlotCount(tier, out int currentSlots) ? currentSlots : 0;
            _summaryText.text = $"Tier {Core.CoreTierUpgrades.RomanNumeral(tier)}  |  {slots} slots";

            _emptyText.rectTransform.sizeDelta = new Vector2(900f, 260f);
            _emptyText.text = RuneStorageCoreUpgrade.Describe(container, _player, tier, _upgradeSource);
            _emptyText.gameObject.SetActive(true);

            bool canUpgrade = tier < Core.CoreTierUpgrades.MaxTier && string.IsNullOrEmpty(_pendingUpgradeRequestId);
            if (_upgradeButton != null)
            {
                string label = tier >= Core.CoreTierUpgrades.MaxTier
                    ? "Highest tier"
                    : $"Upgrade to Tier {Core.CoreTierUpgrades.RomanNumeral(tier + 1)}";
                SetButtonText(_upgradeButton.gameObject, label);
                _upgradeButton.interactable = canUpgrade;
            }

            if (_upgradeSourceButton != null)
            {
                SetButtonText(_upgradeSourceButton.gameObject, _upgradeSource == RuneStorageUpgradeSource.Player
                    ? "Pay from: my inventory"
                    : "Pay from: Core");
            }
        }

        private static void RequestUpgrade()
        {
            if (!_upgradeTabActive) return;
            if (_player == null || _terminal == null)
            {
                SetStatus("The terminal or player is no longer available. Move closer and try again.");
                return;
            }

            if (!RuneStorageCoreAccess.TryResolve(_player, _terminal, out RuneStorageCoreMarker marker, out Container container, out string reason))
            {
                SetStatus(reason);
                RefreshRows();
                return;
            }

            if (!string.IsNullOrEmpty(_pendingUpgradeRequestId))
            {
                SetStatus("An upgrade is already waiting for the Core owner.");
                return;
            }

            if (container.IsOwner())
            {
                SetStatus("Checking the station and the ingredients...");
                string message = RuneStorageCoreUpgrade.Upgrade(_player, _terminal, container, marker, _upgradeSource, null, out _);
                SetStatus(message);
                RefreshRows();
                return;
            }

            int tier = GetTier(container);
            if (!Core.CoreTierUpgrades.IsNextTier(tier, tier + 1)
                || !Core.CoreTierUpgrades.TryGetUpgrade(tier + 1, out string station, out TierUpgradeRequirement[] requirements))
            {
                SetStatus("This Core has no upgrade available. Nothing was used.");
                RefreshRows();
                return;
            }

            // Player-paid: check the station and the items here first, then take the exact ingredients out of the
            // inventory and send them. They come back if the Core owner refuses.
            List<ItemDrop.ItemData> payment = null;
            string payload = string.Empty;
            if (_upgradeSource == RuneStorageUpgradeSource.Player)
            {
                if (!RuneStorageCrafting.IsStationNearbyNamed(marker, _terminal, station))
                {
                    SetStatus($"A nearby {station} is required for Tier {Core.CoreTierUpgrades.RomanNumeral(tier + 1)}. No items were used.");
                    RefreshRows();
                    return;
                }

                Inventory playerInventory = _player.GetInventory();
                if (!RuneStorageCoreUpgrade.HasPayment(playerInventory, requirements))
                {
                    SetStatus("Your inventory does not hold the full cost. Equipped items are not used. No items were used.");
                    RefreshRows();
                    return;
                }

                payment = RuneStorageCoreUpgrade.TakePayment(playerInventory, requirements);
                payload = RuneStorageTransfers.SerializeItems(payment);
                if (payload == null)
                {
                    RefundPayment(_player, payment);
                    SetStatus("These ingredients could not be packaged for the Core owner. Nothing was used.");
                    RefreshRows();
                    return;
                }
            }

            string requestId = Guid.NewGuid().ToString("N");
            if (!marker.SendUpgradeRequest(requestId, _upgradeSource == RuneStorageUpgradeSource.Player, payload))
            {
                RefundPayment(_player, payment);
                SetStatus("The Core's network request path is not ready. Nothing was upgraded.");
                RefreshRows();
                return;
            }

            _pendingUpgradeRequestId = requestId;
            _pendingUpgradePlayer = _player;
            _pendingUpgradePayment = payment;
            SetStatus("Sent the upgrade to the Core owner. Waiting for confirmation...");
            RefreshRows();
        }

        // Called by the Core marker when the Core owner answers a remote upgrade request.
        internal static void ReceiveUpgradeStatus(RuneStorageCoreMarker marker, string requestId, bool ok, string message)
        {
            if (string.IsNullOrEmpty(requestId) || !string.Equals(requestId, _pendingUpgradeRequestId, StringComparison.Ordinal)) return;

            Player player = _pendingUpgradePlayer;
            List<ItemDrop.ItemData> payment = _pendingUpgradePayment;
            _pendingUpgradeRequestId = null;
            _pendingUpgradePlayer = null;
            _pendingUpgradePayment = null;

            if (!ok && payment != null && player != null)
            {
                RefundPayment(player, payment);
                message = message + " Your ingredients were returned to your inventory.";
            }

            SetStatus(message);
            if (_panel != null && _isOpen) RefreshRows();
        }

        private static void RefundPayment(Player player, List<ItemDrop.ItemData> payment)
        {
            if (player == null || payment == null || payment.Count == 0) return;

            Inventory inventory = player.GetInventory();
            if (inventory == null) return;

            Vector3 dropAt = player.transform.position + player.transform.forward + Vector3.up * 0.5f;
            RuneStorageTransfers.PlaceCopies(inventory, payment, dropAt);
        }
    }
}
