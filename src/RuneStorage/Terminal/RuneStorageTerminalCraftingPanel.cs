using System;
using System.Collections.Generic;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace RuneStorage
{
    internal static partial class RuneStorageTerminalPanel
    {
        private const float CraftStationWidth = 250f;
        private const float CraftStationX = PanelWidth - 34f - CraftStationWidth;
        private const float CraftStationTop = -244f;
        private const float CraftStationAreaHeight = PanelHeight - 344f;
        private const float CraftListWidth = PanelWidth - 68f - CraftStationWidth - 24f;
        private static GameObject _craftStationTabs;
        private static RectTransform _craftStationContent;
        private static Text _craftStationHeader;
        private static Button _craftOutputButton;
        private static List<RuneStorageCraftingStationGroup> _craftStationGroups = new List<RuneStorageCraftingStationGroup>();
        private static string _selectedCraftStationId;
        private static string _craftStationTabSignature;
        private static bool _craftOutputToCore;
        private static RuneStorageCoreMarker _pendingCraftCore;
        private static string _pendingCraftRequestId;

        private static void ResetCraftingPanelState()
        {
            _craftStationTabs = null;
            _craftStationContent = null;
            _craftStationHeader = null;
            _craftOutputButton = null;
            _craftStationGroups = new List<RuneStorageCraftingStationGroup>();
            _selectedCraftStationId = null;
            _craftStationTabSignature = null;
            _craftOutputToCore = false;
        }

        private static void BuildCraftControls(GUIManager gui)
        {
            _craftStationTabs = new GameObject("RuneStorageCraftingStations", typeof(RectTransform));
            _craftStationTabs.transform.SetParent(_panel.transform, false);
            SetRect(
                _craftStationTabs.GetComponent<RectTransform>(),
                Vector2.up,
                Vector2.up,
                Vector2.up,
                new Vector2(CraftStationX, CraftStationTop),
                new Vector2(CraftStationWidth, CraftStationAreaHeight));
            _craftStationContent = _craftStationTabs.GetComponent<RectTransform>();

            _craftStationHeader = CreateText(gui, "RuneStorageCraftStationHeader", "CRAFTING TABLES", _panel.transform,
                Vector2.up, Vector2.up, new Vector2(CraftStationX, -205f), 16, AccentColor, CraftStationWidth, 34f, false);
            if (_craftStationHeader != null) _craftStationHeader.alignment = TextAnchor.MiddleLeft;

            _craftOutputButton = CreateToolbarButton(
                gui,
                "RuneStorageCraftOutputDestination",
                "Output: Player",
                new Vector2(514f, -205f),
                258f,
                38f,
                15,
                ToggleCraftOutputDestination);
            SetAutomaticNavigation(_craftOutputButton);
            _craftStationTabs.SetActive(false);
            if (_craftStationHeader != null) _craftStationHeader.gameObject.SetActive(false);
            _craftOutputButton.gameObject.SetActive(false);
        }

        private static void SetCraftControlsVisible(bool visible)
        {
            if (_craftStationTabs != null) _craftStationTabs.SetActive(visible);
            if (_craftStationHeader != null) _craftStationHeader.gameObject.SetActive(visible);
            if (_craftOutputButton != null) _craftOutputButton.gameObject.SetActive(visible);
            UpdateCraftOutputButton();
        }

        private static void SetItemListLayout(bool crafting)
        {
            if (_scrollView == null) return;
            float width = crafting ? CraftListWidth : PanelWidth - 68f;
            float height = crafting ? PanelHeight - 354f : PanelHeight - 334f;

            RectTransform rect = _scrollView.GetComponent<RectTransform>();
            SetRect(
                rect,
                Vector2.up,
                Vector2.up,
                Vector2.up,
                new Vector2(34f, crafting ? -251f : -244f),
                new Vector2(width, height));

            ScrollRect scrollRect = _scrollView.GetComponentInChildren<ScrollRect>(true);
            if (scrollRect == null) return;

            SetRect(
                scrollRect.GetComponent<RectTransform>(),
                Vector2.up,
                Vector2.up,
                Vector2.up,
                Vector2.zero,
                new Vector2(width, height));
            SetRect(
                scrollRect.viewport,
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                new Vector2(0f, 1f),
                Vector2.zero,
                new Vector2(width, height));

            ConfigureContent(scrollRect.content);
        }

        private static void SetSearchLayout(bool crafting)
        {
            if (_searchInput == null) return;
            SetRect(
                _searchInput.GetComponent<RectTransform>(),
                Vector2.up,
                Vector2.up,
                Vector2.up,
                new Vector2(34f, -205f),
                new Vector2(crafting ? 460f : 350f, 38f));
        }

        private static void ToggleCraftOutputDestination()
        {
            _craftOutputToCore = !_craftOutputToCore;
            UpdateCraftOutputButton();
            SetStatus(_craftOutputToCore
                ? "Crafted items will go to the connected Core."
                : "Crafted items will go to your inventory.");
            RefreshCraftRows();
        }

        private static void UpdateCraftOutputButton()
        {
            if (_craftOutputButton == null) return;
            _craftOutputButton.interactable = string.IsNullOrEmpty(_pendingCraftRequestId);
            SetButtonText(_craftOutputButton.gameObject, _craftOutputToCore ? "Output: Core" : "Output: Player");
            Text label = _craftOutputButton.GetComponentInChildren<Text>(true);
            if (label != null) label.color = _craftOutputToCore ? AccentColor : BodyTextColor;
        }

        private static void RefreshCraftRows()
        {
            if (_panel == null || _content == null || !_craftTabActive) return;

            if (!RuneStorageCoreAccess.TryResolve(_player, _terminal, out RuneStorageCoreMarker marker, out Container container, out string reason))
            {
                ClearRows();
                _craftStationGroups.Clear();
                RebuildCraftStationTabs(_craftStationGroups);
                _summaryText.text = "Core unavailable";
                _emptyText.text = "Crafting is unavailable until one valid Core is connected to this terminal.";
                _emptyText.gameObject.SetActive(true);
                SetStatus(reason);
                return;
            }

            if (marker.IsReadOnly)
            {
                ClearRows();
                _summaryText.text = "Core is read-only";
                _emptyText.text = "Crafting is disabled because the Core cannot safely accept changes.";
                _emptyText.gameObject.SetActive(true);
                SetStatus(marker.StatusMessage ?? "The Core is read-only. No ingredients can be used.");
                return;
            }

            if (ObjectDB.instance == null || ObjectDB.instance.m_recipes == null)
            {
                ClearRows();
                _summaryText.text = "Recipes unavailable";
                _emptyText.text = "Valheim's recipe list is not ready. Close and reopen the terminal to try again.";
                _emptyText.gameObject.SetActive(true);
                SetStatus("Valheim's recipe list is not ready yet.");
                return;
            }

            List<RuneStorageCraftingStationGroup> discoveredGroups;
            try
            {
                discoveredGroups = RuneStorageCrafting.GetStationGroups(_player, marker, _terminal);
                _craftStationGroups = discoveredGroups
                    .Where(group => !group.RequiresStation || group.NearbyLevel > 0)
                    .ToList();
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError($"RuneStorage recipe preview failed: {exception}");
                ClearRows();
                _summaryText.text = "Recipes unavailable";
                _emptyText.text = "RuneStorage could not read Valheim's recipes. Close and reopen the terminal to try again.";
                _emptyText.gameObject.SetActive(true);
                SetStatus("The recipe list could not be read safely.");
                return;
            }

            RebuildCraftStationTabs(_craftStationGroups);
            if (_craftStationGroups.Count == 0)
            {
                ClearRows();
                _selectedCraftStationId = null;
                bool waitingForStation = discoveredGroups.Any(group => group.RequiresStation);
                _summaryText.text = waitingForStation ? "No nearby crafting tables" : "No unlocked recipes";
                _emptyText.text = waitingForStation
                    ? "Move within crafting range of a table to see its recipes here."
                    : "No unlocked normal recipes are available for crafting.";
                _emptyText.gameObject.SetActive(true);
                SetStatus(waitingForStation
                    ? "Crafting tables appear in this list when detected nearby."
                    : "Unlock a normal recipe to see it here.");
                return;
            }

            RuneStorageCraftingStationGroup selected = _craftStationGroups.FirstOrDefault(group =>
                string.Equals(group.Identity, _selectedCraftStationId, StringComparison.Ordinal));
            if (selected == null)
            {
                selected = _craftStationGroups[0];
                _selectedCraftStationId = selected.Identity;
                UpdateCraftStationTabSelection();
            }

            try
            {
                RuneStorageCrafting.BuildRecipeViews(_player, container, selected, _craftOutputToCore);
            }
            catch (Exception exception)
            {
                Plugin.Log.LogError($"RuneStorage recipe preview failed: {exception}");
                ClearRows();
                _summaryText.text = "Recipes unavailable";
                _emptyText.text = "RuneStorage could not prepare this station's recipes. Close and reopen the terminal to try again.";
                _emptyText.gameObject.SetActive(true);
                SetStatus("The selected station's recipes could not be prepared safely.");
                return;
            }

            _summaryText.text = selected.RequiresStation
                ? selected.NearbyLevel > 0
                    ? $"{selected.DisplayName}  |  Nearby station level {selected.NearbyLevel}  |  {selected.Recipes.Count} unlocked recipes"
                    : $"{selected.DisplayName}  |  No station in crafting range  |  {selected.Recipes.Count} unlocked recipes"
                : $"Hand crafting  |  No station required  |  {selected.Recipes.Count} unlocked recipes";

            if (!string.IsNullOrEmpty(_pendingCraftRequestId))
            {
                SetStatus("A craft request is still waiting for the Core owner. Check the inventories before requesting another craft.");
            }
            else if (string.IsNullOrEmpty(_statusMessage)
                || _statusMessage == "Reading unlocked recipes and nearby stations..."
                || _statusMessage == "Reading recipes for this station..."
                || _statusMessage == "Reading the Core inventory...")
            {
                SetStatus(container.IsOwner()
                    ? "Craft once per click. Ingredients come from the Core."
                    : "Craft requests are validated by the Core's network owner.");
            }

            string query = _searchInput != null ? _searchInput.text.Trim() : string.Empty;
            List<RuneStorageCraftingRecipe> visible = string.IsNullOrEmpty(query)
                ? selected.Recipes
                : selected.Recipes.Where(recipe => MatchesCraftSearch(recipe, query)).ToList();
            visible = visible.OrderBy(recipe => recipe.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(recipe => recipe.Identity, StringComparer.Ordinal)
                .ToList();

            ClearRows();
            if (visible.Count == 0)
            {
                _emptyText.text = selected.Recipes.Count == 0
                    ? "No unlocked recipes use this crafting station."
                    : "No recipes or ingredients match that search.";
                _emptyText.gameObject.SetActive(true);
                return;
            }

            _emptyText.gameObject.SetActive(false);
            for (int index = 0; index < visible.Count; index++)
            {
                CreateCraftRecipeRow(visible[index], index);
            }
        }

        private static bool MatchesCraftSearch(RuneStorageCraftingRecipe recipe, string query)
        {
            if (recipe.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || recipe.IngredientSummary.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || recipe.Identity.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            return recipe.Ingredients.Any(ingredient =>
                ingredient.DisplayName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0
                || ingredient.SharedName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static void RebuildCraftStationTabs(List<RuneStorageCraftingStationGroup> groups)
        {
            if (_craftStationContent == null) return;

            string signature = string.Join("|", groups.Select(group => group.Identity));
            if (string.Equals(signature, _craftStationTabSignature, StringComparison.Ordinal))
            {
                UpdateCraftStationTabSelection();
                return;
            }

            for (int index = _craftStationContent.childCount - 1; index >= 0; index--)
            {
                UnityEngine.Object.Destroy(_craftStationContent.GetChild(index).gameObject);
            }
            _craftStationTabSignature = signature;
            GUIManager gui = GUIManager.Instance;
            if (gui == null) return;

            int columns = groups.Count > 12 ? 2 : 1;
            int rows = groups.Count == 0 ? 0 : Mathf.CeilToInt(groups.Count / (float)columns);
            const float columnGap = 6f;
            const float rowGap = 5f;
            float tabWidth = (CraftStationWidth - columnGap * (columns - 1)) / columns;
            float tabHeight = rows == 0
                ? 0f
                : Mathf.Clamp((CraftStationAreaHeight - rowGap * (rows - 1)) / rows, 26f, 44f);

            for (int index = 0; index < groups.Count; index++)
            {
                RuneStorageCraftingStationGroup group = groups[index];
                string stationIdentity = group.Identity;
                int column = index / rows;
                int row = index % rows;
                GameObject buttonObject = gui.CreateButton(
                    group.DisplayName,
                    _craftStationContent,
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    Vector2.zero,
                    tabWidth,
                    tabHeight);
                buttonObject.name = "RuneStorageStationTab_" + stationIdentity;
                buttonObject.transform.SetParent(_craftStationContent, false);
                SetRect(
                    buttonObject.GetComponent<RectTransform>(),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(0f, 1f),
                    new Vector2(column * (tabWidth + columnGap), -row * (tabHeight + rowGap)),
                    new Vector2(tabWidth, tabHeight));

                Button button = buttonObject.GetComponent<Button>();
                if (button == null) continue;
                gui.ApplyButtonStyle(button, columns == 1 ? 15 : 13);
                SetAutomaticNavigation(button);
                SetButtonText(buttonObject, group.DisplayName);
                Text label = button.GetComponentInChildren<Text>(true);
                if (label != null)
                {
                    label.horizontalOverflow = HorizontalWrapMode.Wrap;
                    label.verticalOverflow = VerticalWrapMode.Truncate;
                }
                button.onClick.AddListener(() => SelectCraftStation(stationIdentity));
            }

            UpdateCraftStationTabSelection();
        }

        private static void SelectCraftStation(string stationIdentity)
        {
            _selectedCraftStationId = stationIdentity;
            UpdateCraftStationTabSelection();
            SetStatus("Reading recipes for this station...");
            RefreshCraftRows();
        }

        private static void UpdateCraftStationTabSelection()
        {
            if (_craftStationContent == null) return;
            foreach (Transform child in _craftStationContent)
            {
                Button button = child.GetComponent<Button>();
                if (button == null) continue;
                bool selected = string.Equals(child.name, "RuneStorageStationTab_" + _selectedCraftStationId, StringComparison.Ordinal);
                SetTabSelected(button, selected);
            }
        }

        private static void CreateCraftRecipeRow(RuneStorageCraftingRecipe recipe, int index)
        {
            string details = recipe.IngredientSummary;
            if (!recipe.IsSupported || !recipe.OutputAvailable)
            {
                details += "   |   " + recipe.DisabledReason;
            }

            int lines = Mathf.Clamp((details.Length + 69) / 70, 1, 4);
            float height = 51f + lines * 18f;
            GameObject row = new GameObject("RuneStorageCraftRecipeRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(_content, false);
            LayoutElement rowSize = row.GetComponent<LayoutElement>();
            rowSize.minHeight = height;
            rowSize.preferredHeight = height;
            rowSize.flexibleHeight = 0f;
            row.GetComponent<Image>().color = index % 2 == 0 ? RowColor : AlternateRowColor;

            GameObject iconObject = new GameObject("RecipeIcon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(row.transform, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            SetRect(iconRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(42f, 42f));
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = recipe.Icon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = recipe.Icon != null;

            GUIManager gui = GUIManager.Instance;
            CreateRowText(gui, row.transform, "RecipeName", $"{recipe.DisplayName} x{recipe.OutputAmount}",
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(64f, -6f), 17, BodyTextColor, CraftListWidth - 180f, 25f, TextAnchor.MiddleLeft);
            CreateRowText(gui, row.transform, "RecipeRequirements", details,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(64f, -31f), 14, BodyTextColor, CraftListWidth - 180f, lines * 18f, TextAnchor.UpperLeft);

            GameObject craftButtonObject = gui.CreateButton(
                "Craft",
                row.transform,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-16f, 0f),
                92f,
                42f);
            craftButtonObject.transform.SetParent(row.transform, false);
            SetRect(
                craftButtonObject.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-16f, 0f),
                new Vector2(92f, 42f));
            craftButtonObject.name = "RuneStorageCraftOne";
            SetButtonText(craftButtonObject, "Craft");
            Button craftButton = craftButtonObject.GetComponent<Button>();
            if (craftButton != null)
            {
                gui.ApplyButtonStyle(craftButton, 15);
                SetAutomaticNavigation(craftButton);
                craftButton.interactable = recipe.CanCraft && string.IsNullOrEmpty(_pendingCraftRequestId);
                string recipeIdentity = recipe.Identity;
                craftButton.onClick.AddListener(() => RequestCraft(recipeIdentity));
            }
        }

        private static void SetAutomaticNavigation(Selectable selectable)
        {
            if (selectable == null) return;
            Navigation navigation = selectable.navigation;
            navigation.mode = Navigation.Mode.Automatic;
            selectable.navigation = navigation;
        }

        private static void RequestCraft(string recipeIdentity)
        {
            if (!string.IsNullOrEmpty(_pendingCraftRequestId))
            {
                SetStatus("A previous craft request is still waiting for the Core owner. Check the inventories before requesting another craft.");
                return;
            }

            if (!RuneStorageCoreAccess.TryResolve(_player, _terminal, out RuneStorageCoreMarker marker, out Container container, out string reason))
            {
                SetStatus(reason);
                RefreshCraftRows();
                return;
            }

            if (marker.IsReadOnly)
            {
                SetStatus(marker.StatusMessage ?? "The Core is read-only. No ingredients can be used.");
                return;
            }

            RuneStorageCraftingStationGroup selected = _craftStationGroups.FirstOrDefault(group =>
                string.Equals(group.Identity, _selectedCraftStationId, StringComparison.Ordinal));
            RuneStorageCraftingRecipe preview = selected?.Recipes.FirstOrDefault(recipe =>
                string.Equals(recipe.Identity, recipeIdentity, StringComparison.Ordinal));
            if (preview == null || !preview.CanCraft)
            {
                SetStatus(preview?.DisabledReason ?? "That recipe changed. The list has been refreshed.");
                RefreshCraftRows();
                return;
            }

            string requestId = Guid.NewGuid().ToString("N");
            _pendingCraftCore = marker;
            _pendingCraftRequestId = requestId;
            UpdateCraftOutputButton();

            if (container.IsOwner())
            {
                SetStatus("Validating the recipe and Core ingredients...");
                string result = RuneStorageCrafting.Craft(_player, _terminal, marker, recipeIdentity, _craftOutputToCore);
                ReceiveCraftResult(marker, requestId, result);
                return;
            }

            if (!marker.SendCraftRequest(recipeIdentity, _craftOutputToCore, requestId))
            {
                _pendingCraftCore = null;
                _pendingCraftRequestId = null;
                UpdateCraftOutputButton();
                SetStatus("The Core's network request path is not ready. No ingredients were used.");
                return;
            }

            SetStatus("Sent the craft request to the Core owner. Waiting for validation...");
            RefreshCraftRows();
        }

        internal static void ReceiveCraftResult(RuneStorageCoreMarker marker, string requestId, string result)
        {
            if (marker == null || marker != _pendingCraftCore
                || !string.Equals(requestId, _pendingCraftRequestId, StringComparison.Ordinal))
            {
                return;
            }

            _pendingCraftCore = null;
            _pendingCraftRequestId = null;
            UpdateCraftOutputButton();
            SetStatus(result);
            if (_panel != null && _isOpen) RefreshRows();
        }
    }
}
