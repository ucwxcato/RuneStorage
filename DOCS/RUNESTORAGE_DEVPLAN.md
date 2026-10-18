# RuneStorage - Development Plan

> **Status:** In progress. The Unity terminal visual, 256x256 icon, AssetBundle, Jotunn registration, Gale deployment, placement ghost, and placed transform are verified as of 2026-10-18. The Core visual prefab, 256x256 icon, AssetBundle, Jotunn registration, Gale deployment, ghost, and placed transform are also verified as of 2026-10-18; Core hover/support/health behavior and storage remain in progress.
>
> **Purpose:** Add a Valheim digital-storage system with a buildable runestone terminal and a nearby storage core that stores large quantities of items in a searchable, sortable ledger.
>
> **Authority:** This is the canonical implementation plan for the RuneStorage plugin. It owns the RuneStorage pieces, storage ledger, terminal interaction, persistence, transactions, packaging, and test gates.
>
> **Target:** Valheim with BepInEx and Jotunn, tested through the active Gale profile:
> `C:\Users\magni\AppData\Roaming\com.kesomannen.gale\valheim\profiles\VHouseholds-Admin`

## 0. Outcome

The player places a RuneStorage Terminal and one RuneStorage Core within range. The Core can be upgraded in place to increase its capacity. Interacting with the terminal opens a storage screen backed by the Core. The player can deposit items, search and sort stored entries, withdraw exact quantities, and leave the world without losing or duplicating items.

The intended happy path is:

```text
Build terminal + core -> core is detected nearby -> open terminal -> deposit items
-> ledger shows the combined totals -> search/select an entry -> withdraw an amount
-> save and reload -> the same items remain available exactly once
```

The MVP is complete only when the path works with a mass-storage capacity, the terminal refuses to operate without a valid core, and the transaction tests show no duplication or silent item loss.

## 1. Locked decisions

These decisions define the MVP. They may be revisited only if implementation evidence shows that a decision is incompatible with Valheim or Jotunn.

- The mod name is **RuneStorage**.
- The plugin identity is `com.runestorage.valheim`, assembly `RuneStorage`, version `0.1.3` for the initial Core build-piece integration.
- The terminal AssetBundle is `runestorage_terminal`; its visual prefab is `RuneStorage_Terminal_Visual` and its icon asset is `Assets/RuneStorage/Terminal/Icons/RuneStorage_Terminal_Icon.png`.
- The RuneStorage Core uses the imported source `Assets/RuneStorage/Core/Source/cyber_core.glb`, visual prefab `RuneStorage_Core_Visual`, build-piece ID `piece_runestorage_core`, icon `Assets/RuneStorage/Core/Icons/RuneStorage_Core_Icon.png`, and AssetBundle `runestorage_core`.
- The Core's embedded `Animation` clip is its looping idle. It animates `Sphere.001_3` and `Sphere.003_4` through rotation and scale channels. Keep the Animator Controller and animated hierarchy included with the Core visual AssetBundle.
- The Core uses the cache-verified `piece_chest_wood` named clone as its runtime shell. Remove the inherited `Container` component so the Core cannot become a second chest-based storage ledger.
- The first buildable piece identity is `piece_runestorage_terminal`.
- The system has two separate buildable pieces:
  - **RuneStorage Terminal:** a runestone-style interaction point and user interface.
  - **RuneStorage Core:** the nearby storage authority and persistent item ledger. Capacity tiers are upgrades to this same placed piece, not separate buildables.
- The terminal does not own item data. The core owns the ledger. If no valid core is available, the terminal refuses storage actions and explains why.
- The MVP uses one core per terminal. If more than one eligible core is in range, the terminal refuses to choose silently and reports that the network is ambiguous. Multi-core networks are a later feature.
- The initial interaction radius is **12 meters**, authored as a tuning value. The exact distance must be tested against Valheim placement and multiplayer behavior before release.
- Stage 1 is mass storage, not a small chest:
  - **20,000 total item units**.
  - **256 distinct item keys**.
