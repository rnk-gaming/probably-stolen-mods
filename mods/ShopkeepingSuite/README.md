# Off the Books: Shopkeeping Suite

Aids for running the shop: marks on items, tooltip lines, a ledger of the day's money and a window with four pages.

| | |
|---|---|
| Mod id | `rnk.shopkeeping-suite` |
| Version | 0.7.2 |
| Needs | Nothing. Reads Station Economy, Scrapyard Expansion and Module Forge when they run. |

The player's guide is [dist/README.txt](dist/README.txt): what the mod does, its settings, what it
changes in a save and how to remove it.

## Install

Subscribe on the Steam Workshop, or copy the contents of `dist/` into a folder of its own under the
game's `Mods/` folder. Approve the mod and switch it on in the game's Mods menu, then restart the game.

## Build

```bash
dotnet build mods/ShopkeepingSuite -c Release
```

The DLL lands in `bin/Release/netstandard2.1/`. The folder `dist/` holds the released build.
