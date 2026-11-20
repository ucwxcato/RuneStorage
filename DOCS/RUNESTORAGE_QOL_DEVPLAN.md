# RuneStorage - Quality-of-Life and Core Feedback Plan

> **Status (2026-11-20):** Planned, not started. Nothing in this file is implemented. Items 1 (network version check) and 2 (admin-synced config) are done and live in `RUNESTORAGE_DEVPLAN.md`, Phase 5 and section 6.
>
> **Authority:** This file owns the QoL features below. The canonical plan is `RUNESTORAGE_DEVPLAN.md`. Any change to Core inventory authority, transactions, or tier rules must be made there first.
>
> **Scope:** Eight player-facing quality-of-life items and two follow-ups from the 2026-11-20 feature review: "Can craft" filter (11) and Core fill-level glow (15).

## 0. Ground rules for every item

- Storage authority does not change. The Core's native `Container`/`Inventory` is the only item store. Nothing here adds a ledger, a cached inventory copy used for a transaction, or a second serializer.
- Views (category filter, sort, pins, search, can-craft preview) are client-side and never decide a transaction. Any mutation (deposit matching, hotbar/worn exclusion, click-to-take) goes through the existing server-validated transfer path and rechecks range, access, capacity, and stale inventory on the owner.
- Whether a remote client's copy of the Core's contents is fresh is **not verified**. Anything a client must show about the Core's fill level, such as hover text or the glow, uses a value the server writes to the Core's ZDO (`RuneStorageFilledSlots`), so it does not depend on that copy. Shared groundwork in Phase A covers this.
- Anything a player stores for themselves (pins) is per character, not per Core, and must not be treated as proof of anything by the server.
- **Hard rule (owner decision, 2026-11-20):** Hotbar items and worn (equipped) items are never moved by a bulk deposit: Deposit All, Deposit Everything, or deposit matching. The owner enforces this on the server, including for remote requests, and it is not a setting or per-character option. These items stay visible in the Deposit tab. Explicit single-item deposits are an open question (see D9).
- Every Valheim type, enum, field, and key name used by an item must be checked against `lib/BepInEx/` and `assembly_valheim.dll` at implementation time. Do not write names from memory.
- UI follows the Jötunn `GUIManager` and Valheim-styled controls rules from `AGENTS.md`. Antislop UI and accessibility skills apply.

## 1. Decisions and open questions

| # | Question | Recommendation | Status |
|---|----------|----------------|--------|
| D1 | Pins: key by item prefab name (all qualities merge) or by full item identity (quality, durability, crafter)? | Prefab name. A pinned Iron Sword stays pinned for every copy. Identity-level pins are noisy. | Open |
| D2 | Hotbar and worn items | **Resolved (owner, 2026-11-20):** never deposited by any path. Replaces the earlier protected-slot toggle, which is removed. | Decided |
| D3 | Should the hotbar and worn exclusion be enforced on the server? | **Resolved:** yes. The owner rejects or skips those items in every deposit path. Client-side hiding is for display only. | Decided |
| D8 | Should hotbar and worn items appear in the Deposit tab list? | **Resolved (owner, 2026-11-20):** yes, keep them visible. Do not hide them. Only the bulk deposit buttons skip them. Note: worn items are currently hidden from that list, so this changes existing behavior. | Decided |
| D9 | Should explicit single-item deposits (Deposit 1/5/X on a row) also refuse hotbar and worn items? | Unanswered. The owner's latest instruction names only Deposit All. Their earlier wording ("ever") may cover single-item deposits too. Ask before implementing. | Open |
| D4 | Deposit matching: which items count as "matching"? | Items that would merge into an existing Core row under `RuneStorageItemGroups.GetIdentity`. Because identity includes durability and the equipped flag, damaged or equipped gear will rarely match. That is the intended safety behavior. | Open |
| D5 | Default hotkeys (deposit matching, click shortcuts) | Config-driven `KeyCode` entries, defaulted to a key that does not conflict with Valheim bindings. Pick the default after checking the Valheim input map. | Open |
| D6 | Category source | Map from the item's `ItemType` (verify the enum), with trophies and ammo handled explicitly. | Open |
| D7 | Glow scale | Linear from empty to full, with a configurable threshold below which the glow stays off. | Open |

## 2. Phase A - Shared groundwork

