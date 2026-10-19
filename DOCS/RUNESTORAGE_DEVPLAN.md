# RuneStorage - Development Plan

> **Status:** In progress. The previous Unity terminal visual, Jotunn registration, placement ghost, and placed transform were verified as of 2026-10-18. On 2026-10-19, the replacement `Terminal2_Visual` prefab, 256x256 icon, and `runestorage_terminal` AssetBundle were rebuilt. The user reported that the new terminal ghost was offset although the placed visual aligned. The visual is now centered against the build origin during piece setup, before Jötunn registers it, so the preview does not depend on the runtime anchor executing. Release build and capacity tests pass, and the updated DLL is deployed to `VHouseholds-Admin`; the user confirmed the revised ghost and placed appearance look correct in-game on 2026-10-19. The Core visual prefab, 256x256 icon, AssetBundle, Jötunn registration, Gale deployment, ghost, and placed transform are verified as of 2026-10-18. The Core retains its native `Container`, configures Tier I as 16x16, applies the saved tier before native inventory loading, and blocks the Core's inherited chest UI. The terminal has Stored and Deposit tabs, per-item Take/Deposit 1/5/X actions, exact-group Deposit All, deposit-everything, searchable metadata-safe aggregation, stored-item sorting by name or amount in either direction, and faster scrolling. The user confirmed the UI and normal deposits/withdrawals work well, and reported the stored items remained after a restart. Terminal/Core Hammer recipes are configured at the locked costs; in-game recipe visibility remains to be verified. **Clarified product goal (2026-10-19):** crafting from Core-held items is a core feature, through separate tabs for nearby crafting-station types (such as Workbench and Forge) that honor each station's effective upgrade level. This is planned but not implemented or runtime-verified. Release build and capacity tests pass; broader boundary, lifecycle, crafting, and multiplayer tests remain open.
>
> **Purpose:** Add a Valheim digital-storage system where a buildable runestone terminal presents the nearby Core's native inventory and crafts from its stored ingredients using recipes unlocked by nearby, correctly upgraded crafting stations.
>
> **Authority:** This is the canonical implementation plan for the RuneStorage plugin. It owns the RuneStorage pieces, Core inventory and tier capacity, terminal interaction, transactions, packaging, and test gates.
>
> **Target:** Valheim with BepInEx and Jotunn, tested through the active Gale profile:
> `C:\Users\magni\AppData\Roaming\com.kesomannen.gale\valheim\profiles\VHouseholds-Admin`

## 0. Outcome

The player places a RuneStorage Terminal and one RuneStorage Core within range. The Core can be upgraded in place to increase its slot capacity. Interacting with the terminal opens searchable storage and crafting views backed by the Core's native Valheim `Container` inventory. The player can deposit items, search and sort stored totals, withdraw exact quantities, and craft recipes using ingredients held in the Core when the required nearby crafting station is available.

**Crafting scope lock (2026-10-19):** The first implementation crafts one normal recipe per click from Core ingredients. Output goes to the player's inventory by default, with a terminal toggle to send it to the Core. A typed `Craft X` amount and sequential batch queue are deferred until the one-craft path works and is verified in-game.

The intended happy path is:

```text
Build terminal + core -> core is detected nearby -> deposit items -> terminal aggregates matching stacks
-> open terminal -> search/select an item -> withdraw an amount
-> approach a required crafting station -> open the terminal's crafting view -> craft using Core ingredients
-> save and reload -> remaining and crafted items persist exactly once
```

The MVP is complete only when storage and Core-backed crafting work with the supported capacity, the terminal refuses invalid Core/station states, and transaction tests show no duplication or silent item loss.

## 1. Locked decisions

These decisions define the MVP. They may be revisited only if implementation evidence shows that a decision is incompatible with Valheim or Jotunn.

- The mod name is **RuneStorage**.
- The plugin identity is `com.runestorage.valheim`, assembly `RuneStorage`, version `0.1.3` for the initial Core build-piece integration.
- The terminal AssetBundle is `runestorage_terminal`; its visual prefab asset is `Terminal2_Visual` and its icon asset is `Assets/RuneStorage/Terminal/Icons/RuneStorage_Terminal_Icon.png`.
- The RuneStorage Core uses the imported source `Assets/RuneStorage/Core/Source/cyber_core.glb`, visual prefab `RuneStorage_Core_Visual`, build-piece ID `piece_runestorage_core`, icon `Assets/RuneStorage/Core/Icons/RuneStorage_Core_Icon.png`, and AssetBundle `runestorage_core`.
- The Core's embedded `Animation` clip is its looping idle. It animates `Sphere.001_3` and `Sphere.003_4` through rotation and scale channels. Keep the Animator Controller and animated hierarchy included with the Core visual AssetBundle.
- The Core uses the cache-verified `piece_chest_wood` named clone as its runtime shell and retains its inherited native `Container`. Its `Container` inventory is the only stored-item authority; no shadow ledger or second container is allowed.
- The first buildable piece identity is `piece_runestorage_terminal`.
- The system has two separate buildable pieces:
  - **RuneStorage Terminal:** a runestone-style interaction point and user interface.
  - **RuneStorage Core:** the nearby storage authority and persistent native inventory. Capacity tiers are upgrades to this same placed piece, not separate buildables.
