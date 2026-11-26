# Off the Books: Stacking

Stack materials, medical items and key cards, with a limit for each kind of goods.

| | |
|---|---|
| Mod id | `rnk.stacking` |
| Version | 1.9.2 |
| Needs | Nothing. Works with Station Economy and Faucet Filter when they run. |

The player's guide is [dist/README.txt](dist/README.txt): what the mod does, its settings, what it
changes in a save and how to remove it.

## Install

Subscribe on the Steam Workshop, or copy the contents of `dist/` into a folder of its own under the
game's `Mods/` folder. Approve the mod and switch it on in the game's Mods menu, then restart the game.

## Build

```bash
dotnet build mods/Stacking -c Release
```

The DLL lands in `bin/Release/netstandard2.1/`. The folder `dist/` holds the released build.
