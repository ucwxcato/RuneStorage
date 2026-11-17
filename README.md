# RuneStorage

RuneStorage adds a buildable runestone terminal that gives you a searchable view of a nearby storage core. The core keeps the items in its own Valheim chest inventory. The terminal lets you deposit, withdraw, craft from stored items, and upgrade the core.

![RuneStorage Terminal in game](DOCS/images/runestorage-terminal.png)

Version 0.1.0 is the first public release.

## What it does

- **RuneStorage Terminal:** the interaction point. Open it to see stored items, deposit yours, and craft.
- **RuneStorage Core:** the storage. It holds the items and can be upgraded in place to more slots.
- **Search and sort:** the terminal groups matching stacks and keeps items with different quality, durability, or crafter separate.
- **Crafting from storage:** recipes use items stored in the Core. The required crafting station must be nearby.
- **Chest-style access:** anyone can use the Core and terminal unless a ward or private area blocks them.

## Pieces and costs

| Piece | Where to build | Cost |
|---|---|---|
| RuneStorage Terminal | Hammer | 10 Fine Wood, 10 Stone, 2 Surtling Cores |
| RuneStorage Core (Tier I, 256 slots) | Hammer, needs a Forge | 10 Bronze, 10 Fine Wood, 4 Surtling Cores |

Upgrades are done from the terminal's Upgrade tab, and the Core is upgraded in place. Each upgrade keeps everything already stored.

| Tier | Slots | Station | Cost |
|---|---|---|---|
| I | 256 | Forge | Built with the Core |
| II | 512 | Forge, nearby | 10 Iron, 10 Fine Wood, 4 Surtling Cores |
| III | 1,024 | Blackforge, nearby | 10 Black Metal, 10 Fine Wood, 6 Surtling Cores |

You can pay for an upgrade from the Core's stored items, or from your own inventory with the "Pay from" toggle. Equipped items are never used.

## How to use it

1. Place a terminal and a Core within 12 meters of each other. Use one Core per terminal. If two Cores are in range, the terminal refuses to choose and tells you why.
2. Interact with the terminal.
3. **Stored tab:** take items out with Take 1, Take 5, or Take X. Sort by name or amount.
4. **Deposit tab:** put items in with Deposit 1, Deposit 5, Deposit X, or Deposit all for a whole item group. Deposit Everything moves all your unequipped items.
5. **Craft tab:** craft recipes with items stored in the Core. Each click crafts one recipe.
6. **Upgrade tab:** upgrade the Core.

Deposit Everything and "Deposit all" store as much as fits and leave the rest in your inventory. Deposit 1, 5, and X are all-or-nothing: if the whole amount doesn't fit, nothing moves. Withdrawals are all-or-nothing too.

Hammer removal is blocked while a Core still holds items. Empty it first.

## Known limits

- **Disconnects:** RuneStorage has no recovery journal. If you disconnect in the middle of a transfer to a remote Core, items can be duplicated or lost. Vanilla chests have the same kind of risk.
- **Remote trust:** the Core owner trusts the item data a remote player sends for deposits and player-paid upgrades.
- **Destroyed Core:** the Core's items are not lost when it is destroyed. The terminal holds no items.
- **Multiplayer:** not yet tested.

## Install

Install through Thunderstore or r2modman once published. The package needs BepInExPack_Valheim and Jötunn 2.30.2.

The Thunderstore package page is the same as the package README: [packaging/thunderstore/README.md](packaging/thunderstore/README.md).

Thunderstore: https://thunderstore.io/c/valheim/p/catosaurluna/RuneStorage/ (live after the first upload).

## Build from source

Requirements:
- .NET 8 SDK, to build with `dotnet build` and to run the model tests.
- A Valheim install. The build reads Valheim and Unity assemblies from the Steam path by default. Override it with `-p:ValheimDir="C:\path\to\Valheim"`.

BepInEx 5.4.23.6, 0Harmony, and Jötunn 2.30.2 are vendored under `lib/BepInEx`. See `lib/BepInEx/VENDORED.txt`.

```
dotnet build src/RuneStorage/RuneStorage.csproj -c Release
dotnet run --project tests/RuneStorage.ModelTests -c Release
```

If the RuneStorage Test r2modman profile exists, the post-build step copies the Release DLL into its `BepInEx\plugins` folder.

## Documentation

- `DOCS/RUNESTORAGE_DEVPLAN.md`: the development plan, design decisions, and verification status.

## Credits

- [BepInEx](https://github.com/BepInEx/BepInEx), LGPL-3.0.
- [Harmony](https://github.com/pardeike/Harmony), MIT.
- [Jötunn](https://github.com/Valheim-Modding/Jotunn), MIT.

Valheim and its assets are owned by Iron Gate Studio and are not included in this repository.

## License

To be decided.