- The terminal does not own item data. The Core's native `Container` owns the inventory. If no valid Core is available, the terminal refuses storage actions and explains why.
- The MVP uses one core per terminal. If more than one eligible core is in range, the terminal refuses to choose silently and reports that the network is ambiguous. Multi-core networks are a later feature.
- The initial interaction radius is **12 meters**, authored as a tuning value. The exact distance must be tested against Valheim placement and multiplayer behavior before release.
- Stage 1 is mass storage: **Tier I has 256 inventory slots**. A slot holds a normal Valheim stack or one non-stackable item; total item count therefore depends on the mix of items and their normal stack limits.
- Tier I is a **Bronze Age** unlock. The Core build recipe requires a Forge and costs **10 Bronze, 10 Fine Wood, and 4 Surtling Cores**.
- The RuneStorage Terminal build recipe costs **10 Fine Wood, 10 Stone, and 2 Surtling Cores**.
- Tier II and Tier III upgrade costs are still open. Do not change the locked Terminal or Tier I Core recipes without revisiting the progression decision.
- Proposed later capacities double the slot count: Tier II has 512 slots and Tier III has 1,024 slots. Treat these as design targets until tier resizing, inventory loading, and compatibility are verified in-game.
- The terminal displays one aggregate row for compatible stored stacks, with the sum of their quantities. Items with different prefab identity or item-relevant metadata remain separate rows. Aggregation is presentation only; actual `ItemData` remains in the Core's `Container` inventory.
- The MVP stores items in the Core's native inventory. It does not spawn a hidden pile of world containers and does not use the terminal inventory as a second storage authority.
- Core-backed crafting is a required RuneStorage feature: the terminal uses ingredients held in the Core's native `Container` inventory. It must not create a second item store or recipe-ingredient ledger.
- For the first crafting slice, the Core is the only ingredient source; carried player items do not supplement it.
- The terminal has a separate crafting tab for each supported station type (for example, Workbench and Forge). Each tab groups the player's unlocked normal-craft recipes by their required station. A craft action is enabled only when a matching station instance is within Valheim's native use distance and meets the recipe's required level.
- Crafting eligibility uses `CraftingStation.InUseDistance(player)` and that same station instance's `GetLevel(true)`, compared with `Recipe.GetRequiredStationLevel(1)`. `GetLevel(true)` refreshes extensions for that station instance. Never combine upgrade pieces across different station instances.
- Each click initially crafts exactly one recipe. Output goes to the player's inventory by default; a visible UI toggle routes output to the connected Core. If the selected destination cannot accept the full output, reject the craft before consuming ingredients.
- The first slice covers registered, unlocked normal crafting recipes. Repairs and upgrades are excluded. A typed `Craft X` queue is deferred until one-at-a-time crafting is implemented and runtime-verified; do not add its field or imply batch support in the first slice.
- The MVP includes deposit, withdrawal, Core-backed crafting, search, sorting, slot-capacity feedback, native save/reload persistence, and server-authoritative request validation.
- The terminal UI will use Jotunn's `GUIManager` and Valheim-style UI helpers/anchors rather than requiring a separate player-facing UI mod. The old local mission-board panel is a layout reference only, not a RuneStorage dependency.
- Core-backed crafting is the next planned feature after the current storage interaction. Multi-Core crafting networks remain out of scope while the MVP supports one unambiguous Core per terminal.
- All prefab-based pieces must use Jotunn's registered named-prefab clone path. The implementation must verify the base prefab through `PrefabManager.Cache` before registration and must not guess a name such as `piece_wood`.
- The plugin must declare a Jotunn dependency and register one-shot prefab content from `PrefabManager.OnVanillaPrefabsAvailable`, unsubscribing the callback after registration.

### Stage capacity model

| Tier | Inventory slots | Status |
|---|---:|---|
| Tier I | 256 | Starting capacity target |
| Tier II | 512 | Proposed in-place upgrade |
| Tier III | 1,024 | Proposed in-place upgrade |

These capacities correspond to a 16-slot inventory width and heights of 16, 32, and 64 rows. Tier II and Tier III remain proposals until the actual Core Container accepts the dimensions and the default chest interaction cannot bypass the active tier. Tier I and Terminal recipe costs are locked above; Tier II/III upgrade items and costs, visual feedback, and release timing remain open.

## 2. Goals and non-goals

### Goals

- Let a player store a large amount of normal Valheim item content behind one terminal.
- Combine compatible stacks into readable aggregate totals.
- Preserve item identity and metadata for equipment and other non-stackable content.
- Keep the core as the single source of truth for stored items.
- Make the terminal useful while the core is nearby and clearly unusable when it is missing, too far away, invalid, full, or recovering from a save problem.
- Keep client requests untrusted and validate every deposit, withdrawal, range check, permission check, and capacity change on the authoritative side.
- Rely on native `Container` save/load for item persistence and persist the Core tier separately through its ZDO state.
- Package the mod for Thunderstore and deploy the built plugin to the active Gale profile after the release gates pass.

### Explicitly out of scope for the MVP

- Automatic pulling from nearby chests, carts, player tombstones, or other inventories.
- Multiple cores forming one shared network.
- Wireless or world-wide terminal access.
- Cross-world, cross-character, cloud, or external database storage.
- A custom permission system that bypasses Valheim's existing ward or build-access rules.
- Automatic item compression, conversion, filtering rules, or logistics pipes.
- A guaranteed atomic save spanning a player's inventory and the Core's world data. This engine boundary must be tested; do not add a second item ledger just to imply atomicity.
- Ripped or restricted Valheim assets copied into the repository or distributed in the Thunderstore package.

## 3. User experience and operational flow

### Build and connect

1. The player opens the Hammer and finds a **RuneStorage** build category or the chosen existing category.
2. The player places a **RuneStorage Terminal**.
3. The player places a **RuneStorage Core** within the configured range. It begins at Tier I and can later be upgraded in place.
4. The terminal detects exactly one valid core and changes to the ready state.
5. If the core is absent, too far away, destroyed, unreadable, or ambiguous because multiple cores are nearby, the terminal remains placeable but shows a direct status message instead of opening storage.

### Open and inspect

1. The player interacts with the terminal.
2. The terminal validates the player, access rules, terminal distance, core distance, and core state.
3. The storage screen opens with:
   - occupied slots and the active tier's slot capacity;
   - total item quantity across all stored stacks;
   - a search field;
   - deterministic sorting;
   - aggregate item rows with icon, name, summed quantity, and separate rows when item metadata differs;
   - deposit-all and withdrawal controls.
4. If validation fails, the screen does not open or shows a non-destructive error with a retry path.

### Deposit

1. The player selects **Deposit All** or deposits a selected item/quantity.
2. The server reads the player's real inventory. It does not trust item names, counts, or metadata sent by the client UI.
3. The server identifies the exact player inventory items and checks whether the Core's native inventory can accept them within the active tier's slot dimensions.
4. If the operation fits, the server transfers those actual item records to the Core `Inventory`; it does not serialize them into a parallel ledger.
5. The UI refreshes from the Core inventory and shows aggregate totals computed from the stored stacks.
6. If the operation does not fit, no item is removed and the UI reports the remaining slot capacity.

### Withdraw

1. The player selects an aggregate row and a quantity, or chooses one complete non-stackable item.
2. The server resolves the selection against the current Core inventory and validates that the player can receive the requested quantity.
3. The server removes the exact quantity from the Core inventory and adds the corresponding actual items to the player's inventory using Valheim's inventory APIs.
4. The UI refreshes from both inventories and reports the committed result.
5. If the inventory cannot accept the full request, the operation is rejected before mutation or is reduced only when the player explicitly chose an available-space-limited action.