- A stackable material contributes its full quantity to total item units but normally consumes one item key. A stored entry may exceed a normal inventory stack and is split into normal stacks when withdrawn.
- Non-stackable items are stored as individual records or as safely equivalent records only when every reconstruction-relevant field matches. Quality, variant, durability, crafter, and custom data must not be flattened into one generic item.
- The MVP stores items in a virtual ledger. It does not spawn a hidden pile of world containers and does not use the terminal inventory as a second storage authority.
- The MVP includes deposit, withdrawal, search, sorting, capacity feedback, save/reload persistence, and multiplayer-safe request validation.
- The terminal UI will use Jotunn's `GUIManager` and Valheim-style UI helpers/anchors rather than requiring a separate player-facing UI mod. The old local mission-board panel is a layout reference only, not a RuneStorage dependency.
- Crafting from the network is explicitly deferred until the basic storage transaction loop is stable. Nearby workbench and forge discovery belongs to the post-MVP crafting phase.
- All prefab-based pieces must use Jotunn's registered named-prefab clone path. The implementation must verify the base prefab through `PrefabManager.Cache` before registration and must not guess a name such as `piece_wood`.
- The plugin must declare a Jotunn dependency and register one-shot prefab content from `PrefabManager.OnVanillaPrefabsAvailable`, unsubscribing the callback after registration.

### Stage capacity model

| Tier | Total item units | Distinct item keys | Status |
|---|---:|---:|---|
| Tier I | 20,000 | 256 | Starting capacity |
| Tier II | 50,000 | 512 | Planned in-place upgrade |
| Tier III | 100,000 | 1,024 | Planned in-place upgrade |

Tier II and Tier III values are planning targets only. Their recipes, upgrade items, resource costs, visual feedback, and release timing remain open until Core persistence and transaction tests pass.

## 2. Goals and non-goals

### Goals

- Let a player store a large amount of normal Valheim item content behind one terminal.
- Combine compatible stacks into readable aggregate totals.
- Preserve item identity and metadata for equipment and other non-stackable content.
- Keep the core as the single source of truth for stored items.
- Make the terminal useful while the core is nearby and clearly unusable when it is missing, too far away, invalid, full, or recovering from a save problem.
- Keep client requests untrusted and validate every deposit, withdrawal, range check, permission check, and capacity change on the authoritative side.
- Persist the ledger across world save, reload, server restart, and plugin reload where Valheim exposes a reliable lifecycle hook.
- Package the mod for Thunderstore and deploy the built plugin to the active Gale profile after the release gates pass.

### Explicitly out of scope for the MVP

- Crafting directly from RuneStorage.
- Automatic pulling from nearby chests, carts, player tombstones, or other inventories.
- Multiple cores forming one shared network.
- Wireless or world-wide terminal access.
- Cross-world, cross-character, cloud, or external database storage.
- A custom permission system that bypasses Valheim's existing ward or build-access rules.
- Automatic item compression, conversion, filtering rules, or logistics pipes.
- A guaranteed atomic save spanning both Valheim's player inventory file and the core's world data. The implementation must use a journal and safe recovery behavior, but this boundary must be tested and documented honestly.
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
2. The terminal validates the player, access rules, terminal distance, core distance, core state, and core revision.
3. The storage screen opens with:
   - total used units and total capacity;
   - distinct-key count and key limit;
   - a search field;
   - deterministic sorting;
   - stored entries with icon, name, available quantity, and whether the entry is stackable or individual;
   - deposit-all and withdrawal controls.
4. If validation fails, the screen does not open or shows a non-destructive error with a retry path.

### Deposit

1. The player selects **Deposit All** or deposits a selected item/quantity.
2. The server reads the player's real inventory. It does not trust item names, counts, or metadata sent by the client UI.
3. The server groups compatible stackable items, calculates the capacity delta, and checks the distinct-key limit and serialized-payload limit.
4. If the operation fits, a journal entry is created, the exact inventory items are removed, and the ledger is committed.
5. The UI refreshes from the core revision and shows the new aggregate totals.
6. If the operation does not fit, nothing is removed and the UI reports the remaining item-unit and key capacity.

### Withdraw

