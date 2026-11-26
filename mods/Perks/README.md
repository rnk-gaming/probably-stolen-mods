# Off the Books: Perks

29 starting perks for the new-game screen, with drawbacks that give points back.

| | |
|---|---|
| Mod id | `rnk.perks` |
| Version | 0.2.2 |
| Needs | Nothing. |

The player's guide is [dist/README.txt](dist/README.txt): what the mod does, its settings, what it
changes in a save and how to remove it.

## Install

Subscribe on the Steam Workshop, or copy the contents of `dist/` into a folder of its own under the
game's `Mods/` folder. Approve the mod and switch it on in the game's Mods menu, then restart the game.

## Build

```bash
dotnet build mods/Perks -c Release
```

The DLL lands in `bin/Release/netstandard2.1/`. The folder `dist/` holds the released build.
