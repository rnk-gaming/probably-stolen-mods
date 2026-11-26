# Off the Books: Agri Stock Odds

Agri Expansion's machines appear in vendor stock by chance instead of on every visit.

| | |
|---|---|
| Mod id | `rnk.agri-stock-odds` |
| Version | 2.0.1 |
| Needs | Agri Expansion. |

The player's guide is [dist/README.txt](dist/README.txt): what the mod does, its settings, what it
changes in a save and how to remove it.

## Install

Subscribe on the Steam Workshop, or copy the contents of `dist/` into a folder of its own under the
game's `Mods/` folder. Approve the mod and switch it on in the game's Mods menu, then restart the game.

## Build

```bash
dotnet build mods/AgriStockOdds -c Release
```

The DLL lands in `bin/Release/netstandard2.1/`. The folder `dist/` holds the released build.
