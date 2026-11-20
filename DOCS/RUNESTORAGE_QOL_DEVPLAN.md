# RuneStorage - Quality-of-Life and Core Feedback Plan

> **Status (2026-11-20):** Planned, not started. Nothing in this file is implemented. Items 1 (network version check) and 2 (admin-synced config) are done and live in `RUNESTORAGE_DEVPLAN.md`, Phase 5 and section 6.
>
> **Authority:** This file owns the QoL features below. The canonical plan is `RUNESTORAGE_DEVPLAN.md`. Any change to Core inventory authority, transactions, or tier rules must be made there first.
>
> **Scope:** Eight player-facing quality-of-life items and two follow-ups from the 2026-11-20 feature review: "Can craft" filter (11) and Core fill-level glow (15).

## 0. Ground rules for every item

- Storage authority does not change. The Core's native `Container`/`Inventory` is the only item store. Nothing here adds a ledger, a cached inventory copy used for a transaction, or a second serializer.
- Views (category filter, sort, pins, search, can-craft preview) are client-side and never decide a transaction. Any mutation (deposit matching, protected-slot skip, click-to-take) goes through the existing server-validated transfer path and rechecks range, access, capacity, and stale inventory on the owner.
- Clients do not receive the Core's inventory contents. Anything a client must show about the Core's fill level, such as hover text or the glow, needs a value the server writes to the Core's ZDO. Shared groundwork in Phase A covers this.
- Anything a player stores for themselves (pins) is per character, not per Core, and must not be treated as proof of anything by the server.
- **Hard rule (owner decision, 2026-11-20):** Hotbar items and worn (equipped) items are never deposited, by any path: Deposit All, Deposit Everything, deposit matching, single-item Deposit 1/5/X, or a remote request. This is not a setting and not per character. The owner enforces it on the server, and clients cannot override it.
- Every Valheim type, enum, field, and key name used by an item must be checked against `lib/BepInEx/` and `assembly_valheim.dll` at implementation time. Do not write names from memory.
- UI follows the Jötunn `GUIManager` and Valheim-styled controls rules from `AGENTS.md`. Antislop UI and accessibility skills apply.

## 1. Decisions and open questions

| # | Question | Recommendation | Status |
|---|----------|----------------|--------|
| D1 | Pins: key by item prefab name (all qualities merge) or by full item identity (quality, durability, crafter)? | Prefab name. A pinned Iron Sword stays pinned for every copy. Identity-level pins are noisy. | Open |
| D2 | Hotbar and worn items | **Resolved (owner, 2026-11-20):** never deposited by any path. Replaces the earlier protected-slot toggle, which is removed. | Decided |
| D3 | Should the hotbar and worn exclusion be enforced on the server? | **Resolved:** yes. The owner rejects or skips those items in every deposit path. Client-side hiding is for display only. | Decided |
| D8 | Should hotbar and worn items appear in the Deposit tab list? | Hide them, since worn items are already hidden. A single line of text explains that hotbar and worn items are never stored. | Open |
| D4 | Deposit matching: which items count as "matching"? | Items that would merge into an existing Core row under `RuneStorageItemGroups.GetIdentity`. Because identity includes durability and the equipped flag, damaged or equipped gear will rarely match. That is the intended safety behavior. | Open |
| D5 | Default hotkeys (deposit matching, click shortcuts) | Config-driven `KeyCode` entries, defaulted to a key that does not conflict with Valheim bindings. Pick the default after checking the Valheim input map. | Open |
| D6 | Category source | Map from the item's `ItemType` (verify the enum), with trophies and ammo handled explicitly. | Open |
| D7 | Glow scale | Linear from empty to full, with a configurable threshold below which the glow stays off. | Open |

## 2. Phase A - Shared groundwork

