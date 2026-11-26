# Off the Books: Machine Upgrades

Upgrade eight machines by dropping the game's own materials on them.

| | |
|---|---|
| Mod id | `rnk.machine-upgrades` |
| Version | 3.1.2 |
| Needs | Nothing. Skips Agri Expansion's Integrated Farm when that mod runs. |

The player's guide is [dist/README.txt](dist/README.txt): what the mod does, its settings, what it
changes in a save and how to remove it.

## Install

Subscribe on the Steam Workshop, or copy the contents of `dist/` into a folder of its own under the
game's `Mods/` folder. Approve the mod and switch it on in the game's Mods menu, then restart the game.

## Build

```bash
dotnet build mods/MachineUpgrades -c Release
```

The DLL lands in `bin/Release/netstandard2.1/`. The folder `dist/` holds the released build.