- [x] Verify in `assembly_valheim.dll` the `ItemDrop.ItemData` type fields and `ItemDrop.ItemData.ItemType` enum values needed for category mapping (item 5), and record the exact names here.
- [x] Verify the input API. `UnityEngine.Input.GetKey`, `GetKeyDown`, and `GetKeyUp` are public static methods in `UnityEngine.InputLegacyModule.dll` (probe, 2026-11-20). Modifier keys use `GetKey` with `KeyCode.LeftShift` and `KeyCode.LeftControl`. Hotkey code is not written yet.
- [x] Verify item fields and enum (build-time probe of `assembly_valheim.dll`, 2026-11-20). `ItemDrop.ItemData` has `m_stack`, `m_gridPos`, `m_equipped`, `m_shared`, `m_customData`, `m_quality`, `m_variant`, `m_worldLevel`, `m_durability`, `m_crafterID`, `m_crafterName`, `m_dropPrefab`, and `m_cheated`. `ItemType` values: None, Material, Consumable, OneHandedWeapon, Bow, Shield, Helmet, Chest, Ammo, Customization, Legs, Hands, Trophy, TwoHandedWeapon, Torch, Misc, Shoulder, Utility, Tool, Attach_Atgeir, Fish, TwoHandedWeaponLeft, AmmoNonEquipable, Trinket.
- [x] Confirm `SharedData` has no footprint size (only `m_maxStackSize` and `m_setSize`), so each item occupies one inventory cell and "slots" equals the number of stored items. The terminal's existing "occupied slots" count (`allItems.Count`) is therefore correct.
- [x] Confirm `Inventory.Changed(bool, bool)` is private and `Inventory.m_onChanged` is public. Confirm `ZDO.Set(string, int)` and `ZDO.GetInt(string, out int)`.
- [x] Add a server-written fill count (ZDO key `RuneStorageFilledSlots`, int) in `RuneStorageCoreMarker`. Owner-only and throttled to once per second, so it covers every mutation path (deposit, withdraw, craft, upgrade, load) without one hook per call site. Clients read it and never write it. Build evidence only.
- [x] Update rule: the fill count is republished when it changes, so it is never stale for more than about 1 second. A first publish happens on the first owner update after load. Build evidence only; in-game timing is unverified.
- [x] Add the per-character pins store (`Terminal/RuneStoragePinStore.cs`). Decided format: a plain text file at `BepInEx\config\RuneStorage\pins.txt`, one `playerId<TAB>prefabName` line per pin. Keyed by the character's player ID, so it is not shared between characters or worlds. Pins are keyed by prefab name (D1). Lines that cannot be parsed are kept unchanged and never dropped. The server never reads this file. Build evidence only; the pin UI is item 7.
- [x] Use `Inventory.GetHotbar()` as the source of hotbar membership for item 4, instead of hard-coding a row index. The probe confirmed the method exists on `Inventory` and returns a `List<ItemDrop.ItemData>`. Its exact row definition is not visible to reflection, so confirm it in game: the hotbar items the method returns must match what the hotbar shows (Verify item below).

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

- [ ] **Known gap in shipped code (2026-11-20):** Deposit All and Deposit Everything (`RuneStorageTransfers`, bulk path) skip only `m_equipped` items, so hotbar items are deposited today. Fix this first (suggested order step 0).
- [ ] Add one server-side predicate, `IsStorable(item, inventory)`, that returns false for equipped items and for items in the hotbar row. Bulk paths on the owner call it: Deposit All, Deposit Everything, and deposit matching, including remote requests.
- [ ] A bulk request skips hotbar and worn items and reports them in the result note, for example "Hotbar and worn items were not stored."
- [ ] Hotbar and worn items stay visible in the Deposit tab (D8). Their Deposit 1/5/X controls follow D9.
- [ ] The owner excludes hotbar and worn items even when a client sends an identity that matches a stored group, so a crafted bulk request cannot bypass the rule.
- [ ] Confirm the hotbar row index against the engine's inventory layout (Phase A) before relying on it.
- [ ] **Verify:** Deposit All, Deposit Everything, and deposit matching never move a hotbar or worn item, for host and remote client. The result note names what was skipped. Items already in the Core are unaffected. The rule holds after restart and after changing the hotbar layout.

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
| Save/reload and restart | Pins and fill count survive; hotbar and worn items stay out of the Core |
| Authority | Owner revalidates every mutation; a crafted or stale request changes nothing |
| Non-RuneStorage containers | Chest and other container hover text and interaction are unchanged |
| Logs | No exceptions; debug output off by default |

## 8. Suggested order

0. Hotfix: close the hotbar and worn-item gap in item 4 before any other deposit work.
1. Phase A: groundwork and the fill count. Everything later depends on it.
2. Phase B: hover text (6), click shortcuts (8), pins (7).
3. Phase C: category filter (5), can-craft (11).
4. Phase D: deposit matching (3), then the hotbar and worn-item rule (4). The rule itself is the hotfix in step 0.
5. Phase E: fill glow (15).

## 9. Open tuning points

- D1, D4 to D7, and D9 in section 1 (D2, D3, and D8 are decided).
- Whether the Stored tab's aggregate row count changes with category filters (expected: no; the filter only hides rows).