- [ ] Verify in `assembly_valheim.dll` the `ItemDrop.ItemData` type fields and `ItemDrop.ItemData.ItemType` enum values needed for category mapping (item 5), and record the exact names here.
- [ ] Verify the Valheim input API used by Valheim's own UI (`Input.GetKey`, `Input.GetKeyDown`, and modifier handling) against `UnityEngine.InputLegacyModule` before writing hotkey code (item 3, 8).
- [ ] Verify how the terminal currently obtains Core contents on the client (the existing snapshot/refresh path in `RuneStorageTerminalPanel`). Items 11 and any client display depend on this path, so document it before reusing it.
- [ ] Add a server-written fill count to the Core's ZDO (for example key `RuneStorage.FilledSlots`, int). Update it after every successful Core inventory mutation (deposit, withdraw, craft output and consumption, tier upgrade, and load). Clients read it; they never write it.
- [ ] Decide the update rule for the fill count so it is not stale after a native save load. Recompute on Core `Awake` after native load and on every mutation path.
- [ ] Add a per-character client settings store for pins. Location and format are decided in the first implementation step, and must be keyed by the character's player ID, not by Core or world.
- [ ] Verify the hotbar slot indices for Valheim's player inventory (the hotbar row in `Inventory` grid positions). The hotbar exclusion (item 4) must match the engine's actual layout, not an assumed one.
- [ ] **Verify (Phase A):** The fill count matches the Core's occupied slot count after deposit, withdraw, craft, tier upgrade, and save/reload on the host, and a client reads the same value.

## 3. Phase B - Small, low-risk items

### Item 6 - Slot usage in hover text

- [ ] Extend the existing Core `Container.GetHoverText` Harmony patch (`CoreContainerHoverTextPatch`) to show `RuneStorage Core - {filled}/{capacity} slots (Tier {tier})`. Non-RuneStorage containers stay untouched.
- [ ] Show the same readout on the terminal's hover text, reading the Core's fill count and tier through its ZDO and the tier capacity table.
- [ ] Use the Phase A fill count, not a client-side inventory scan, so the value is correct for clients.
- [ ] **Verify:** Hover text matches the Stored tab's occupied-slot count for a populated Core on host and client, and a remote client sees the same numbers after the host deposits.

### Item 8 - Click shortcuts and search focus

- [ ] Shift-click on a Stored row takes one full stack of that group, using the stack size from the item's shared data. The client sends an exact amount. The owner rechecks available quantity and stack limits before moving anything.
- [ ] Ctrl-click takes one item. Its meaning matches the existing "1" button, and the server path is unchanged.
- [ ] Focus the search field when the terminal opens. Keep Escape behavior unchanged.
- [ ] Shortcut keys come from config (D5). Hold-state detection uses the verified Valheim input API from Phase A.
- [ ] **Verify:** Shift-click, Ctrl-click, and search focus behave on Stored and Deposit tabs. A shortcut that exceeds what the Core holds is refused without changing either inventory. Keyboard focus reaches the search field without a mouse.

### Item 7 - Pinned favorites

- [ ] Add a pin toggle on each Stored row and tile. Pins are stored per character, keyed by prefab name (D1).
- [ ] Pinned groups sort to the top of the Stored tab, then fall back to the active sort order. Pinning never changes the aggregate grouping.
- [ ] A pinned group that is absent from the Core is kept in the store and shown only when present, so pins survive temporary emptiness.
- [ ] Pins are view-only. They never affect deposit, withdraw, or craft.
- [ ] **Verify:** Pins survive a game restart and a character switch, stay per character, and do not change the aggregate row count or totals.

## 4. Phase C - View filters and craft visibility

### Item 5 - Category filters

- [ ] Add a category selector (Materials, Food, Potions, Weapons, Armor, Ammo, Trophies, Other) to the Stored and Deposit tabs. It combines with search and sort.
- [ ] Map categories from `ItemType` (D6). Ammo and trophies use explicit rules if the enum does not separate them cleanly.
- [ ] Category filtering is view-only. A filter with zero matches shows an explicit empty state, not a silent blank list.
- [ ] **Verify:** Each category shows only its items on Stored and Deposit, counts match totals, and combining with search and sort gives the expected rows.

### Item 11 - "Can craft" filter and missing-ingredient display

- [ ] On the Craft tab, add a "Can craft now" filter. A recipe is listed as craftable when the Core's stored ingredient totals (Phase A path) cover every requirement at the nearby station.
- [ ] For each recipe row that is not craftable, show what it is short of, for example `Need 4 more Iron (have 6/10)`, computed from the same ingredient totals.
- [ ] The preview is advisory. The craft request still goes through the owner-side unlock, station, level, and ingredient revalidation. A preview that says craftable must never be trusted to consume items.
- [ ] Recipes that use upgrader costs (see the Hand-recipe correction in the main devplan) show the same upgrader rule as the craft path.
- [ ] **Verify:** Filter results match the craft outcome for a sample set of recipes, including one short by one item, one fully stocked, and one with an under-level station. A craft attempted from a recipe the preview marked craftable still succeeds or fails with the server's message, never silently.