### Craft from Core storage

1. The player opens the terminal's **Craft** view while an appropriate vanilla crafting station is nearby.
2. The Craft view has a separate tab for each supported station type, such as **Workbench** or **Forge**. Each tab shows its station availability and effective upgrade level.
3. The terminal offers recipes for that tab only when a real nearby station of that type satisfies the recipe's station level and other Valheim requirements. Its level includes valid upgrades/extensions as calculated by the game; station eligibility is not inferred from the Core's storage radius.
4. The UI shows recipe requirements and available quantities resolved from the Core's current native inventory. During the first crafting slice, the Core is the only ingredient source; carried player items do not supplement it.
5. When the player requests a craft, the authoritative side revalidates player access, terminal/Core state, the selected station instance and its effective level, recipe validity, and current Core ingredient counts. It serializes the craft with other mutations of that Core.
6. The system validates the selected output destination before consuming ingredients. By default this is the player's inventory; a visible toggle routes output to the Core. If the destination cannot accept the full output, reject the craft before mutation. Failure must not silently consume ingredients or duplicate output.
7. The terminal refreshes recipe availability and stored totals from the live Core inventory and reports the result.

### Status messages

The MVP needs clear messages for at least:

- `No RuneStorage Core found nearby.`
- `More than one RuneStorage Core is in range.`
- `Storage core is recovering from an interrupted operation.`
- `Storage is full.`
- `Storage has no room for another item type.`
- `The selected item changed. Refresh and try again.`
- `This item cannot be serialized safely and was not moved.`
- `The storage data could not be read. No items were changed.`

## 4. Architecture and ownership

The existing implementation is under `src/RuneStorage/`. The following names and paths are proposed boundaries for extending or consolidating it; not every listed class/file currently exists.

### Proposed source layout

```text
RuneStorage/
  DOCS/
    RUNESTORAGE_DEVPLAN.md
  src/
    RuneStorage/
      Plugin.cs
      RuneStorage.csproj
      Prefabs/
        RuneStoragePieceRegistration.cs
        RuneStorageVisuals.cs
      Core/
        RuneStorageCore.cs
        CoreInventory.cs
        InventoryAggregator.cs
        CoreTierCapacity.cs
        StorageTransaction.cs
        CoreCraftingTransaction.cs  # proposed; Core-backed recipe transactions
        NearbyStationResolver.cs    # proposed; verified station/level eligibility
      Terminal/
        RuneStorageTerminal.cs
        RuneStorageTerminalUi.cs
        CoreResolver.cs
      Security/
        AccessValidator.cs
      Assets/
        icon.png
        RuneStorage.bundle
  Unity/
    RuneStorage-Valheim/
  scripts/
    package_runestorage_thunderstore.ps1
  MODS/
    thunderstore/
```

### Component responsibilities

| Component | Owns | Must not own |
|---|---|---|
| `Plugin` | BepInEx entry point, dependency declaration, config, lifecycle | Storage data or UI state |
| Piece registration | Verified named clones, recipes, icons, visual attachment | Direct cached-prefab instantiation as the final piece |
| `RuneStorageCore` | Core identity, range anchor, native `Container`, tier, transaction state | Terminal screen state or a duplicate item store |
| `CoreInventory` | Native inventory access, slot-capacity enforcement, item transfer | Search/filter presentation state |
| `InventoryAggregator` | Grouping compatible inventory stacks, summed totals, lookup, sorting | Mutating item ownership or storing a second copy |
| `CoreTierCapacity` | Tier-to-width/height mapping and slot-count math | Runtime tier persistence or upgrades |
| `RuneStorageTransactions` | Current deposit/withdraw path; future shared Core mutation coordination | UI commands that bypass server validation |
| `CoreCraftingTransaction` (proposed) | Recipe validation, Core ingredient reservation/consumption, output completion, and failure handling | A second ingredient store or trusting UI counts |
| `NearbyStationResolver` (proposed) | Resolving nearby station type/level and eligibility using verified Valheim APIs | Inventing a station radius that bypasses game rules |
| `RuneStorageTerminal` | Interaction point, nearby-core resolution, UI requests | Permanent item ownership |
| `RuneStorageTerminalUi` | Search, sorting, aggregate display, selection, user feedback | Trusting client-supplied quantities or prefab names |
| `AccessValidator` | Player, distance, build-access, ward, and server-authority checks | Granting access when validation is unavailable |
| Package script | Clean build output, manifest, README, icon, DLL placement in ZIP | Shipping source, PDB, or unrelated files |

### UI reuse strategy

Jotunn already exposes a GUI layer intended for custom in-game panels. RuneStorage subscribes to `GUIManager.OnCustomGUIAvailable` and attaches its panel beneath `GUIManager.CustomGUIFront`, using Jotunn's wood panel, scroll view, text, input-field, and button helpers. This avoids making RuneStorage depend on a separate end-user UI mod that may not expose a stable developer API.

**Design Read:** In-game item-storage panel for Valheim players, in Valheim's native wood-panel language, with ENERGY 1 / RHYTHM 1 / MOTION 1. Jotunn's panel and Averia/Norse fonts keep the screen visually in-world; the dark inset gives the list a stable reading surface; warm gold is reserved for the title and item totals so quantities are easy to scan; the single predictable list suits search and repeated inventory rows; no motion is used because it would distract from item selection.

The existing local file below contains useful patterns for panels, buttons, scroll views, and TextMeshPro rows, but its canvas lifecycle must not be copied blindly. RuneStorage should adapt the layout ideas to Jotunn's custom-GUI lifecycle and destroy/refresh its panel cleanly on scene changes:

```text
C:\Users\magni\Documents\BotsnCoding\Valheim\Households-REPO\OLDmoddedcatoheim\src\MissionSystemMod\MissionBoardPanel.cs
```

The current RuneStorage screen is implemented as a single panel with:

- a title and core status line;
- occupied slots, active tier capacity, and total item quantity;
- a search field;
- a scrollable item-entry list;
- per-entry quantity and withdraw controls;
- `Deposit All` and close buttons;
- a visible error/status area for missing cores, full capacity, stale selections, and recovery mode.

The planned **Craft** view will use the same GUI lifecycle and Valheim visual language. It will have a station-specific tab for each supported station type, report that station's availability/effective upgrade level, and show only its eligible recipes. Ingredient availability comes from the Core. Recipe-list scale, filters, and disabled-tab appearance remain design decisions; the output destination defaults to player inventory and can be toggled to the Core.