1. The player selects an entry and a quantity, or chooses one complete non-stackable record.
2. The server validates that the entry still exists at the requested revision and that the player can receive the resulting inventory stacks.
3. The ledger reservation is journaled, the item data is reconstructed, and the items are added to the player's inventory.
4. The ledger commit removes only the reserved amount or records.
5. If the inventory cannot accept the full request, the operation is rejected before mutation or is reduced only when the player explicitly chose an available-space-limited action.

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

The repository is currently empty. The following names and paths are proposed implementation boundaries, not existing files.

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
        StorageLedger.cs
        StorageEntry.cs
        StorageKey.cs
        StorageCapacity.cs
        StorageTransaction.cs
        StorageJournal.cs
        StoragePersistence.cs
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
| `RuneStorageCore` | Core identity, range anchor, ledger authority, revision, journal state | Terminal screen state |
| `StorageLedger` | Entry grouping, capacity math, lookup, sorting, deterministic deltas | Player permission decisions |
| `StoragePersistence` | Versioned serialization, save/load, migration, corruption handling | Silent data resets |
| `StorageTransaction` | Deposit/withdraw validation, locks, journal transitions, rollback/refund | UI commands that bypass server validation |
| `RuneStorageTerminal` | Interaction point, nearby-core resolution, UI requests | Permanent item ownership |
| `RuneStorageTerminalUi` | Search, sorting, selection, display, user feedback | Trusting client-supplied quantities or prefab names |
| `AccessValidator` | Player, distance, build-access, ward, and server-authority checks | Granting access when validation is unavailable |
| Package script | Clean build output, manifest, README, icon, DLL placement in ZIP | Shipping source, PDB, or unrelated files |

### UI reuse strategy

Jotunn already exposes a GUI layer intended for custom in-game panels. The planned RuneStorage screen should subscribe to `GUIManager.OnCustomGUIAvailable` and attach its panel beneath Jotunn's scene-safe custom GUI anchors, using the Valheim-styled controls and scaling behavior exposed by that manager. This avoids making RuneStorage depend on a separate end-user UI mod that may not expose a stable developer API.

The existing local file below contains useful patterns for panels, buttons, scroll views, and TextMeshPro rows, but its canvas lifecycle must not be copied blindly. RuneStorage should adapt the layout ideas to Jotunn's custom-GUI lifecycle and destroy/refresh its panel cleanly on scene changes:

```text
C:\Users\magni\Documents\BotsnCoding\Valheim\Households-REPO\OLDmoddedcatoheim\src\MissionSystemMod\MissionBoardPanel.cs
```

The first RuneStorage screen should be a single panel with:

- a title and core status line;
- used units, total units, distinct keys, and key limit;
- a search field;
- a scrollable item-entry list;
- per-entry quantity and withdraw controls;
- `Deposit All` and close buttons;
- a visible error/status area for missing cores, full capacity, stale selections, and recovery mode.

The UI is a view and request builder. It must never become a second ledger or trust its own displayed counts as transaction authority.

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

### Selected terminal visual source

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

### Storage identity and capacity

The ledger needs two related concepts:

- **Total item units:** the number of physical items represented by the ledger.
- **Distinct item keys:** the number of different records needed to reconstruct those items.

The key must include every `ItemDrop.ItemData` field that changes the resulting item or its behavior. The exact field map must be checked against the Valheim assemblies used by the project before implementation. The planned identity includes:

| Item category | Planned grouping rule |
|---|---|
| Stackable material | Aggregate only when prefab identity and all reconstruction-relevant metadata match |
| Stackable item with variant/quality/custom data | Keep separate from otherwise similar items when those fields differ |
| Equipment or tool | Preserve each distinct item record when durability, quality, variant, crafter, or custom data differs |
| Unsupported or unknown metadata | Reject the operation without changing the player's inventory or the ledger |

Stage 1 constraints are:

```text
total_item_units <= 20000
distinct_item_keys <= 256
serialized_payload <= the measured safe core-data budget
```

The serialized-payload condition is intentional. A nominal item count must never be allowed to create an unreadable world object. The safe budget must be measured against the actual Valheim/Jotunn persistence API during Phase 0 and enforced before a mutation.

### Core lifecycle states

