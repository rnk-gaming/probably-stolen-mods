# Off the Books: Storage Upgrades

![Off the Books: Storage Upgrades](../../pictures/off-the-books-storage-upgrades-976x544.png)

More room in the things you already own. Drop a Metal Ingot on a storage bay and it gets a column or a row.

| | |
|---|---|
| Mod id | `rnk.storage-upgrades` |
| Version | 1.3.4 |
| Needs | Nothing. |

## What you get

- **Bays:** Smuggler's Bay, Machine Bay, Storage Bay and the Gunsmith and Chemist ones, each up to its own limit. A Smuggler's Bay (Modified) reaches 16x16 and a Storage Bay (Extended) 20x20.
- **Loot crates:** drop an empty crate on another of the same kind. A 7x5 crate ends at 19x11, six crates in one.
- **Beer Cases:** the same trick. Three wide first (24x4), and a second row (24x8) with the sixth case.
- **Machine storage:** the Fridge, Wine Rack, Aquaculture and Rat Cage take ingots once the game's own upgrades are done. The Fridge reaches 9x13, the Aquaculture 24x16. The Water Tank goes from 12 to 48.
- **Trade-in:** hand an enlarged Smuggler's Bay to the janitor and the modified one keeps what you added.

## Options

In the Mods list and in Options, under **Off the Books: Storage Upgrades**. You can cap how far each bay grows and how many crates one crate may swallow. Two switches that are off from the start let bays hold a Machine Bay.

## Good to know

- Sizes are saved on the containers. "Before removing this mod: list what to move" in Options shows what has to come out before you remove the mod.
- With a stacking mod, a whole stack of ingots upgrades as far as it reaches.
- Works on its own.

The full player's guide is [dist/README.txt](dist/README.txt).

## Install

Subscribe on the Steam Workshop, or copy the contents of `dist/` into a folder of its own under the
game's `Mods/` folder. Approve the mod and switch it on in the game's Mods menu, then restart the game.

## Build

```bash
dotnet build mods/StorageUpgrades -c Release
```

The DLL lands in `bin/Release/netstandard2.1/`. The folder `dist/` holds the released build.

## Problems and ideas

Open an [issue](https://github.com/rnk-gaming/probably-stolen-mods/issues). That is where I keep track of things.
