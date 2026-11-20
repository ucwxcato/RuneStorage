using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Jotunn.Managers;
using UnityEngine;
using UnityEngine.UI;

namespace RuneStorage
{
    internal static partial class RuneStorageTerminalPanel
    {
        private enum StoredSortField
        {
            Name,
            Amount
        }

        private const float PanelWidth = 1100f;
        private const float PanelHeight = 780f;
        private const int GridColumns = 4;
        private const float TileWidth = 236f;
        private const float TileHeight = 116f;
        private const float TileSpacing = 8f;
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
        private static Button _craftTabButton;
        private static Button _depositEverythingButton;
        private static Button _viewToggleButton;
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
        private static bool _craftTabActive;
        private static bool _gridView;
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
            _craftTabButton = null;
            _depositEverythingButton = null;
            _viewToggleButton = null;
            _sortLabel = null;
            _sortByNameButton = null;
            _sortByAmountButton = null;
            _sortDirectionButton = null;
            _storedSortField = StoredSortField.Name;
            _sortAscending = true;
            _depositTabActive = false;
            _craftTabActive = false;
            _upgradeTabActive = false;
            _upgradeTabButton = null;
            _upgradeButton = null;
            ResetCraftingPanelState();

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
            // Status messages can wrap to two lines; the box is sized for that so the second line is not clipped.
            _statusText = CreateText(gui, "RuneStorageStatus", _statusMessage, _panel.transform,
                Vector2.up, Vector2.up, new Vector2(34f, -70f), 18, BodyTextColor, 960f, 46f, false);
            _summaryText = CreateText(gui, "RuneStorageSummary", "", _panel.transform,
                Vector2.up, Vector2.up, new Vector2(34f, -122f), 18, AccentColor, PanelWidth - 68f, 30f, false);

            _storedTabButton = CreateTabButton(gui, "Stored", new Vector2(34f, -159f), () => SelectTab(false));
            _depositTabButton = CreateTabButton(gui, "Deposit", new Vector2(150f, -159f), () => SelectTab(true));
            _craftTabButton = CreateTabButton(gui, "Craft", new Vector2(266f, -159f), SelectCraftTab);
            _viewToggleButton = CreateToolbarButton(gui, "RuneStorageViewToggle", "Grid view", new Vector2(PanelWidth - 34f - 150f, -159f), 150f, 38f, 16,
                ToggleItemView);

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
            BuildCraftControls(gui);
            BuildUpgradeControls(gui);

            _scrollView = gui.CreateScrollView(
                _panel.transform,
                false,
                true,
                0.22f,
                4f,
                gui.ValheimScrollbarHandleColorBlock,
                SurfaceColor,
                PanelWidth - 68f,
                PanelHeight - 334f);
            _scrollView.transform.SetParent(_panel.transform, false);
            _scrollView.name = "RuneStorageItemList";
            RectTransform scrollRectTransform = _scrollView.GetComponent<RectTransform>();
            SetRect(scrollRectTransform, Vector2.up, Vector2.up, Vector2.up, new Vector2(34f, -244f), new Vector2(PanelWidth - 68f, PanelHeight - 334f));

            ScrollRect scrollRect = _scrollView.GetComponentInChildren<ScrollRect>(true);
            if (scrollRect != null)
            {
                scrollRect.horizontal = false;
                scrollRect.vertical = true;
                scrollRect.scrollSensitivity = 600f;
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
                Vector2.up, Vector2.up, new Vector2(34f, -PanelHeight + 68f), 15, BodyTextColor, PanelWidth - 68f, 40f, false);

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

            ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ApplyContentLayoutMode(content);
        }

        // Grid view applies to the Stored, Deposit, and Craft lists. The Upgrade list keeps its list layout.
        private static bool IsItemGridActive()
        {
            return _gridView && !_upgradeTabActive;
        }