```text
Unregistered
  -> Ready
  -> Full
  -> RecoveryRequired
  -> CorruptReadOnly
  -> Destroyed / Unloaded
```

- `Ready`: reads and writes are permitted after validation.
- `Full`: reads remain available; deposits that exceed capacity are rejected without mutation.
- `RecoveryRequired`: the journal contains an unfinished operation; new writes are blocked until recovery completes.
- `CorruptReadOnly`: the payload cannot be decoded or migrated; reads are limited to diagnostics and no destructive reset is allowed.
- `Destroyed / Unloaded`: no new request may use the core reference.

### Persistence format

Use a versioned compact format rather than unbounded ad hoc strings. The planned record contains:

```text
schemaVersion
capacityTier
ledgerRevision
totalItemUnits
distinctKeyCount
entry records
journal record, when an operation is in progress
checksum or equivalent integrity marker
```

The format must support:

- deterministic ordering for stable saves and debugging;
- schema migration from the first released version;
- a bounded payload size check before commit;
- a diagnostic export or log summary without printing private item data unnecessarily;
- refusing to load malformed data without replacing it with an empty core.

### Transaction rules

Every deposit and withdrawal is a server-authoritative transaction:

1. Validate the sender, player state, distance, access, target core, core revision, and request shape.
2. Acquire a per-core operation lock.
3. Re-read the relevant player inventory or ledger entries.
4. Calculate the exact before/after delta.
5. Check capacity, key count, payload size, and inventory space before mutation.
6. Write a journal intent with a unique operation ID.
7. Mutate the player inventory and ledger in the chosen safe order.
8. Commit the new revision and clear the journal only after the operation is complete.
9. Send the result and the new revision to the UI.
10. Release the lock in a `finally`-equivalent cleanup path.

The implementation must never accept a client-supplied prefab name, item count, quality, or metadata as proof that the player owns an item. The server reads the actual inventory and matches the requested operation to it.

### Interrupted operations

If a disconnect, save, or plugin unload occurs during a transaction:

- the journal remains visible to the next load;
- the core enters `RecoveryRequired` instead of accepting new writes;
- recovery compares the journal's expected delta with the available player/core state;
- the system either completes the recorded operation exactly once or refunds the reserved side without silently duplicating it;
- if the player is unavailable or the state cannot be reconciled, the core stays blocked and logs a precise recovery reason;
- no recovery path clears the ledger or destroys the journal automatically.

The plan must include a documented limitation for the boundary between world-object persistence and character-inventory persistence. A successful normal transaction is required to be loss-free in testing; crash behavior must be verified and any remaining engine limitation must be visible to the player rather than hidden.

## 6. Configuration, permissions, and integrations

### Proposed configuration

The first configuration file should expose only values that are safe to tune without changing the storage schema:

```ini
[Storage]
CoreRangeMeters = 12
Stage1TotalItemUnits = 20000
Stage1DistinctItemKeys = 256
AllowNonStackableItems = true
EnableDebugLogging = false

[Access]
UseValheimBuildAndWardAccess = true

[UI]
ItemsPerPage = 40
```