The UI is a searchable, aggregated view and request builder. It never becomes a second inventory or trusts its displayed counts as transaction authority. The Core's normal chest interaction is suppressed. Current source-level deposits/withdrawals re-resolve the Core and access state, require the local process to own the Core's ZNetView, and move the original `ItemData` between native inventories. A client that does not own the Core cannot transfer. A network request path for remote peers is still required before multiplayer transfers or Core-backed crafting can be enabled for remote clients.

### Prefab and asset pipeline

The planned asset path is:

```text
author-made visual or approved local asset
  -> Unity prefab/material/icon
  -> AssetBundle when a custom visual is required
  -> BepInEx plugin
  -> Jotunn named clone
  -> in-game buildable piece
```

The terminal visual should be runestone-like. The exact vanilla base prefab name is **TBD until `PrefabManager.Cache` is inspected at runtime**. For the Core build-piece runtime shell, verify the selected vanilla base name through `PrefabManager.Cache` before registration. The plan must not invent a base name.

### Previous terminal visual source

The selected source for the terminal visual is:

```text
C:\Users\magni\Documents\BotsnCoding\Valheim\ASSETS-Repo\assetsetuff\Digital-Storage\Source\terminal1.glb
```

Verified file facts from the source file:

```text
Format: glTF 2.0 binary (.glb)
Size: 4,968,708 bytes
SHA-256: FE109DA924AE81BBC457376DE59C4CC2A668341C6AB811B9BE1AEE6760DBF673
Nodes: 136
Meshes: 63
Materials: 1
Embedded images: 4
Animations: 0
```

Keep the original source file in `ASSETS-Repo`. Import or stage a working copy in `Unity\RuneStorage-Valheim\` and do not overwrite the original. The Unity import checkpoint must confirm:

- the model faces the intended direction;
- the model has a sensible Valheim-world scale;
- the root/pivot gives the terminal the intended placement point;
- all four embedded images and the `GATE_FUN` material render correctly;
- the visual has an intentional collider or uses the verified base piece's collider behavior;
- the visual is saved as a RuneStorage terminal prefab with a unique AssetBundle label;
- the final AssetBundle prefab name is recorded before plugin registration.

The GLB has no animation data, so it is a static terminal visual. Do not add animation-specific runtime code unless a later replacement asset requires it.

### Current terminal visual prefab

The replacement prefab supplied in the Unity project is:

```text
Unity\RuneStorage-Valheim\Assets\RuneStorage\Terminal\Prefabs\Terminal2_Visual.prefab
```

Its serialized root scale is `(4, 4, 4)`. Runtime attachment must preserve the prefab-authored scale and facing; alignment may adjust position only. The RuneStorage terminal icon is rendered from this prefab, and the `runestorage_terminal` AssetBundle contains this prefab instead of the previous terminal visual. Runtime observation (2026-10-19): the build ghost was offset, but the placed piece aligned to the marker. Runtime-only centering was insufficient because the placement ghost did not reflect the custom anchor's correction. The combined renderer bounds are now centered on the piece root during `AttachVisual`, before `PieceManager.AddPiece`; the post-enable anchor remains as a small runtime correction for the placed piece. The Release DLL containing this change is deployed to `VHouseholds-Admin`. The user confirmed the revised ghost and placed appearance look correct in-game on 2026-10-19.

### Selected Core visual source

The selected core source is the Unity-imported working asset:

```text
Assets/RuneStorage/Core/Source/cyber_core.glb
```

The binary GLB contains one 10-second clip named `Animation`, with four transform channels: rotation and scale on `Sphere.001_3` and `Sphere.003_4`. The Unity import uses glTFast's Mecanim animation mode. The Core visual prefab must include an Animator Controller whose default state plays this clip on loop. Keep the animated node hierarchy intact and center the visual root on the model's measured bounds before using it as a buildable piece.

Runtime checkpoint (2026-10-18): Unity built the generic Core prefab, 256x256 icon, and `runestorage_core` AssetBundle. The `VHouseholds-Admin` profile log confirms the 0.1.3 plugin loaded both bundles and registered both pieces using the cache-verified `piece_chest_wood` base. The user confirmed the Core placement ghost and placed model align correctly. Hover, support, health, animation appearance, and storage behavior remain unverified.

### Terminal effect decision (deferred)

The ripped reference project contains two useful vanilla-looking smoke candidates:

```text
Assets\world\SmokeFire\SmokeParticleSystem.prefab
Assets\GameElements\StatusEffects\effects\vfx_Smoked.prefab
```

The MVP decision is now to use the runestone terminal GLB as-is, with no particle child, smoke material, or runtime effect clone. The previously staged smoke files are optional reference work only and must not be attached, registered, or included in the terminal AssetBundle unless the effect decision is reopened later.

The `demister_ball` reference is intentionally not selected because it is the Valheim demister orb and produces the round-orb appearance rather than soft terminal smoke.

For each piece, the implementation must follow this order:

1. Wait for `PrefabManager.OnVanillaPrefabsAvailable`.
2. Resolve candidate base names from `PrefabManager.Cache`.
3. Log the verified base name and fail registration clearly if none is present.
4. Construct the piece with the Jotunn named clone constructor, such as `new CustomPiece(customName, verifiedBasePrefabName, pieceConfig)`.
5. Attach the custom visual and assign the icon after the Jotunn clone exists.
6. Disable only the base visual renderers replaced by the RuneStorage visual.
7. Add the completed piece to the appropriate Jotunn manager.
8. Unsubscribe the one-shot registration callback.

The implementation must not create the final piece by directly instantiating a cached prefab and wrapping that clone in `CustomPiece`.

## 5. Data, lifecycle, and failure handling

### Inventory capacity and aggregate identity

The Core's native `Container` and `Inventory` are the sole stored-item data. Tier capacity is a number of inventory slots; normal Valheim max-stack behavior remains in force. Current proposed dimensions are:

| Tier | Width | Height | Slots |
|---|---:|---:|---:|
| Tier I | 16 | 16 | 256 |
| Tier II | 16 | 32 | 512 |
| Tier III | 16 | 64 | 1,024 |

Tier I's 256 slots are the starting target. Tier II and Tier III remain proposals pending runtime validation of expansion and saved-tier restoration. These are slot limits, not fixed item-unit guarantees: one occupied slot may hold a stack, and stack sizes differ by item.

The terminal aggregates the current Core inventory for display. It must compare exact prefab identity and all fields that affect stack compatibility or item identity (including quality, variant, durability, crafter, and custom data as applicable). Stack count and inventory grid position are not identity. Unlike items must never be merged in the view. Aggregation is read-only and does not create substitute `ItemData` records.

The project's local Valheim assembly was inspected. `Container.Awake()` creates an `Inventory`; `Container.Save()` serializes it through `Inventory.Save(ZPackage)` into the Core's ZDO, and `Container.Load()` restores it through `Inventory.Load(ZPackage)`. `Inventory.SetHeight(Int32)` exists and changes the inventory's logical height. RuneStorage relies on this native item persistence rather than a duplicate item serializer. The current Core implementation retains its inherited `Container`, stores its tier separately in the ZDO, and applies tier dimensions before native inventory loading. The user reported that stored items survived a game restart; wider capacity, tier-transition, and multiplayer persistence checks remain open.

The standalone capacity mapping in `src/RuneStorage/Core/CoreTierCapacity.cs` is covered by isolated tests. It does not resize a live `Inventory`, attach a `Container` to the Core, aggregate items, or verify save/load.

### Core lifecycle states

```text
Unregistered
  -> Ready
  -> Full
  -> TierStateInvalid
  -> RecoveryRequired
  -> Destroyed / Unloaded
