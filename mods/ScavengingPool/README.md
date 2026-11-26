# Off the Books: Scavenging Pool

All of a night's scavenging attempts in one pool, for one visit to the dumping ground.

| | |
|---|---|
| Mod id | `rnk.scavenging-pool` |
| Version | 2.1.2 |
| Needs | Nothing. Counts Transit Quota's trips when that mod runs. |

The player's guide is [dist/README.txt](dist/README.txt): what the mod does, its settings, what it
changes in a save and how to remove it.

## Install

Subscribe on the Steam Workshop, or copy the contents of `dist/` into a folder of its own under the
game's `Mods/` folder. Approve the mod and switch it on in the game's Mods menu, then restart the game.

## Build

```bash
dotnet build mods/ScavengingPool -c Release
```

The DLL lands in `bin/Release/netstandard2.1/`. The folder `dist/` holds the released build.