The capacity defaults are authored targets. The implementation must clamp invalid values and log the effective values at startup. A configuration change must not silently shrink an existing core below its stored contents.

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
- Use a per-core lock and monotonic revision to reject stale requests.
- Re-read the actual inventory for deposits.
- Reserve exact ledger entries for withdrawals.
- Use an operation journal with unique IDs and recovery states.
- Never retry a mutation blindly after an exception.
- Do not clear a stuck or corrupt core as a convenience action.
- Log operation ID, core ID, player ID, old revision, new revision, and outcome without logging full custom item data by default.

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
- Ledger decode failure: enter read-only or blocked recovery mode; never replace data with an empty ledger.
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
| One core in range | Terminal opens the correct core | Manual test with core identity/revision log |
| Multiple cores in range | Terminal refuses ambiguity without moving items | Manual test and log |
| Deposit one stack | Exact amount leaves inventory and appears in the ledger | Before/after inventory and ledger count |
| Deposit many stacks | Compatible stacks aggregate into one entry and total units are correct | Ledger display and serialized record inspection |
| 20,000-unit boundary | Final fitting deposit succeeds; excess deposit changes nothing | Capacity test with exact counts |
| 256-key boundary | Final fitting key succeeds; next new key is rejected without mutation | Key-count test |
| Non-stackable equipment | Quality, durability, variant, crafter, and custom data survive storage/withdrawal | Item field comparison before and after |
| Search and sorting | Results are deterministic and do not change the ledger | UI test across refreshes |
| Partial withdrawal | Requested amount is removed from the ledger and added to inventory exactly once | Before/after counts |
| No inventory space | Withdrawal is rejected or reduced only by an explicit user action | Inventory-full test |
| Save and reload | Stored items remain available with the same totals and metadata | World reload test |
| Plugin restart | Core loads without resetting data | Restart log and ledger comparison |
| Interrupted deposit | Recovery does not duplicate or silently lose the operation | Forced disconnect/save-boundary test |
| Interrupted withdrawal | Reserved items are restored or delivered exactly once | Forced disconnect/save-boundary test |
| Malformed payload | Core becomes blocked/read-only and preserves diagnostic data | Corruption test and log |
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
- [ ] Decide whether the first visual comes from an approved author-made AssetBundle or a verified vanilla base visual with a custom child.
- [x] Stage `terminal1.glb` into the Unity asset project without altering the original source file.
- [x] Inspect `terminal1.glb` in Unity and confirm scale, orientation, pivot, embedded materials/textures, collider behavior, and the intended terminal prefab root.
 - [x] Record the final terminal visual prefab name and AssetBundle label in the project plan before code attaches it to the Jotunn clone.
- [x] Defer terminal particles for the MVP; use the imported runestone terminal visual as-is.
- [ ] If an effect is revisited later, inspect it in a separate test prefab before changing the terminal visual.
- [ ] Map the current Valheim `ItemDrop.ItemData` fields needed to reconstruct stackable and non-stackable items.
- [ ] Measure the safe persistence payload budget for the selected ZDO/world-data API.
- [ ] Define the first schema version and journal state transitions.
- [ ] **Verify:** The repository has a clean buildable skeleton, the dependency versions are recorded, base prefab names are runtime-verified, and the capacity/persistence constraints are measurable.

### Phase 1 - Plugin and safe piece registration

 - [x] Add the BepInEx plugin entry point and `[BepInDependency(Jotunn.Main.ModGuid)]`.
 - [x] Subscribe to `PrefabManager.OnVanillaPrefabsAvailable` and unsubscribe after one-shot registration.
 - [x] Register the terminal with `new CustomPiece(customName, verifiedBasePrefabName, pieceConfig)` using a verified base name.
- [x] Register the Core with the same named-clone path and its unique piece, visual, icon, and bundle identifiers.
- [x] Attach the terminal/Core visuals after cloning and before adding each piece to its Jotunn manager.
- [x] Attach the built terminal visual from `terminal1.glb` after the named Jotunn terminal clone is created, disabling only the base renderers that the visual replaces.
- [x] Do not attach a smoke or particle effect to the MVP terminal.
- [ ] Add distinct icons and Hammer recipes once the visual and piece identities are stable.
- [x] Add startup logs for plugin version, resolved base prefabs, piece names, and registration result; the 0.1.3 profile log confirms both bundles and pieces registered with `piece_chest_wood`.
- [ ] **Verify:** Both pieces appear as valid buildable objects and their ghosts/placement work. The user confirmed Core ghost and placed alignment on 2026-10-18; hover/support/health checks still need testing.

### Phase 2 - Core ledger and persistence

- [ ] Implement `StorageKey` and the metadata-preserving item record model.
- [ ] Implement Stage 1 capacity math for 20,000 total units and 256 distinct keys.
- [ ] Implement deterministic grouping for compatible stackable items.
- [ ] Implement individual-record handling for non-stackable items.
- [ ] Implement the versioned persistence codec with size checks, checksum/integrity validation, and migration hooks.
- [ ] Attach the ledger to the placed core's valid runtime network object and persist the core revision.
- [ ] Implement `Ready`, `Full`, `RecoveryRequired`, and `CorruptReadOnly` states.
- [ ] Add tests for grouping, capacity boundaries, metadata preservation, deterministic ordering, malformed data, and schema version rejection.
- [ ] **Verify:** A placed core can save and reload a populated ledger without losing item identity, exceeding the capacity rules, or resetting malformed data to empty.