```

- `Ready`: reads and writes are permitted after validation.
- `Full`: reads remain available; deposits that exceed the active slot capacity are rejected without mutation.
- `TierStateInvalid`: the separately persisted tier is outside the supported range; block writes rather than guessing a smaller capacity.
- `RecoveryRequired`: an observed interrupted transfer cannot yet be reconciled; new writes are blocked until it is handled.
- `Destroyed / Unloaded`: no new request may use the core reference.

### Native inventory persistence and tier state

Valheim's `Container` owns persistence for the Core inventory. RuneStorage must not mirror item records, serialize a second ledger, or silently clear the Container if loading fails. Persist only RuneStorage-owned state separately, initially the Core's capacity tier and any small transaction/recovery marker proven necessary by testing.

On a new Core with no saved tier, initialize Tier I. On a loaded Core, validate the persisted tier and apply its width/height before native Container contents are loaded. If the tier is unknown or the inventory cannot be restored safely, block writes and preserve diagnostic state; never replace the inventory with an empty one. Verify that increasing height preserves all existing item positions and that a save/reload at each tier retains every item.

Do not impose an artificial serialized item-payload limit on the native Container path. Test the game's existing ZDO save behavior at the intended slot capacities and document any engine limit discovered.

### Transaction rules

Every deposit and withdrawal is a server-authoritative transfer between the player's inventory and the Core inventory:

1. Validate the sender, player state, distance, access, target Core, inventory-change token, and request shape.
2. Acquire a per-core operation lock.
3. Re-read the exact source and destination inventory state.
4. Calculate the exact before/after item and slot delta.
5. Check active tier capacity and destination inventory space before mutation.
6. Transfer exact existing item records through Valheim's inventory APIs without creating a duplicate item store.
7. Confirm the actual transfer result, update any small Core change counter used for stale UI requests, and refresh the UI from authoritative inventories.
8. Release the lock in a `finally`-equivalent cleanup path.

The implementation must never accept a client-supplied prefab name, item count, quality, or metadata as proof that the player owns an item. The server reads the actual inventory and matches the requested operation to it.

### Core-backed crafting transaction

Crafting is another mutation of the Core's authoritative native inventory, not a separate storage backend. A craft request must resolve a currently registered recipe and a nearby station satisfying that recipe's requirements, then re-read the Core inventory and reserve the exact ingredient quantities under the same per-Core mutation lock used by deposits and withdrawals. The UI's ingredient counts are a preview only. The implementation must preflight the selected output destination before consuming ingredients: player inventory by default, or Core when the UI toggle is selected. If the destination cannot accept the full output, reject before mutation. No craft may be reported as complete until ingredients and output have reached a consistent result. The local Valheim recipe/station APIs have been verified for the initial implementation; runtime behavior and transaction edge cases still require testing.

Station tabs are keyed by actual crafting-station type, not display labels guessed from recipe names. For each candidate station, use the game's effective level calculation, including its valid upgrade/extension pieces. When multiple stations are nearby, validate each recipe against one real eligible station instance; do not combine extension levels across separate stations. Displayed station state is a preview and must be revalidated at craft time.

### Interrupted operations

If a disconnect, save, or plugin unload occurs during a transaction, test whether Valheim's inventory APIs can leave a partial transfer across the player's character save and the Core's world save. The normal operation must never report success unless both sides reflect the exact transfer. Only add a compact operation marker/recovery path if an observed engine boundary requires it; do not duplicate full item data into a custom journal. If recovery cannot safely reconcile the two inventories, block further writes and report the precise reason rather than guessing or clearing data.

The plan must include a documented limitation for the boundary between world-object persistence and character-inventory persistence. A successful normal transaction is required to be loss-free in testing; crash behavior must be verified and any remaining engine limitation must be visible to the player rather than hidden.

## 6. Configuration, permissions, and integrations

### Proposed configuration

The first configuration file should expose only values that are safe to tune without changing the storage schema:

```ini
[Storage]
CoreRangeMeters = 12
EnableDebugLogging = false

[Access]
UseValheimBuildAndWardAccess = true

