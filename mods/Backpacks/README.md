# Off the Books: Backpacks

Upgrade backpacks with materials: size, compartments, side bags and a toolbox slot. Adds two backpacks and two starting perks.

| | |
|---|---|
| Mod id | `rnk.backpacks` |
| Version | 1.6.0 |
| Needs | Nothing. |

The player's guide is [dist/README.txt](dist/README.txt): what the mod does, its settings, what it
changes in a save and how to remove it.

## Install

Subscribe on the Steam Workshop, or copy the contents of `dist/` into a folder of its own under the
game's `Mods/` folder. Approve the mod and switch it on in the game's Mods menu, then restart the game.

## Build

```bash
dotnet build mods/Backpacks -c Release
```

The DLL lands in `bin/Release/netstandard2.1/`. The folder `dist/` holds the released build.
