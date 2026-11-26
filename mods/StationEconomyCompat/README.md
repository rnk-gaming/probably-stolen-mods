# Off the Books: Station Economy Compatibility

Corrects Station Economy's phone on wide screens, its texts, its English, its Furnace feed and its whole-lot offers, in memory.

| | |
|---|---|
| Mod id | `rnk.station-economy-compat` |
| Version | 2.6.2 |
| Needs | Station Economy. |

The player's guide is [dist/README.txt](dist/README.txt): what the mod does, its settings, what it
changes in a save and how to remove it.

## Install

Subscribe on the Steam Workshop, or copy the contents of `dist/` into a folder of its own under the
game's `Mods/` folder. Approve the mod and switch it on in the game's Mods menu, then restart the game.

## Build

```bash
dotnet build mods/StationEconomyCompat -c Release
```

The DLL lands in `bin/Release/netstandard2.1/`. The folder `dist/` holds the released build.