        // Unity allows one LayoutGroup per GameObject, so the content swaps its group rather than holding both.
        private static void ApplyContentLayoutMode(RectTransform content)
        {
            if (content == null) return;

            if (IsItemGridActive())
            {
                RemoveLayoutGroup<VerticalLayoutGroup>(content);
                GridLayoutGroup grid = content.GetComponent<GridLayoutGroup>();
                if (grid == null) grid = content.gameObject.AddComponent<GridLayoutGroup>();
                ConfigureGridLayout(grid);
            }
            else
            {
                RemoveLayoutGroup<GridLayoutGroup>(content);
                VerticalLayoutGroup layout = content.GetComponent<VerticalLayoutGroup>();
                if (layout == null) layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
                ConfigureVerticalLayout(layout);
            }
        }

        private static void RemoveLayoutGroup<T>(RectTransform content) where T : LayoutGroup
        {
            T existing = content.GetComponent<T>();
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
        }

        private static void ConfigureVerticalLayout(VerticalLayoutGroup layout)
        {
            layout.spacing = 5f;
            layout.padding = new RectOffset(5, 5, 5, 5);
            layout.childAlignment = TextAnchor.UpperCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        private static void ConfigureGridLayout(GridLayoutGroup grid)
        {
            // Craft tiles are taller for ingredient text and narrower because the station rail takes the right side.
            grid.cellSize = _craftTabActive ? new Vector2(CraftTileWidth, CraftTileHeight) : new Vector2(TileWidth, TileHeight);
            grid.spacing = new Vector2(TileSpacing, TileSpacing);
            grid.padding = new RectOffset(5, 5, 5, 5);
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = _craftTabActive ? CraftGridColumns : GridColumns;
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
            SetStatus(_craftTabActive ? "Reading unlocked recipes and nearby stations..." : "Reading the Core inventory...");
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
            RunTransfer(RuneStorageTransferKind.DepositAll, null, 0, -1, "Checking Core access and player inventory...");
        }

        private static void Withdraw(string identity, int amount, int shownCount)
        {
            if (amount <= 0)
            {
                SetStatus("Enter a whole-number amount greater than zero.");
                return;
            }

            RunTransfer(RuneStorageTransferKind.Withdraw, identity, amount, shownCount, "Checking Core access and available inventory space...");
        }

        private static void WithdrawCustom(string identity, int shownCount)
        {
            if (_amountInput == null || !int.TryParse(_amountInput.text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int amount) || amount <= 0)
            {
                SetStatus("Enter a whole-number amount greater than zero for Take X.");
                return;
            }

            Withdraw(identity, amount, shownCount);
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

        private static void DepositItem(string identity, int amount, bool allMatching = false)
        {
            if (!allMatching && amount <= 0)
            {
                SetStatus("Enter a whole-number amount greater than zero.");
                return;
            }

            RuneStorageTransferKind kind = allMatching ? RuneStorageTransferKind.DepositIdentity : RuneStorageTransferKind.DepositExact;
            RunTransfer(kind, identity, amount, -1, "Checking Core access and player inventory...");
        }

        private static void RunTransfer(RuneStorageTransferKind kind, string identity, int amount, int expectedQuantity, string progressMessage)
        {
            if (_player == null || _terminal == null)
            {
                SetStatus("The terminal or player is no longer available. Move closer and try again.");
                return;
            }

            SetStatus(progressMessage);
            string status = RuneStorageTransfers.Start(_player, _terminal, kind, identity, amount, expectedQuantity, out bool waiting);
            SetStatus(status);
            RefreshRows();
        }

        // Called by the Core marker when the Core owner answers a remote transfer request.
        internal static void ReceiveTransferStatus(RuneStorageCoreMarker marker, string status)
        {
            if (string.IsNullOrEmpty(status)) return;
            SetStatus(status);
            if (_panel != null && _isOpen) RefreshRows();
        }

        private static void SelectTab(bool deposit)
        {
            LeaveUpgradeTab();
            bool wasCrafting = _craftTabActive;
            _depositTabActive = deposit;
            _craftTabActive = false;
            SetTabSelected(_storedTabButton, !deposit);
            SetTabSelected(_depositTabButton, deposit);
            SetTabSelected(_craftTabButton, false);

            if (_depositEverythingButton != null)
            {
                _depositEverythingButton.gameObject.SetActive(deposit);
            }
            SetSortControlsVisible(!deposit && !_craftTabActive);
            SetCraftControlsVisible(false);
            SetViewToggleVisible(true);
            UpdateSortControls();

            if (_amountInput != null) _amountInput.gameObject.SetActive(true);
            if (_amountLabel != null) _amountLabel.gameObject.SetActive(true);
            SetItemListLayout(false);
            SetSearchLayout(false);
            if (wasCrafting && _searchInput != null) _searchInput.text = string.Empty;

            if (_amountLabel != null) _amountLabel.text = deposit ? "Deposit X" : "Withdraw X";
            if (_searchInput != null && _searchInput.placeholder is Text placeholder)
            {
                placeholder.text = deposit ? "Search carried items" : "Search stored items";
            }

            if (_helpText != null)
            {
                _helpText.text = deposit
                    ? "Deposit 1 or 5, enter an amount for Deposit X, or use Deposit all for that item group. Deposit Everything moves all unequipped items."
                    : "Choose Name or Amount to sort, then use the arrow for direction. Take 1 or 5, or enter an amount above for Take X.";
            }

            SetStatus("Reading the Core inventory...");
            RefreshRows();
        }

        private static void SelectCraftTab()
        {
            LeaveUpgradeTab();
            _depositTabActive = false;
            _craftTabActive = true;
            SetTabSelected(_storedTabButton, false);
            SetTabSelected(_depositTabButton, false);
            SetTabSelected(_craftTabButton, true);

            if (_depositEverythingButton != null) _depositEverythingButton.gameObject.SetActive(false);
            SetSortControlsVisible(false);
            SetViewToggleVisible(true);
            SetCraftControlsVisible(true);
            if (_amountInput != null) _amountInput.gameObject.SetActive(false);
            if (_amountLabel != null) _amountLabel.gameObject.SetActive(false);
            SetItemListLayout(true);
            SetSearchLayout(true);
            if (_searchInput != null) _searchInput.text = string.Empty;

            if (_searchInput != null && _searchInput.placeholder is Text placeholder)
            {
                placeholder.text = "Search recipes or ingredients";
            }

            if (_helpText != null)
            {
                _helpText.text = "Each click crafts one recipe using Core ingredients. A nearby station must meet the recipe's required level.";
            }

            SetStatus("Reading unlocked recipes and nearby stations...");
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

        private static void ToggleItemView()
        {
            _gridView = !_gridView;
            ApplyContentLayoutMode(_content);
            UpdateViewToggle();
            SetStatus(_gridView ? "Showing as a grid." : "Showing as a list.");
            RefreshRows();
        }

        private static void SetViewToggleVisible(bool visible)
        {
            if (_viewToggleButton == null) return;
            _viewToggleButton.gameObject.SetActive(visible);
            if (visible) UpdateViewToggle();
        }

        // The label names the view the button switches to, so the action is clear before it is taken.
        private static void UpdateViewToggle()
        {
            if (_viewToggleButton == null) return;
            SetButtonText(_viewToggleButton.gameObject, _gridView ? "List view" : "Grid view");
            _viewToggleButton.gameObject.name = _gridView ? "RuneStorageListViewToggle" : "RuneStorageGridViewToggle";
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
            if (_craftTabActive)
            {
                RefreshCraftRows();
                return;
            }

            if (_upgradeTabActive)
            {
                RefreshUpgradeView();
                return;
            }

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
            bool grid = IsItemGridActive();
            for (int index = 0; index < visibleGroups.Count; index++)
            {
                if (grid) CreateItemTile(visibleGroups[index]);
                else CreateItemRow(visibleGroups[index], index);
            }
        }

        // Tiles share one fill colour. A grid has no reading order to stripe, so alternating rows would only add noise.
        private static void CreateItemTile(RuneStorageItemGroup group)
        {
            GameObject tile = new GameObject("RuneStorageItemTile", typeof(RectTransform), typeof(Image));
            tile.transform.SetParent(_content, false);
            tile.GetComponent<Image>().color = RowColor;

            GameObject iconObject = new GameObject("ItemIcon", typeof(RectTransform), typeof(Image));
            iconObject.transform.SetParent(tile.transform, false);
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            SetRect(iconRect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -10f), new Vector2(40f, 40f));
            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = group.Icon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = group.Icon != null;

            GUIManager gui = GUIManager.Instance;
            CreateRowText(gui, tile.transform, "ItemName", group.DisplayName,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(58f, -8f), 15, BodyTextColor, 168f, 36f, TextAnchor.UpperLeft);
            string countLabel = $"{group.Count.ToString("N0", CultureInfo.InvariantCulture)} {(_depositTabActive ? "carried" : "stored")}";
            CreateRowText(gui, tile.transform, "ItemCount", countLabel,
                new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(58f, -44f), 14, AccentColor, 168f, 20f, TextAnchor.UpperLeft);

            string identity = group.Identity;
            if (_depositTabActive)
            {
                CreateTileActionButton(gui, tile.transform, "All", -84f, 50f, () => DepositItem(identity, 0, true));
                CreateTileActionButton(gui, tile.transform, "X", -28f, 50f, () => DepositCustom(identity));
                CreateTileActionButton(gui, tile.transform, "5", 28f, 50f, () => DepositItem(identity, 5));
                CreateTileActionButton(gui, tile.transform, "1", 84f, 50f, () => DepositItem(identity, 1));
            }
            else
            {
                int shownCount = (int)Math.Min(group.Count, int.MaxValue);
                CreateTileActionButton(gui, tile.transform, "Take 1", -70f, 64f, () => Withdraw(identity, 1, shownCount));
                CreateTileActionButton(gui, tile.transform, "Take 5", 0f, 64f, () => Withdraw(identity, 5, shownCount));
                CreateTileActionButton(gui, tile.transform, "Take X", 70f, 64f, () => WithdrawCustom(identity, shownCount));
            }
        }

        private static void CreateTileActionButton(GUIManager gui, Transform parent, string label, float centerX, float width, Action action)
        {
            CreateActionButton(gui, parent, label, new Vector2(0.5f, 0f), new Vector2(centerX, 12f), width, 34f, 14, action);
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
                new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(64f, 0f), 17, BodyTextColor, 370f, 46f, TextAnchor.MiddleLeft);
            CreateRowText(gui, row.transform, "ItemCount", $"{group.Count.ToString("N0", CultureInfo.InvariantCulture)} {(_depositTabActive ? "carried" : "stored")}",
                new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(_depositTabActive ? -454f : -302f, 0f), 16, AccentColor, 120f, 30f, TextAnchor.MiddleRight);

            string identity = group.Identity;
            if (_depositTabActive)
            {
                CreateRowActionButton(gui, row.transform, "Deposit all", 14f, 112f, () => DepositItem(identity, 0, true));
                CreateRowActionButton(gui, row.transform, "Deposit X", 134f, 96f, () => DepositCustom(identity));
                CreateRowActionButton(gui, row.transform, "Deposit 5", 238f, 96f, () => DepositItem(identity, 5));
                CreateRowActionButton(gui, row.transform, "Deposit 1", 342f, 96f, () => DepositItem(identity, 1));
            }
            else
            {
                int shownCount = (int)Math.Min(group.Count, int.MaxValue);
                CreateRowActionButton(gui, row.transform, "Take 1", 15f, 82f, () => Withdraw(identity, 1, shownCount));
                CreateRowActionButton(gui, row.transform, "Take 5", 105f, 82f, () => Withdraw(identity, 5, shownCount));
                CreateRowActionButton(gui, row.transform, "Take X", 195f, 82f, () => WithdrawCustom(identity, shownCount));
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
            CreateActionButton(gui, parent, label, new Vector2(1f, 0.5f), new Vector2(-rightOffset, 0f), width, 42f, 15, action);
        }

        // Anchor and pivot share one point, so position is measured from that anchor.
        private static Button CreateActionButton(
            GUIManager gui,
            Transform parent,
            string label,
            Vector2 anchor,
            Vector2 position,
            float width,
            float height,
            int fontSize,
            Action action)
        {
            GameObject buttonObject = gui.CreateButton(label, parent, anchor, anchor, position, width, height);
            buttonObject.transform.SetParent(parent, false);
            SetRect(buttonObject.GetComponent<RectTransform>(), anchor, anchor, anchor, position, new Vector2(width, height));
            SetButtonText(buttonObject, label);

            Button button = buttonObject.GetComponent<Button>();
            if (button == null) return null;

            gui.ApplyButtonStyle(button, fontSize);
            button.onClick.AddListener(() => action());
            return button;
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