## 5. Phase D - Transactions

### Item 3 - Deposit matching button and hotkey

- [ ] Add a "Deposit matching" action on the Deposit tab, and a configurable hotkey (D5) active only while the terminal panel is open.
- [ ] Client builds the list of player items whose identity (D4) matches an existing Core group, then sends it as a deposit-all-style request. The owner rebuilds the plan from its own inventory and ignores the client's list of quantities.
- [ ] Reuse `StorageTransferPlanner` for the preview. Deposit matching is all-or-nothing per group only where the existing "Deposit All" rule is all-or-nothing, and partial results report what did not fit, matching Deposit All.
- [ ] Never include hotbar or worn items (item 4).
- [ ] **Verify:** Matching deposits move only items that merge into existing rows. Full-Core and partial-fit cases report shortfall without losing items. The owner's inventories match before and after on host and remote client, and an out-of-range or unauthorized request changes nothing.

### Item 4 - Hotbar and worn items are never deposited

This replaces the earlier protected-slot idea. There is no toggle and nothing is stored per character.

- [ ] **Known gap in shipped code (2026-11-20):** Deposit All and Deposit Everything (`RuneStorageTransfers`, bulk path) skip only `m_equipped` items, so hotbar items are deposited today. The single-item identity deposit path has no server-side equipped or hotbar check; it relies on the client's list. Both must be fixed before any new deposit feature ships.
- [ ] Add one server-side predicate, `IsStorable(item, inventory)`, that returns false for equipped items and for items in the hotbar row. Every deposit path calls it on the owner: Deposit All, Deposit Everything, deposit matching, and single-item deposits, including remote requests.
- [ ] A single-item request for a hotbar or worn item is refused with a clear message, and no items move. A bulk request skips those items and reports them in the result note, for example "Hotbar and worn items were not stored."
- [ ] Hide hotbar and worn items from the Deposit tab list (D8), with one line of text that explains the rule.
- [ ] Hotbar and worn items are excluded on the owner even when the client sends an identity that matches a stored group.
- [ ] **Verify:** Hotbar and worn items never move through any deposit path, for host and remote client. A crafted request naming a hotbar or worn item is refused. Items already in the Core are unaffected. The rule holds after restart and after changing the hotbar layout.

## 6. Phase E - Visual feedback

### Item 15 - Core fill-level glow

- [ ] Verify the Core's `cyber_core` material setup: which shader, whether `_EmissionColor` is exposed, and whether emission is enabled. Record the names here before writing code.
- [ ] Drive glow from the Phase A fill count (fill ratio = filled slots / capacity), never from a client-side inventory scan.
- [ ] Use a `MaterialPropertyBlock` on the Core's renderers so the shared material is not changed and no per-frame material clones are created.
- [ ] Apply the linear ramp and threshold from D7 and update only when the fill count changes, not every frame.
- [ ] Keep the glow client-side and visual only. It must not affect the animation controller or the Core's ZDO beyond the fill count.
- [ ] **Verify:** The glow matches the fill ratio on host and client, updates after a deposit or withdraw, is correct after save/reload and tier upgrade, and the Core's idle animation still plays.

## 7. Verification matrix

Each item needs all of the following before it is marked complete.

| Check | Required evidence |
|-------|-------------------|
| Clean Release build | 0 warnings, 0 errors, DLL SHA-256 recorded |
| Host, single player | Item behaves as described in its Verify line |
| Remote client | Same behavior, and any displayed Core state matches the host |
| Save/reload and restart | Pins, protected slots, and fill count survive |
| Authority | Owner revalidates every mutation; a crafted or stale request changes nothing |
| Non-RuneStorage containers | Chest and other container hover text and interaction are unchanged |
| Logs | No exceptions; debug output off by default |

## 8. Suggested order

0. Hotfix: close the hotbar and worn-item gap in item 4 before any other deposit work.
1. Phase A: groundwork and the fill count. Everything later depends on it.
2. Phase B: hover text (6), click shortcuts (8), pins (7).
3. Phase C: category filter (5), can-craft (11).
4. Phase D: deposit matching (3), then protected slots (4).
5. Phase E: fill glow (15).

## 9. Open tuning points

- D1 to D7 in section 1.
- Whether the Stored tab's aggregate row count changes with category filters (expected: no; the filter only hides rows).
- Whether protected slots should also block the player's own single-item deposits (current recommendation: no).
