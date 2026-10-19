using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace RuneStorage
{
    internal static class RuneStorageTerminalPanel
    {
        private enum StoredSortField
        {
            Name,
            Amount
        }

        private const float PanelWidth = 980f;
        private const float PanelHeight = 710f;
        private static readonly Color SurfaceColor = new Color(0.13f, 0.09f, 0.05f, 1f);
        private static readonly Color RowColor = new Color(0.19f, 0.13f, 0.07f, 1f);
        private static readonly Color AlternateRowColor = new Color(0.16f, 0.11f, 0.06f, 1f);
        private static readonly Color BodyTextColor = new Color(0.96f, 0.92f, 0.82f, 1f);
        private static readonly Color AccentColor = new Color(0.91f, 0.70f, 0.30f, 1f);

        private static GameObject _panel;
        private static GameObject _scrollView;
        private static RectTransform _content;
        private static InputField _searchInput;
        private static InputField _amountInput;
        private static Text _amountLabel;
        private static Text _statusText;
        private static Text _summaryText;
        private static Text _emptyText;
        private static Text _helpText;
        private static Button _storedTabButton;
        private static Button _depositTabButton;
        private static Button _depositEverythingButton;
        private static Text _sortLabel;
        private static Button _sortByNameButton;
        private static Button _sortByAmountButton;
        private static Button _sortDirectionButton;
        private static StoredSortField _storedSortField = StoredSortField.Name;
        private static bool _sortAscending = true;
        private static Player _player;
        private static RuneStorageTerminalMarker _terminal;
        private static bool _isOpen;
        private static bool _depositTabActive;
        private static bool _restoreCursor;
        private static CursorLockMode _previousCursorLock;
        private static bool _previousCursorVisible;
        private static int _lastScreenWidth;
        private static int _lastScreenHeight;
        private static string _statusMessage = "Reading the Core inventory...";

        internal static void OnCustomGUIAvailable()
        {
            if (_isOpen) Close();
            _panel = null;
            _scrollView = null;
            _content = null;
            _searchInput = null;
            _amountInput = null;
            _amountLabel = null;
            _statusText = null;
            _summaryText = null;
            _emptyText = null;
            _helpText = null;
            _storedTabButton = null;
            _depositTabButton = null;
            _depositEverythingButton = null;
            _sortLabel = null;
            _sortByNameButton = null;
            _sortByAmountButton = null;
            _sortDirectionButton = null;
            _storedSortField = StoredSortField.Name;
            _sortAscending = true;
            _depositTabActive = false;

            GUIManager gui = GUIManager.Instance;
            if (gui == null || GUIManager.CustomGUIFront == null) return;

            Build(gui);
        }

        internal static void Open(Player player, RuneStorageTerminalMarker terminal)
        {
            _player = player != null ? player : Player.m_localPlayer;
            _terminal = terminal;
            _statusMessage = "Reading the Core inventory...";

            if (_panel == null)
            {
                GUIManager gui = GUIManager.Instance;
                if (gui == null || GUIManager.CustomGUIFront == null)
                {
                    _statusMessage = "RuneStorage's interface is not ready. Close the game menu and try again.";
                    return;
                }

                Build(gui);
            }

            if (_panel != null) Show();
        }

        internal static void Shutdown()
        {
            Close();
            if (_panel != null) UnityEngine.Object.Destroy(_panel);
            _panel = null;
        }

        internal static void HandleUpdate()
        {
            if (!_isOpen) return;

            if (_panel == null || _terminal == null || _player == null)
            {
                Close();
                return;
            }

            ApplyResolutionScale();

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Close();
                return;
            }

            if (Vector3.Distance(_player.transform.position, _terminal.transform.position) > RuneStorageCoreAccess.TerminalUseDistance + 1f)
            {
                SetStatus("You moved too far from the RuneStorage Terminal. The panel was closed.");
                Close();
            }
        }

        internal static void HandleRootDestroyed(GameObject root)
        {
            if (_panel != root) return;
            Close();
            _panel = null;
            _content = null;
        }

        private static void Build(GUIManager gui)
        {
            if (GUIManager.CustomGUIFront == null) return;

            _panel = gui.CreateWoodpanel(
                GUIManager.CustomGUIFront.transform,
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                PanelWidth,
                PanelHeight,
                false);
            _panel.name = "RuneStorageTerminalPanel";
            _panel.SetActive(false);
            _panel.AddComponent<RuneStoragePanelRuntime>();
            SetRect(
                _panel.GetComponent<RectTransform>(),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                new Vector2(0.5f, 0.5f),
                Vector2.zero,
                new Vector2(PanelWidth, PanelHeight));
            ApplyResolutionScale();

            CreateSurface(_panel.transform);
            CreateText(gui, "RuneStorageTitle", "RuneStorage", _panel.transform,
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -34f), 30, AccentColor, 760f, 42f, true);
            _statusText = CreateText(gui, "RuneStorageStatus", _statusMessage, _panel.transform,
                Vector2.up, Vector2.up, new Vector2(34f, -78f), 18, BodyTextColor, 900f, 30f, false);
            _summaryText = CreateText(gui, "RuneStorageSummary", "", _panel.transform,
                Vector2.up, Vector2.up, new Vector2(34f, -110f), 18, AccentColor, 900f, 30f, false);

            _storedTabButton = CreateTabButton(gui, "Stored", new Vector2(34f, -159f), () => SelectTab(false));
            _depositTabButton = CreateTabButton(gui, "Deposit", new Vector2(150f, -159f), () => SelectTab(true));

            GameObject searchObject = gui.CreateInputField(
                _panel.transform,
                Vector2.up,
                Vector2.up,
                new Vector2(34f, -205f),
                InputField.ContentType.Standard,
                "Search stored items",
                17,
                350f,
                38f);
            searchObject.transform.SetParent(_panel.transform, false);
            SetRect(
                searchObject.GetComponent<RectTransform>(),
                Vector2.up,
                Vector2.up,
                Vector2.up,
                new Vector2(34f, -205f),
                new Vector2(350f, 38f));
            searchObject.name = "RuneStorageSearch";
            _searchInput = searchObject.GetComponent<InputField>();
            if (_searchInput != null)
            {
                _searchInput.text = string.Empty;
                _searchInput.caretColor = BodyTextColor;
                _searchInput.selectionColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.45f);
                _searchInput.onValueChanged.AddListener(_ => RefreshRows());
            }

            _amountLabel = CreateText(gui, "RuneStorageAmountLabel", "Withdraw X", _panel.transform,
                Vector2.up, Vector2.up, new Vector2(408f, -205f), 16, BodyTextColor, 128f, 38f, false);
            GameObject amountObject = gui.CreateInputField(
                _panel.transform,
                Vector2.up,
                Vector2.up,
                new Vector2(550f, -205f),
                InputField.ContentType.IntegerNumber,
                "Amount",
                17,
                90f,
                38f);
            amountObject.transform.SetParent(_panel.transform, false);
            SetRect(
                amountObject.GetComponent<RectTransform>(),
                Vector2.up,
                Vector2.up,
                Vector2.up,
                new Vector2(550f, -205f),
                new Vector2(90f, 38f));
            amountObject.name = "RuneStorageAmount";
            _amountInput = amountObject.GetComponent<InputField>();
            if (_amountInput != null)
            {
                _amountInput.text = string.Empty;
                _amountInput.caretColor = BodyTextColor;
                _amountInput.selectionColor = new Color(AccentColor.r, AccentColor.g, AccentColor.b, 0.45f);
            }

            GameObject depositButtonObject = gui.CreateButton(
                "Deposit Everything",
                _panel.transform,
                Vector2.up,
                Vector2.up,
                new Vector2(700f, -205f),
                210f,
                42f);
            depositButtonObject.transform.SetParent(_panel.transform, false);
            SetRect(
                depositButtonObject.GetComponent<RectTransform>(),
                Vector2.up,
                Vector2.up,
                Vector2.up,
                new Vector2(700f, -205f),
                new Vector2(210f, 42f));
            depositButtonObject.name = "RuneStorageDepositAll";
            SetButtonText(depositButtonObject, "Deposit Everything");
            Button depositButton = depositButtonObject.GetComponent<Button>();
            if (depositButton != null)
            {
                gui.ApplyButtonStyle(depositButton, 15);
                depositButton.onClick.AddListener(DepositAll);
            }
            _depositEverythingButton = depositButton;
            if (depositButtonObject != null) depositButtonObject.SetActive(false);

            _sortLabel = CreateText(gui, "RuneStorageSortLabel", "Sort", _panel.transform,
                Vector2.up, Vector2.up, new Vector2(654f, -205f), 15, BodyTextColor, 38f, 38f, false);
            _sortByNameButton = CreateToolbarButton(gui, "RuneStorageSortByName", "Name", new Vector2(700f, -205f), 68f, 38f, 14,
                () => SelectSortField(StoredSortField.Name));
            _sortByAmountButton = CreateToolbarButton(gui, "RuneStorageSortByAmount", "Amount", new Vector2(776f, -205f), 84f, 38f, 14,
                () => SelectSortField(StoredSortField.Amount));
            _sortDirectionButton = CreateToolbarButton(gui, "RuneStorageSortDirection", "↑", new Vector2(868f, -205f), 56f, 38f, 19,
                ToggleSortDirection);
            if (_sortLabel != null) _sortLabel.gameObject.SetActive(false);
            SetSortControlsVisible(false);

            _scrollView = gui.CreateScrollView(
                _panel.transform,
                false,
                true,
                0.22f,
                4f,
                gui.ValheimScrollbarHandleColorBlock,
                SurfaceColor,
                912f,
                376f);
            _scrollView.transform.SetParent(_panel.transform, false);
            _scrollView.name = "RuneStorageItemList";
            RectTransform scrollRectTransform = _scrollView.GetComponent<RectTransform>();
            SetRect(scrollRectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(34f, -244f), new Vector2(912f, 376f));

            ScrollRect scrollRect = _scrollView.GetComponentInChildren<ScrollRect>(true);
            if (scrollRect != null)
            {
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.scrollSensitivity = 300f;
                _content = scrollRect.content;
                ConfigureContent(_content);
            }

            if (_content == null)
            {
                Plugin.Log.LogError("Jotunn's RuneStorage scroll view has no content RectTransform.");
            }

            _emptyText = CreateText(gui, "RuneStorageEmpty", "The Core is empty. Deposit items to list them here.", _scrollView.transform,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, 18, BodyTextColor, 700f, 56f, false);
            _emptyText.alignment = TextAnchor.MiddleCenter;
            _emptyText.gameObject.SetActive(false);

            _helpText = CreateText(gui, "RuneStorageHelp", "", _panel.transform,
                Vector2.up, Vector2.up, new Vector2(34f, -642f), 15, BodyTextColor, 912f, 30f, false);

            GameObject closeButtonObject = gui.CreateButton(
                "Close",
                _panel.transform,
                new Vector2(1f, 1f),
                new Vector2(1f, 1f),
                new Vector2(-74f, -38f),
                86f,
                36f);
            closeButtonObject.transform.SetParent(_panel.transform, false);
            SetRect(
                closeButtonObject.GetComponent<RectTransform>(),
                Vector2.one,
                Vector2.one,
                Vector2.one,
                new Vector2(-30f, -28f),
                new Vector2(86f, 36f));
            closeButtonObject.name = "RuneStorageClose";
            SetButtonText(closeButtonObject, "Close");
            Button closeButton = closeButtonObject.GetComponent<Button>();
            if (closeButton != null)
            {
                gui.ApplyButtonStyle(closeButton, 16);
                closeButton.onClick.AddListener(Close);
            }

            if (_searchInput != null && depositButton != null)
            {
                Navigation searchNavigation = _searchInput.navigation;
                searchNavigation.mode = Navigation.Mode.Automatic;
                _searchInput.navigation = searchNavigation;
                depositButton.navigation = Navigation.defaultNavigation;
            }

            SelectTab(false);

            RuneStoragePanelRuntime runtime = _panel.GetComponent<RuneStoragePanelRuntime>();
            if (runtime != null) runtime.Initialize(_panel);
        }

        private static void CreateSurface(Transform parent)
        {
            var surface = new GameObject("RuneStorageSurface", typeof(RectTransform), typeof(Image));
            surface.transform.SetParent(parent, false);
            RectTransform rect = surface.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(18f, 18f);
            rect.offsetMax = new Vector2(-18f, -64f);
            Image image = surface.GetComponent<Image>();
            image.color = SurfaceColor;
            image.raycastTarget = false;
        }

        private static Text CreateText(
            GUIManager gui,
            string name,
            string value,
            Transform parent,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 position,
            int fontSize,
            Color color,
            float width,
            float height,
            bool title)
        {
            Font font = title ? gui.NorseBold : gui.AveriaSerif;
            GameObject textObject = gui.CreateText(
                value,
                parent,
                anchorMin,
                anchorMax,
                position,
                font,
                fontSize,
                color,
                false,
                Color.clear,
                width,
                height,
                false);
            textObject.name = name;
            textObject.transform.SetParent(parent, false);
            SetRect(
                textObject.GetComponent<RectTransform>(),
                anchorMin,
                anchorMax,
                anchorMin,
                position,
                new Vector2(width, height));
            Text text = textObject.GetComponent<Text>();
            if (text != null)
            {
                text.color = color;
                text.raycastTarget = false;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
                if (title) text.alignment = TextAnchor.MiddleCenter;
            }

            return text;
        }

        private static void ConfigureContent(RectTransform content)
        {
            if (content == null) return;

            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = new Vector2(0f, 0f);

            VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
            if (layout == null) layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 5f;
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static void Show()
        {
            if (_panel == null) return;

            _previousCursorLock = Cursor.lockState;
            _previousCursorVisible = Cursor.visible;
            _restoreCursor = true;
            _isOpen = true;
            _panel.SetActive(true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            GUIManager.BlockInput(true);
            if (_searchInput != null)
            {
                _searchInput.Select();
                _searchInput.ActivateInputField();
            }
            SetStatus("Reading the Core inventory...");
            RefreshRows();
        }

        private static void Close()
        {
            bool wasOpen = _isOpen;
            _isOpen = false;
            if (_panel != null) _panel.SetActive(false);

            if (wasOpen)
            {
                GUIManager.BlockInput(false);
                if (_restoreCursor)
                {
                    Cursor.lockState = _previousCursorLock;
                    Cursor.visible = _previousCursorVisible;
                }
            }

            _restoreCursor = false;
            _player = null;
            _terminal = null;
        }

        private static void DepositAll()
        {
            SetStatus("Checking Core access and player inventory...");
            SetStatus(RuneStorageTransactions.DepositAll(_player, _terminal));
            RefreshRows();
        }

        private static void Withdraw(string identity, int amount)
        {
            if (amount <= 0)
            {
                SetStatus("Enter a whole-number amount greater than zero.");
                return;
            }

            SetStatus("Checking Core access and available inventory space...");
            SetStatus(RuneStorageTransactions.Withdraw(_player, _terminal, identity, amount));
            RefreshRows();
        }

        private static void WithdrawCustom(string identity)
        {
            if (_amountInput == null || !int.TryParse(_amountInput.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount) || amount <= 0)
            {
                SetStatus("Enter a whole-number amount greater than zero for Take X.");
                return;
            }

            Withdraw(identity, amount);
        }

        private static void DepositItem(string identity, int amount, bool allMatching = false)
        {
            if (!allMatching && amount <= 0)
            {
                SetStatus("Enter a whole-number amount greater than zero.");
                return;
            }

            SetStatus("Checking Core access and player inventory...");
            SetStatus(RuneStorageTransactions.Deposit(_player, _terminal, identity, amount, allMatching));
            RefreshRows();
        }

        private static void DepositCustom(string identity)
        {
            if (_amountInput == null || !int.TryParse(_amountInput.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount) || amount <= 0)
            {
                SetStatus("Enter a whole-number amount greater than zero for Deposit X.");
                return;
            }

            DepositItem(identity, amount);
        }

        private static void SelectTab(bool deposit)
        {
            _depositTabActive = deposit;
            SetTabSelected(_storedTabButton, !deposit);
            SetTabSelected(_depositTabButton, deposit);

            if (_depositEverythingButton != null)
            {
                _depositEverythingButton.gameObject.SetActive(deposit);
            }
            SetSortControlsVisible(!deposit);
            UpdateSortControls();

            if (_amountLabel != null) _amountLabel.text = deposit ? "Deposit X" : "Withdraw X";
            if (_searchInput != null && _searchInput.placeholder is Text placeholder)
            {
                placeholder.text = deposit ? "Search carried items" : "Search stored items";
            }

            if (_helpText != null)
            {
                _helpText.text = deposit
                    ? "Deposit 1 or 5, enter an amount for Deposit X, or use All for that item group. Deposit Everything moves all unequipped items."
                    : "Choose Name or Amount to sort, then use the arrow for direction. Take 1 or 5, or enter an amount above for Take X.";
            }

            SetStatus("Reading the Core inventory...");
            RefreshRows();
        }

        private static void SetTabSelected(Button button, bool selected)
        {
            if (button == null) return;

            ColorBlock colors = button.colors;
            colors.disabledColor = colors.normalColor;
            button.colors = colors;
            button.interactable = !selected;

            Text label = button.GetComponentInChildren<Text>(true);
            if (label != null) label.color = selected ? AccentColor : BodyTextColor;
        }

        private static Button CreateToolbarButton(
            GUIManager gui,
            string name,
            string label,
            Vector2 position,
            float width,
            float height,
            int fontSize,
            Action action)
        {
            GameObject buttonObject = gui.CreateButton(label, _panel.transform, Vector2.up, Vector2.up, position, width, height);
            buttonObject.transform.SetParent(_panel.transform, false);
            SetRect(buttonObject.GetComponent<RectTransform>(), Vector2.up, Vector2.up, Vector2.up, position, new Vector2(width, height));
            buttonObject.name = name;

            Button button = buttonObject.GetComponent<Button>();
            if (button != null)
            {
                gui.ApplyButtonStyle(button, fontSize);
                SetButtonText(buttonObject, label);
                button.onClick.AddListener(() => action());
            }

            return button;
        }

        private static void SetSortControlsVisible(bool visible)
        {
            if (_sortLabel != null) _sortLabel.gameObject.SetActive(visible);
            if (_sortByNameButton != null) _sortByNameButton.gameObject.SetActive(visible);
            if (_sortByAmountButton != null) _sortByAmountButton.gameObject.SetActive(visible);
            if (_sortDirectionButton != null) _sortDirectionButton.gameObject.SetActive(visible);
        }

        private static void SelectSortField(StoredSortField field)
        {
            if (_storedSortField == field) return;

            _storedSortField = field;
            _sortAscending = true;
            UpdateSortControls();
            SetStatus($"Stored items sorted by {GetSortFieldLabel()}, ascending.");
            RefreshRows();
        }

        private static void ToggleSortDirection()
        {
            _sortAscending = !_sortAscending;
            UpdateSortControls();
            SetStatus($"Stored items sorted by {GetSortFieldLabel()}, {(_sortAscending ? "ascending" : "descending")}.");
            RefreshRows();
        }

        private static void UpdateSortControls()
        {
            SetTabSelected(_sortByNameButton, _storedSortField == StoredSortField.Name);
            SetTabSelected(_sortByAmountButton, _storedSortField == StoredSortField.Amount);

            if (_sortDirectionButton != null)
            {
                string arrow = _sortAscending ? "↑" : "↓";
                SetButtonText(_sortDirectionButton.gameObject, arrow);
                _sortDirectionButton.gameObject.name = _sortAscending
                    ? "RuneStorageSortAscending"
                    : "RuneStorageSortDescending";
            }
        }

        private static string GetSortFieldLabel()
        {
            return _storedSortField == StoredSortField.Name ? "Name" : "Amount";
        }

        private static List<RuneStorageItemGroup> SortStoredGroups(List<RuneStorageItemGroup> groups)
        {
            if (_storedSortField == StoredSortField.Amount)
            {
                return _sortAscending
                    ? groups.OrderBy(group => group.Count)
                        .ThenBy(group => group.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(group => group.Identity, StringComparer.Ordinal)
                        .ToList()
                    : groups.OrderByDescending(group => group.Count)
                        .ThenBy(group => group.DisplayName, StringComparer.OrdinalIgnoreCase)
                        .ThenBy(group => group.Identity, StringComparer.Ordinal)
                        .ToList();
            }

            return _sortAscending
                ? groups.OrderBy(group => group.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(group => group.Identity, StringComparer.Ordinal)
                    .ToList()
                : groups.OrderByDescending(group => group.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(group => group.Identity, StringComparer.Ordinal)
                    .ToList();
        }

        private static void RefreshRows()
        {
            if (_panel == null || _content == null) return;

            if (!RuneStorageCoreAccess.TryResolve(_player, _terminal, out _, out Container container, out string reason))
            {
                ClearRows();
                _summaryText.text = "Core unavailable";
                _emptyText.text = "No item list is available until one valid Core is in range.";
                _emptyText.gameObject.SetActive(true);
                SetStatus(reason);
                return;
            }

            Inventory inventory = _depositTabActive ? _player.GetInventory() : container.GetInventory();
            if (inventory == null)
            {
                ClearRows();
                _summaryText.text = _depositTabActive ? "Player inventory unavailable" : "Core inventory unavailable";
                _emptyText.text = "The item list cannot be read right now. Close and reopen the terminal to try again.";
                _emptyText.gameObject.SetActive(true);
                return;
            }

            var allGroups = RuneStorageItemGroups.Build(inventory, null, _depositTabActive);
            string query = _searchInput != null ? _searchInput.text : string.Empty;
            var visibleGroups = string.IsNullOrWhiteSpace(query)
                ? allGroups
                : RuneStorageItemGroups.Build(inventory, query, _depositTabActive);
            if (!_depositTabActive) visibleGroups = SortStoredGroups(visibleGroups);
            ClearRows();

            int tier = GetTier(container);
            if (_depositTabActive)
            {
                long carriedItems = allGroups.Sum(group => group.Count);
                _summaryText.text = $"{allGroups.Count.ToString("N0", CultureInfo.InvariantCulture)} item groups  |  {carriedItems.ToString("N0", CultureInfo.InvariantCulture)} unequipped items available to deposit";
            }
            else
            {
                List<ItemDrop.ItemData> allItems = inventory.GetAllItems();
                long totalItems = allItems.Sum(item => (long)Math.Max(0, item.m_stack));
                int capacity = Core.CoreTierCapacity.TryGetSlotCount(tier, out int slots) ? slots : 0;
                _summaryText.text = $"Tier {tier}  |  {allItems.Count} / {capacity} occupied slots  |  {totalItems.ToString("N0", CultureInfo.InvariantCulture)} items";
            }

            if (string.IsNullOrEmpty(_statusMessage) || _statusMessage == "Reading the Core inventory...")
            {
                SetStatus(container.IsOwner()
                    ? (_depositTabActive
                        ? "Core ready. Choose Deposit 1, Deposit 5, Deposit X, or All on an item row."
                        : "Core ready. Choose Take 1, Take 5, or Take X on an item row.")
                    : "Core contents are read-only here because another peer owns it; transfers require the Core owner.");
            }

            if (visibleGroups.Count == 0)
            {
                _emptyText.text = _depositTabActive
                    ? (allGroups.Count == 0
                        ? "No unequipped items in your inventory to deposit."
                        : "No carried items match that search. Try another name or item ID.")
                    : (allGroups.Count == 0
                        ? "The Core is empty. Deposit items to list them here."
                        : "No stored items match that search. Try another name or item ID.");
                _emptyText.gameObject.SetActive(true);
                return;
            }

            _emptyText.gameObject.SetActive(false);
            for (int index = 0; index < visibleGroups.Count; index++)
            {
                CreateItemRow(visibleGroups[index], index);
            }
        }

        private static void CreateItemRow(RuneStorageItemGroup group, int index)
        {
            GameObject row = new GameObject("RuneStorageItemRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            row.transform.SetParent(_content, false);
            LayoutElement rowSize = row.GetComponent<LayoutElement>();
            rowSize.minHeight = 62f;
            rowSize.preferredHeight = 62f;
            rowSize.flexibleHeight = 0f;
            row.GetComponent<Image>().color = index % 2 == 0 ? RowColor : AlternateRowColor;

            GameObject iconObject = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(row.transform, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            SetRect(iconRect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f), new Vector2(42f, 42f));
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = group.Icon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = group.Icon != null;

            GUIManager gui = GUIManager.Instance;
            CreateRowText(gui, row.transform, "ItemName", group.DisplayName,
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(64f, 0f), 17, BodyTextColor, 390f, 46f, TextAnchor.MiddleLeft);
            CreateRowText(gui, row.transform, "ItemCount", $"{group.Count.ToString("N0", CultureInfo.InvariantCulture)} {(_depositTabActive ? "carried" : "stored")}",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(_depositTabActive ? -326f : -302f, 0f), 16, AccentColor, 120f, 30f, TextAnchor.MiddleRight);

            string identity = group.Identity;
            if (_depositTabActive)
            {
                CreateRowActionButton(gui, row.transform, "All", 14f, 68f, () => DepositItem(identity, 0, true));
                CreateRowActionButton(gui, row.transform, "Dep X", 90f, 68f, () => DepositCustom(identity));
                CreateRowActionButton(gui, row.transform, "Dep 5", 166f, 68f, () => DepositItem(identity, 5));
                CreateRowActionButton(gui, row.transform, "Dep 1", 242f, 68f, () => DepositItem(identity, 1));
            }
            else
            {
                CreateRowActionButton(gui, row.transform, "Take 1", 15f, 82f, () => Withdraw(identity, 1));
                CreateRowActionButton(gui, row.transform, "Take 5", 105f, 82f, () => Withdraw(identity, 5));
                CreateRowActionButton(gui, row.transform, "Take X", 195f, 82f, () => WithdrawCustom(identity));
            }
        }

        private static Button CreateTabButton(GUIManager gui, string label, Vector2 position, Action action)
        {
            GameObject buttonObject = gui.CreateButton(label, _panel.transform, Vector2.up, Vector2.up, position, 108f, 38f);
            buttonObject.transform.SetParent(_panel.transform, false);
            SetRect(buttonObject.GetComponent<RectTransform>(), Vector2.up, Vector2.up, Vector2.up, position, new Vector2(108f, 38f));
            buttonObject.name = $"RuneStorage{label}Tab";
            SetButtonText(buttonObject, label);

            Button button = buttonObject.GetComponent<Button>();
            if (button != null)
            {
                gui.ApplyButtonStyle(button, 16);
                button.onClick.AddListener(() => action());
            }

            return button;
        }

        private static void CreateRowActionButton(GUIManager gui, Transform parent, string label, float rightOffset, float width, Action action)
        {
            GameObject buttonObject = gui.CreateButton(
                label,
                parent,
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-rightOffset, 0f),
                width,
                42f);
            buttonObject.transform.SetParent(parent, false);
            SetRect(
                buttonObject.GetComponent<RectTransform>(),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(1f, 0.5f),
                new Vector2(-rightOffset, 0f),
                new Vector2(width, 42f));
            SetButtonText(buttonObject, label);

            Button button = buttonObject.GetComponent<Button>();
            if (button == null) return;

            gui.ApplyButtonStyle(button, 15);
            button.onClick.AddListener(() => action());
        }

        private static Text CreateRowText(
            GUIManager gui,
            Transform parent,
            string name,
            string value,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 position,
            int fontSize,
            Color color,
            float width,
            float height,
            TextAnchor alignment)
        {
            GameObject textObject = gui.CreateText(
                value,
                parent,
                anchorMin,
                anchorMax,
                position,
                gui.AveriaSerif,
                fontSize,
                color,
                false,
                Color.clear,
                width,
                height,
                false);
            textObject.name = name;
            textObject.transform.SetParent(parent, false);
            SetRect(
                textObject.GetComponent<RectTransform>(),
                anchorMin,
                anchorMax,
                anchorMin,
                position,
                new Vector2(width, height));
            Text text = textObject.GetComponent<Text>();
            if (text != null)
            {
                text.color = color;
                text.alignment = alignment;
                text.raycastTarget = false;
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                text.verticalOverflow = VerticalWrapMode.Truncate;
            }

            return text;
        }

        private static void ClearRows()
        {
            if (_content == null) return;
            for (int index = _content.childCount - 1; index >= 0; index--)
            {
                UnityEngine.Object.Destroy(_content.GetChild(index).gameObject);
            }
        }

        private static int GetTier(Container container)
        {
            ZNetView view = container.GetComponent<ZNetView>();
            if (view == null) view = container.GetComponentInParent<ZNetView>();
            ZDO zdo = view != null ? view.GetZDO() : null;
            if (zdo != null && zdo.GetInt(CoreTierRuntime.TierZdoKey, out int tier)) return tier;
            return CoreTierRuntime.InitialTier;
        }

        private static void SetStatus(string message)
        {
            _statusMessage = message ?? string.Empty;
            if (_statusText != null) _statusText.text = _statusMessage;
        }

        private static void SetButtonText(GameObject buttonObject, string value)
        {
            if (buttonObject == null) return;
            Text text = buttonObject.GetComponentInChildren<Text>(true);
            if (text == null) return;
            text.text = value;
            text.color = BodyTextColor;
            text.alignment = TextAnchor.MiddleCenter;
        }

        private static void SetRect(
            RectTransform rect,
            Vector2 anchorMin,
            Vector2 anchorMax,
            Vector2 pivot,
            Vector2 position,
            Vector2 size)
        {
            if (rect == null) return;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static void ApplyResolutionScale()
        {
            if (_panel == null) return;
            if (_lastScreenWidth == Screen.width && _lastScreenHeight == Screen.height) return;

            _lastScreenWidth = Screen.width;
            _lastScreenHeight = Screen.height;
            if (Screen.width <= 0 || Screen.height <= 0) return;

            float scale = Mathf.Min(1f, Screen.height / 900f, Screen.width / 1100f);
            scale = Mathf.Max(0.65f, scale);
            _panel.transform.localScale = Vector3.one * scale;
        }
    }

    internal sealed class RuneStoragePanelRuntime : MonoBehaviour
    {
        private GameObject _root;

        internal void Initialize(GameObject root)
        {
            _root = root;
        }

        private void Update()
        {
            RuneStorageTerminalPanel.HandleUpdate();
        }

        private void OnDestroy()
        {
            if (_root != null) RuneStorageTerminalPanel.HandleRootDestroyed(_root);
        }
    }
}