### Phase 3 - Server-authoritative transactions

- [ ] Implement the per-core operation lock and monotonic revision check.
- [ ] Implement deposit preview without mutation.
- [ ] Implement exact deposit transactions with journal intent, inventory validation, ledger delta, commit, and cleanup.
- [ ] Implement exact withdrawal transactions with ledger reservation, inventory-space validation, item reconstruction, commit, and cleanup.
- [ ] Implement the interrupted-operation recovery path and block new writes while recovery is unresolved.
- [ ] Add access, distance, sender, core-state, and stale-revision validation at every request boundary.
- [ ] **Verify:** Deposit and withdrawal tests pass at the normal path, capacity boundaries, stale-request path, full-inventory path, out-of-range path, and forced-interruption path without duplication or silent item loss.

### Phase 4 - Terminal interaction and UI

- [ ] Implement nearby-core resolution using the configured radius and the one-core-only MVP rule.
- [ ] Implement terminal interaction state and direct no-core/ambiguous-core/recovery messages.
- [ ] Subscribe the screen to Jotunn `GUIManager.OnCustomGUIAvailable` and attach it to the scene-safe custom GUI anchor instead of maintaining an unmanaged permanent canvas.
- [ ] Reuse the local mission-board panel's proven layout ideas for a scroll view, buttons, and TextMeshPro rows while adapting lifecycle and styling to Jotunn.
- [ ] Implement the storage screen with total capacity, key count, search, deterministic sorting, item icons, quantities, deposit-all, and withdrawal controls.
- [ ] Refresh the screen from the core revision after every successful mutation and reject stale selections.
- [ ] Add clear full-capacity, unsupported-item, inventory-full, and data-error feedback.
- [ ] **Verify:** A player can place the two pieces, open the terminal, deposit a large material batch, find it by search, withdraw a chosen amount, and see correct status after moving the core out of range.

### Phase 5 - Multiplayer, reload, and hardening

- [ ] Run host/client tests with matching and mismatched plugin versions.
- [ ] Validate server-side ownership/access behavior against the selected Valheim ward/build-access API.
- [ ] Test simultaneous deposit and withdrawal requests against the same core.
- [ ] Test player disconnects, server restart, world save/reload, terminal destruction, core destruction, and plugin unload during normal and journaled operations.
- [ ] Add rate limiting or request debouncing if UI spam can create excessive RPC or ledger work.
- [ ] Review logs for sensitive data, stale references, duplicate registrations, and exception paths.
- [ ] **Verify:** Multiplayer and lifecycle tests show one authoritative ledger revision per committed operation and no unreconciled transaction remains hidden.

### Phase 6 - Upgrade path and post-MVP systems

- [ ] Persist the Core's capacity tier and add an in-place upgrade path without changing its build-piece identity.
- [ ] Define and implement the Tier II upgrade item, recipe, validation, and visual/status feedback.
- [ ] Add Tier III only after Tier II persistence and migration tests pass.
- [ ] Decide whether multiple cores should merge into a network or remain separate storage systems.
- [ ] Design the crafting integration around verified nearby workbench/forge requirements.
- [ ] Add craft-from-storage preview, ingredient reservation, output delivery, and failure recovery as a separate transaction family.
- [ ] **Verify:** Upgrading a populated Core in place preserves every existing entry, increases capacity without changing item identity or build-piece identity, and survives save/reload before crafting work begins.

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
- Exact Hammer category and recipe costs.
- Whether the first public MVP permits non-stackable equipment or initially limits storage to safely reconstructable stackable items.
- The safe serialized payload budget and whether compression is needed.
- The precise recovery behavior available when a player disconnects between a world-object save and a character-inventory save.
- The final Stage 1 interaction radius after placement and multiplayer tests.
- Whether build/ward access should be required for both placement and terminal use.
- The first public package version after Phase 7 passes.
