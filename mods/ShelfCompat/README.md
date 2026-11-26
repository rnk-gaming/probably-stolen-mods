# Off the Books: Water Expanded Compatibility

Lets Water Expanded's devices work on Shop Expansion's shelves and in the back room.

| | |
|---|---|
| Mod id | `rnk.shelf-compat` |
| Version | 1.1.4 |
| Needs | Water Expanded; Shop Expansion for the shelves. |

The player's guide is [dist/README.txt](dist/README.txt): what the mod does, its settings, what it
changes in a save and how to remove it.

## Install

Subscribe on the Steam Workshop, or copy the contents of `dist/` into a folder of its own under the
game's `Mods/` folder. Approve the mod and switch it on in the game's Mods menu, then restart the game.

## Build

```bash
dotnet build mods/ShelfCompat -c Release
```

The DLL lands in `bin/Release/netstandard2.1/`. The folder `dist/` holds the released build.
