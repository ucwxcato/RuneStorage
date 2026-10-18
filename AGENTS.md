# RuneStorage repository instructions

## Project scope

RuneStorage is a Valheim mod that provides a buildable runestone terminal connected to a nearby mass-storage core.

The canonical implementation plan is:

```text
DOCS/RUNESTORAGE_DEVPLAN.md
```

Keep that plan and the implementation synchronized. Do not mark checklist items complete without build, deployment, or runtime evidence at the level the item claims.

The selected RuneStorage Terminal visual source is:

```text
C:\Users\magni\Documents\BotsnCoding\Valheim\ASSETS-Repo\assetsetuff\Digital-Storage\Source\terminal1.glb
```

Treat the original GLB as source material. Do not overwrite it. Stage a working copy in the RuneStorage Unity project, verify its scale, orientation, pivot, materials, embedded textures, and collider behavior, then export the approved visual as the RuneStorage AssetBundle prefab.

The selected RuneStorage Core visual source is:

```text
Unity\RuneStorage-Valheim\Assets\RuneStorage\Core\Source\cyber_core.glb
```

This GLB contains one 10-second transform animation. Use it as the core's looping idle animation through a Unity Animator Controller; preserve the animated child transforms when building the visual prefab and AssetBundle.

Keep the dedicated Unity project inside this repository at:

```text
Unity\RuneStorage-Valheim\
```

Keep Unity-generated `Library`, `Temp`, `Obj`, `Build`, `Builds`, `Logs`, and `UserSettings` data out of version control. Keep `Assets`, `Packages`, and `ProjectSettings` under the repository when the project is created.

### Vanilla effect reuse (deferred)

The local ripped reference project contains smoke candidates at:

```text
Assets\world\SmokeFire\SmokeParticleSystem.prefab
Assets\GameElements\StatusEffects\effects\vfx_Smoked.prefab
```

The RuneStorage MVP intentionally uses the terminal GLB as-is with no particle child or smoke effect. The previously staged authorized smoke files are optional reference work only; do not attach, register, or include them in the terminal AssetBundle unless the user reopens the effect decision later. Never guess a prefab name from the ripped project. Do not use `demister_ball` for the terminal mist; it is the orb-like demister effect.

## Valheim prefab registration rule

When creating or updating RuneStorage custom items, buildable pieces, props, terminals, cores, or other prefab-based content, use Jötunn's registered named-prefab clone path so vanilla runtime references remain valid.

- Add `[BepInDependency(Jotunn.Main.ModGuid)]` to the plugin.
- Register prefab-based content from `PrefabManager.OnVanillaPrefabsAvailable`.
- Unsubscribe a one-shot registration callback after it runs.
- Resolve and verify each vanilla base prefab name through `PrefabManager.Cache` before registration. Never guess a name such as `piece_wood` without confirming that the cache contains it.
- For buildable pieces, use Jötunn's named clone constructor:

  ```csharp
  new CustomPiece(customName, verifiedBasePrefabName, pieceConfig)
  ```

- Use the equivalent Jötunn named-clone constructor for other prefab-based content.
- Do not use `UnityEngine.Object.Instantiate(PrefabManager.Cache.GetPrefab<GameObject>(...))` as the final custom prefab and then wrap that direct clone in `CustomPiece`.
- Direct cached-prefab instantiation can leave `WearNTear`, `ZNetView`, and other runtime references invalid, causing placement-ghost, placement, support, hover, or health `NullReferenceException` errors.
- Attach custom visual children and assign custom icons after the Jötunn clone is created and before adding the custom piece or item to its manager.
- Disable only the base visual renderers replaced by the custom visual.
- Log the resolved base prefab names and registration result.

## RuneStorage architecture rules