[UI]
ItemsPerPage = 40
```

Tier slot counts are defined by the tier map, not user configuration. Do not silently lower a Core's tier or hide occupied slots if the supported dimensions change in a later version.

### Dependencies

- Required: BepInEx Valheim pack.
- Required: Jotunn.
- Optional dependencies: none for the MVP.
- Unity, Valheim, and Jotunn assembly paths must be resolved from the current RuneStorage build environment rather than copied blindly from another mod.

### Access behavior

The server must enforce the same build/ward access boundary that the selected base pieces use, plus the terminal/core distance checks. If the relevant access API cannot be verified, the feature must fail closed for that request and log the missing validation path.

### Package contract

The Thunderstore package must contain only the release files:

```text
icon.png                 # 256x256, final approved icon
manifest.json
README.md
BepInEx/plugins/RuneStorage.dll
```

The package must not include `.pdb`, `obj`, `bin`, source files, local save data, test logs containing private paths, or unrelated AssetBundles.

## 7. Safety, security, and product constraints

### Duplication and item-loss protections

- Treat every client UI request as untrusted.
- Validate the player and core on the server immediately before mutation.
- Use a per-core lock and a Core inventory-change counter (or equivalent verified state) to reject stale requests.
- Re-read the actual inventory for deposits.
- Resolve withdrawals against exact current Core inventory entries and mutate those entries only.
- Add an operation journal only if testing demonstrates it is needed to recover a real cross-save partial-transfer case; never store a second copy of item records in it.
- Never retry a mutation blindly after an exception.
- Do not clear a stuck or corrupt core as a convenience action.
- Log operation ID, core ID, player ID, inventory-change counter, and outcome without logging full custom item data by default.

### Placement and prefab safety

- Add `[BepInDependency(Jotunn.Main.ModGuid)]` to the plugin.
- Register only after vanilla prefabs are available.
- Resolve and verify every base prefab through `PrefabManager.Cache`.
- Use the named Jotunn clone constructor for both buildable pieces.
- Preserve `WearNTear`, `ZNetView`, support, hover, and health references from the valid named clone.
- Verify the placement ghost before calling the piece complete.

### Failure behavior

- Missing base prefab: log the candidate names and abort only that piece registration; do not create a broken fallback clone.
- Missing custom visual or bundle: register a clearly logged fallback only if the fallback was explicitly designed and verified; otherwise fail the visual registration without a broken runtime object.
- Invalid persisted tier or failed inventory load: block writes and preserve diagnostic state; never replace the Core inventory with an empty one.
- Capacity failure: reject the whole operation before mutation.
- Invalid or unsupported item metadata: leave the original inventory untouched.
- Multiple nearby cores: refuse ambiguous access in the MVP.
- Core destroyed during a request: abort, release the lock, and refresh the terminal state.

## 8. Verification matrix

| Scenario | Expected result | Evidence required |
|---|---|---|
| Clean Release build | DLL builds with no errors and no unexplained warnings | Build output and DLL hash/size |
| Jotunn dependency | Plugin declares and loads after Jotunn | Startup log showing plugin and dependency load |
| Base prefab resolution | Verified names come from `PrefabManager.Cache` | Registration log with resolved names |
| Terminal placement ghost | Ghost appears with valid support/hover behavior | In-game screenshot or video plus log |
| Core placement ghost | Core places without `WearNTear`, `ZNetView`, health, or support errors | In-game placement check and console log |
| No nearby core | Terminal refuses storage and gives the no-core message | Manual test and log |
| One core in range | Terminal opens the correct core | Manual test with core identity/inventory-change log |
| Multiple cores in range | Terminal refuses ambiguity without moving items | Manual test and log |
| Tier I capacity | Core accepts items up to 256 inventory slots and rejects overflow without removing player items | In-game boundary test with occupied-slot counts |
| Tier expansion | Height increase preserves all item positions and unlocks only the new rows | Runtime test from 256 to 512 and 1,024 slots |
| Deposit one stack | Exact amount leaves player inventory and appears in Core inventory | Before/after inventory counts |
| Deposit many stacks | Compatible stacks appear as one aggregate row with correct summed quantity | Terminal display compared with Core inventory |
| Non-stackable equipment | Quality, durability, variant, crafter, and custom data survive storage/withdrawal | Item field comparison before and after |
| Search and sorting | Results are deterministic and do not change either inventory | UI test across refreshes |
| Partial withdrawal | Requested amount leaves Core inventory and reaches player inventory exactly once | Before/after counts |
| No inventory space | Withdrawal is rejected or reduced only by an explicit user action | Inventory-full test |
| Save and reload | Stored items remain available with the same totals and metadata | World reload test |
| Plugin restart | Core's native Container inventory loads without resetting data | Restart log and before/after inventory comparison |
| Missing/ineligible station | Craft view refuses recipes that require an unavailable station or station level | In-game test with no station, wrong station, and insufficient station level |
| Station-specific tabs | Workbench recipes appear only in the Workbench tab, Forge recipes only in the Forge tab, and so on for supported types | Compare each tab with the registered recipe station type |
| Upgraded station level | Recipes requiring the higher level appear when valid upgrades/extensions are present and disappear when that level is no longer valid | In-game test before/after adding or removing station upgrades and confirming the displayed effective level |
| Multiple same-type stations | Recipe eligibility is satisfied by one actual station instance; separate stations' upgrade pieces are never combined | Place stations with different levels and verify recipe visibility and craft-time validation |
| Core-backed recipe preview | Recipe availability and ingredient counts match the live Core inventory | Compare terminal preview with Core contents and registered recipe |
| One craft per click | One click consumes one recipe's Core ingredients and produces that recipe's output once | Before/after Core and output-inventory comparison |
| Output destination default | A newly opened terminal routes crafted output to the player's inventory unless changed | Craft once and verify the player inventory changes while the Core does not receive output |
| Output destination toggle | Switching the UI toggle routes the next crafted output into the connected Core | Craft once in each destination mode and compare both inventories |
| Insufficient Core ingredients | Craft is rejected without changing the Core or output inventory | Controlled missing-ingredient test |
| Craft output has no room | Craft does not consume ingredients or duplicate/drop output | Full destination-inventory test |
| Concurrent craft and storage transfer | Operations serialize on the Core; each committed result occurs once | Host/client concurrency test and before/after inventories |
| Save/reload after crafting | Ingredients remain consumed and crafted output persists exactly once | World reload and inventory comparison |
| Interrupted deposit | Recovery does not duplicate or silently lose the operation | Forced disconnect/save-boundary test |
| Interrupted withdrawal | Reserved items are restored or delivered exactly once | Forced disconnect/save-boundary test |
| Invalid saved tier | Core blocks writes and preserves the existing native inventory | Controlled tier-state test and log |
| Multiplayer access | Server validates requests; a stale or out-of-range client request fails | Host/client test with logs |
| Destruction/unload | Terminal stops using a destroyed/unloaded core | Manual test and no stale-reference errors |
| Package contents | ZIP contains only approved release files | ZIP listing |
| Active Gale deployment | Intended DLL is present in `VHouseholds-Admin` | File hash comparison and startup log |

## 9. Phased checklist

### Phase 0 - Repository and design lock

 - [x] Create the initial RuneStorage project structure under `src/RuneStorage`.
 - [x] Confirm the installed Valheim, BepInEx, Jotunn, .NET Framework target, and Unity/AssetBundle tool versions from the local build environment.
- [x] Create the dedicated Unity project at `Unity\RuneStorage-Valheim\` using the verified compatible Unity editor and render-pipeline setup.
 - [x] Confirm the assembly references and build conventions that are actually available to RuneStorage. Do not copy identifiers or deployment files from another mod.
 - [x] Choose and record the plugin GUID, first package version, namespace, terminal piece ID, visual prefab name, icon asset path, and bundle name.
- [x] Record the runtime-verified `PrefabManager.Cache` base prefab used by the terminal and Core: `piece_chest_wood`; keep checking it at registration time.
- [x] Use the user-supplied `Terminal2_Visual` prefab as the terminal's custom AssetBundle visual.
- [x] Stage `terminal1.glb` into the Unity asset project without altering the original source file.
- [x] Inspect `terminal1.glb` in Unity and confirm scale, orientation, pivot, embedded materials/textures, collider behavior, and the intended terminal prefab root.
- [x] Record `Terminal2_Visual` and the `runestorage_terminal` AssetBundle label before attaching it to the Jotunn clone.
- [x] Defer terminal particles for the MVP; use the imported runestone terminal visual as-is.
- [ ] If an effect is revisited later, inspect it in a separate test prefab before changing the terminal visual.
- [x] Inspect the local Valheim `Container`, `Inventory.Save/Load`, and `Inventory.SetHeight` APIs; confirm the native Container persists inventory through its ZDO.
- [x] Inspect the local Valheim assembly: `Container.Awake` creates the inventory from prefab dimensions and schedules `CheckForChanges`; its `Load` reads the inventory blob from the Core ZDO.
- [x] Apply the persisted tier in a Harmony prefix before the private `Container.Load` method restores inventory contents.
- [x] Add Core-only Harmony guards for native `Container.Interact` and hover text; non-RuneStorage containers pass through unchanged.
- [ ] Verify in-game placement, tier load, interaction suppression, and save/reload against the active Gale profile.
- [ ] **Verify:** The repository has a clean buildable skeleton, the dependency versions are recorded, base prefab names are runtime-verified, and the capacity/persistence constraints are measurable.

### Phase 1 - Plugin and safe piece registration

 - [x] Add the BepInEx plugin entry point and `[BepInDependency(Jotunn.Main.ModGuid)]`.
 - [x] Subscribe to `PrefabManager.OnVanillaPrefabsAvailable` and unsubscribe after one-shot registration.
 - [x] Register the terminal with `new CustomPiece(customName, verifiedBasePrefabName, pieceConfig)` using a verified base name.
- [x] Register the Core with the same named-clone path and its unique piece, visual, icon, and bundle identifiers.
- [x] Attach the terminal/Core visuals after cloning and before adding each piece to its Jotunn manager.
- [x] Rebuild the 256x256 terminal icon and `runestorage_terminal` bundle from `Terminal2_Visual`, build Release, and deploy the matching DLL to `VHouseholds-Admin` (2026-10-19).
- [x] Verify in-game that the ghost stays centered through rotation and the placed visual looks correct; the user confirmed the result on 2026-10-19.
- [x] Do not attach a smoke or particle effect to the MVP terminal.
- [x] Add distinct icons and configure the Hammer recipes after visual and piece identities stabilized; the Core requires a Forge.
- [x] Add startup logs for plugin version, resolved base prefabs, piece names, and registration result; the 0.1.3 profile log confirms both bundles and pieces registered with `piece_chest_wood`.
- [ ] **Verify:** Both pieces appear as valid buildable objects and their ghosts/placement work. The user confirmed Core ghost and placed alignment on 2026-10-18; hover/support/health checks still need testing.

### Phase 2 - Native Core inventory and tier capacity

- [x] Define and test the slot dimensions for Tier I (256 slots) and proposed Tier II/III capacities (512/1,024 slots) in an isolated capacity model.
- [x] Retain the named-cloned Core `Container` and configure its base inventory dimensions for Tier I.
- [x] Add a Core marker and runtime tier hook that use the native `Container`/`Inventory` without introducing a second item store.
- [x] Persist the Core capacity tier separately in its ZDO and apply it before native inventory load.
- [ ] Verify in-game that Tier I has 256 usable slots and saved tier changes load before item positions are restored.
- [x] Implement read-only aggregate grouping for compatible Core inventory items, retaining separate entries when item metadata differs.
- [ ] Implement `Ready`, `Full`, and invalid-tier/transfer-recovery states as needed by verified runtime behavior.
- [ ] Test occupied-slot counts, item identity preservation, height expansion, and native save/reload at each supported tier.
- [ ] **Verify:** A placed Core saves and reloads its native Container contents, preserves all item metadata, and enforces its active slot capacity without a parallel ledger.

### Phase 3 - Server-authoritative transactions

The current source includes owner-local transfer methods as a development slice. The complete phase remains open: remote clients cannot submit transfers, and interruption recovery, stale-request validation, and host/client synchronization have not been proven.

- [ ] Implement the per-core operation lock and stale-inventory-change check.
- [ ] Implement deposit preview without mutation.
- [ ] Implement exact deposits between player and Core inventories, validating destination capacity before mutation.
- [ ] Implement exact withdrawals from Core inventory, validating player inventory space before mutation.
- [ ] Test whether a compact interrupted-transfer marker/recovery path is required; do not serialize duplicate item records.
- [ ] Add access, distance, sender, core-state, and stale-inventory validation at every request boundary.
- [ ] **Verify:** Deposit and withdrawal tests pass at the normal path, capacity boundaries, stale-request path, full-inventory path, out-of-range path, and forced-interruption path without duplication or silent item loss.

### Phase 4 - Terminal interaction and UI

- [x] Implement nearby-core resolution using the configured radius and the one-core-only MVP rule.
- [x] Implement terminal interaction state and direct no-core/ambiguous-core/recovery messages.
- [x] Subscribe the screen to Jotunn `GUIManager.OnCustomGUIAvailable` and attach it to the scene-safe custom GUI anchor instead of maintaining an unmanaged permanent canvas.
- [x] Use Jotunn's wood panel, scroll view, buttons, inputs, and Valheim fonts; keep the UI under Jotunn's scene-managed anchor.
- [x] Implement Stored and Deposit tabs with occupied slots, active tier capacity, total item quantity, search, deterministic sorting, aggregate rows/icons, per-item Take/Deposit 1/5/X/All, and deposit-everything controls.
- [x] Add Stored-tab Name/Amount sort controls with an ascending/descending arrow toggle.
- [x] Set terminal list scroll sensitivity to 300 after follow-up feedback that the previous setting was still too slow; verify the new rate in-game.
- [x] **Runtime confirmation (2026-10-19):** The user confirmed the UI works well and normal deposit/withdraw transfers work flawlessly.
- [x] Refresh the screen from the live Core inventory after every successful mutation and reject stale selections.
- [x] Add readable feedback for missing/ambiguous cores, invalid data, full storage, stale selections, and player-inventory capacity.
- [ ] **Verify:** A player can place the two pieces, open the terminal, deposit a large material batch, find it by search, withdraw a chosen amount, and see correct status after moving the core out of range.

### Phase 5 - Multiplayer, reload, and hardening

- [x] **User-reported smoke check (2026-10-19):** Stored items appeared to remain safe after restarting the game. This does not replace larger-scale save/reload or interrupted-transfer testing.
- [ ] Run host/client tests with matching and mismatched plugin versions.
- [ ] Validate server-side ownership/access behavior against the selected Valheim ward/build-access API.
- [ ] Test simultaneous deposit and withdrawal requests against the same core.
- [ ] Test player disconnects, server restart, world save/reload, terminal destruction, Core destruction, and plugin unload during normal transfers and any proven recovery-marker path.
- [ ] Add rate limiting or request debouncing if UI spam can create excessive RPC or inventory scans.
- [ ] Review logs for sensitive data, stale references, duplicate registrations, and exception paths.
- [ ] **Verify:** Multiplayer and lifecycle tests show one authoritative native inventory result per committed transfer and no partial transfer is hidden.

### Phase 6 - Core-backed crafting and capacity upgrades

#### Core-backed crafting - next feature

- [x] Verify the installed Valheim APIs from `assembly_valheim.dll`: recipes are registered in `ObjectDB.m_recipes`; `Player.IsRecipeKnown` and `Recipe.GetRequiredStation(1)` identify unlocked station recipes; `CraftingStation.InUseDistance` applies the native player-to-station range; `GetLevel(true)` refreshes that instance's extensions before returning its effective level; and `GetRequiredStationLevel(1)` supplies the recipe threshold. `InventoryGui.DoCrafting` consumes from the player's inventory, so RuneStorage must consume from the Core directly.
- [x] Lock the initial behavior: Core-only ingredients, one normal craft per click, output to player by default, a UI toggle for Core output, and no repair/upgrade actions in this slice.
- [ ] Add station-specific **Workbench**, **Forge**, and other supported station tabs to the terminal using the existing Jotunn GUI lifecycle; show station availability/effective level and the player's unlocked recipes for that station.
- [ ] Implement server-authoritative recipe preview and craft requests that revalidate the selected live station instance and effective level, recipe, Core, access, and ingredient inventory.
- [ ] Consume ingredients from the Core's native `Container` inventory under the same per-Core mutation lock as deposit/withdraw; do not create an ingredient ledger.
- [ ] Validate the selected output capacity before ingredient mutation. Default output to the player inventory, add the Core-output toggle, and implement non-destructive failure handling for unavailable stations, stale recipe state, insufficient ingredients, no output space, and interrupted requests.
- [ ] **Verify:** One click crafts exactly one representative recipe from Core-held ingredients at the correct nearby station; confirm wrong/missing/under-level stations and missing ingredients change nothing, both output destinations work, and successful outputs and remaining Core items survive save/reload exactly once.
- [ ] After the one-at-a-time craft path passes runtime verification, add a typed `Craft X` amount and sequential queue. Each iteration must be a separately revalidated single-craft transaction, stop on the first failure, and report the completed count. This is deferred and must not be implemented as part of the initial crafting slice.

#### Capacity upgrades - after the first crafting slice

- [ ] Add an Upgrade tab showing current/next tier and capacity, required versus available materials, and an in-place upgrade action.
- [ ] Add an in-place upgrade path that persists the Core's capacity tier without changing its build-piece identity.
- [ ] Define and implement the Tier II upgrade item, recipe, validation, and visual/status feedback.
- [ ] Add Tier III only after Tier II persistence and migration tests pass.
- [ ] Decide whether multiple cores should merge into a network or remain separate storage systems.
- [ ] **Verify:** Upgrading a populated Core in place preserves every existing item and slot position, increases capacity without changing item or build-piece identity, and survives save/reload.

### Phase 7 - Release packaging and deployment

- [ ] Set the first release version and synchronize it across the plugin, manifest, README, and package filename.
- [ ] Add a player-facing README with exact build pieces, capacity behavior, requirements, limitations, and troubleshooting.
- [ ] Add the final 256x256 Thunderstore icon after visual approval.
- [ ] Build a clean Release DLL and remove development artifacts from the staging directory.
- [ ] Create the Thunderstore ZIP with only the approved package contents.
- [ ] Inspect the ZIP listing and verify manifest dependencies and version.
- [ ] Deploy the DLL and any required bundle to:
  `C:\Users\magni\AppData\Roaming\com.kesomannen.gale\valheim\profiles\VHouseholds-Admin\BepInEx\plugins\`
- [ ] Record source and deployed hashes without creating an automatic backup unless separately requested.
- [ ] Start the active profile and capture the registration, placement, interaction, persistence, and error logs.
- [ ] **Verify:** The package installs, the active Gale profile loads RuneStorage, both pieces work in-game, and the verification matrix has evidence for every required MVP row.

## 10. Open tuning points

- Plugin GUID and author/package owner.
- Exact terminal and core base prefab names after runtime cache inspection.
- Whether the terminal uses a custom AssetBundle visual or an approved vanilla runestone visual with custom children.
- Exact Tier II and Tier III upgrade recipes, stations, and resource costs.
- Whether future versions should allow player-carried ingredients to supplement Core ingredients; the first slice uses Core ingredients only.
- Whether future versions should include repair or upgrade actions; the first slice handles normal recipes only.
- The final set of supported station types and mod-added recipe edge cases confirmed during runtime tests; the UI derives station tabs from registered recipes.
- Batch `Craft X` quantity and queue behavior, deferred until one-at-a-time crafting is runtime-verified.
- Whether the first public MVP permits non-stackable equipment or initially limits storage to safely reconstructable stackable items.
- Whether native Container persistence or inventory UI behavior imposes a practical slot ceiling below the proposed tiers.
- The precise recovery behavior available when a player disconnects between a world-object save and a character-inventory save.
- The final Stage 1 interaction radius after placement and multiplayer tests.
- Whether build/ward access should be required for both placement and terminal use.
- The first public package version after Phase 7 passes.