- The RuneStorage Core is the authoritative owner of stored item data.
- The RuneStorage Terminal is only an interaction point and UI. It must not maintain a second item ledger.
- Stage 1 targets 20,000 total item units and 256 distinct item keys. Do not silently reduce this to a small chest-style slot count.
- RuneStorage has one buildable Core identity (`piece_runestorage_core`). Capacity tiers belong to that placed Core and upgrades change it in place; do not create separate Mk I/Mk II/Mk III build pieces.
- The Core's named clone currently uses the cache-verified `piece_chest_wood` runtime shell. Remove its inherited `Container` so it cannot provide a parallel chest inventory beside the Core ledger.
- One terminal uses exactly one eligible nearby core in the MVP. If multiple cores are in range, refuse ambiguous access instead of choosing silently.
- Validate terminal distance, core distance, player access, core state, ledger revision, capacity, and inventory state on the authoritative side immediately before mutation.
- Never trust client-supplied item names, counts, quality, durability, variant, crafter, or custom data as proof of ownership.
- Aggregate only items whose complete reconstruction-relevant identity matches.
- Preserve quality, variant, durability, crafter, custom data, and any other fields needed to reconstruct non-stackable items.
- Reject unsupported or unknown item metadata without changing either the player's inventory or the core.
- Use a per-core lock, monotonic revision, operation ID, and journal for deposit and withdrawal transactions.
- Never retry a failed mutation blindly.
- Do not clear malformed or interrupted storage data automatically. Block the core, preserve diagnostics, and require a deliberate recovery path.
- Keep crafting from storage, automatic chest intake, multi-core networks, and wireless access out of the MVP unless the development plan is explicitly updated.

## UI rule

- Use Jötunn's `GUIManager` for the RuneStorage screen rather than requiring a separate player-facing UI mod.
- Subscribe to `GUIManager.OnCustomGUIAvailable` and attach the screen beneath Jötunn's scene-safe custom GUI anchor.
- Use Valheim-styled controls and scaling where available.
- The UI is a view and request builder. It must never become a second storage authority or trust displayed counts during a transaction.
- The existing mission-board panel is a layout reference only:

  ```text
  C:\Users\magni\Documents\BotsnCoding\Valheim\Households-REPO\OLDmoddedcatoheim\src\MissionSystemMod\MissionBoardPanel.cs
  ```

  Adapt its useful scroll-list, button, and TextMeshPro patterns to Jötunn's lifecycle. Do not copy its unmanaged permanent-canvas behavior without verifying scene cleanup.

## Asset and reference rules

- Follow the beginner asset workflow at:

  ```text
  C:\Users\magni\.codex\skills\valheim-custom-assets-beginner\SKILL.md
  ```

- For Blender, Unity, AssetBundle, BepInEx, Jötunn, or custom asset work, inspect this repository's instructions and relevant documentation before acting.
- Treat ripped Valheim assets as local reference material only. Do not copy or redistribute them unless the relevant permissions allow it.
- Do not invent Unity versions, shader names, AssetBundle paths, prefab names, or Jötunn API signatures. Verify them from the current project and assemblies.
- The asset pipeline is:

  ```text
  approved visual asset -> Unity prefab/material/icon -> AssetBundle when needed
  -> BepInEx plugin -> Jötunn named clone -> in-game test
  ```

- Keep RuneStorage identifiers unique. Do not copy plugin GUIDs, prefab IDs, bundle names, or deployment files from adjacent mods.

## Verification gates

Before calling a RuneStorage milestone complete, verify the relevant boundary:

- Clean Release build.
- Package contents and manifest dependencies.
- Plugin startup and Jötunn registration log.
- Terminal and core placement ghosts.
- Actual placement, hover, support, health, and interaction behavior.
- No-core and multiple-core failure states.
- Deposit and withdrawal capacity boundaries.
- Stack aggregation and non-stackable metadata preservation.
- Save/reload and plugin restart behavior.
- Interrupted-operation recovery behavior.
- Host/client or dedicated-server synchronization when the feature is multiplayer-facing.
- Deployed DLL and bundle hashes when deployment is requested.

## Current Gale test profile and deployment target

Use this exact Gale profile for all current RuneStorage Valheim in-game testing and plugin deployment:

```text
C:\Users\magni\AppData\Roaming\com.kesomannen.gale\valheim\profiles\VHouseholds-Admin
```

Deploy the plugin to its `BepInEx\plugins` directory. Do not create a backup copy during deployment unless the user specifically requests one.

## Working conventions

- Use `apply_patch` for text-file edits.
- Preserve unrelated user changes in a dirty worktree.
- Do not use destructive repository commands such as `git reset --hard` or `git checkout --` unless explicitly requested.
- Do not push or commit to a remote repository unless explicitly requested.
- Do not mark a plan checkbox complete merely because source code exists; require the evidence described by the checkbox.
