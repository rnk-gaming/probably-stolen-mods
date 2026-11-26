# Mods for Probably Stolen

By RnK Gaming. Each mod works on its own unless its row says what it needs.

## Off the Books

| Mod | Version | What it does | Needs |
|---|---|---|---|
| [Off the Books: Backpacks](mods/Backpacks/README.md) | 1.6.0 | Upgrade backpacks with materials: size, compartments, side bags and a toolbox slot. Adds two backpacks and two starting perks. | Nothing. |
| [Off the Books: Storage Upgrades](mods/StorageUpgrades/README.md) | 1.3.4 | Enlarge storage bays, loot crates, Beer Cases and machine storage with materials. | Nothing. |
| [Off the Books: Machine Upgrades](mods/MachineUpgrades/README.md) | 3.1.2 | Upgrade eight machines by dropping the game's own materials on them. | Nothing. Skips Agri Expansion's Integrated Farm when that mod runs. |
| [Off the Books: Stacking](mods/Stacking/README.md) | 1.9.2 | Stack materials, medical items and key cards, with a limit for each kind of goods. | Nothing. Works with Station Economy and Faucet Filter when they run. |
| [Off the Books: Shopkeeping Suite](mods/ShopkeepingSuite/README.md) | 0.7.2 | Aids for running the shop: marks on items, tooltip lines, a ledger of the day's money and a window with four pages. | Nothing. Reads Station Economy, Scrapyard Expansion and Module Forge when they run. |
| [Off the Books: Perks](mods/Perks/README.md) | 0.2.2 | 29 starting perks for the new-game screen, with drawbacks that give points back. | Nothing. |
| [Off the Books: Scavenging Pool](mods/ScavengingPool/README.md) | 2.1.2 | All of a night's scavenging attempts in one pool, for one visit to the dumping ground. | Nothing. Counts Transit Quota's trips when that mod runs. |
| [Off the Books: Station Economy Compatibility](mods/StationEconomyCompat/README.md) | 2.6.2 | Corrects Station Economy's phone on wide screens, its texts, its English, its Furnace feed and its whole-lot offers, in memory. | Station Economy. |
| [Off the Books: Water Expanded Compatibility](mods/ShelfCompat/README.md) | 1.1.4 | Lets Water Expanded's devices work on Shop Expansion's shelves and in the back room. | Water Expanded; Shop Expansion for the shelves. |
| [Off the Books: Agri Stock Odds](mods/AgriStockOdds/README.md) | 2.0.1 | Agri Expansion's machines appear in vendor stock by chance instead of on every visit. | Agri Expansion. |

## Build

You need the .NET SDK and the game installed. The projects read the game's assemblies from its
`Probably Stolen_Data/Managed` folder; set the environment variable `GAME_MANAGED` to that folder
if the game is not in Steam's default place.

```bash
dotnet build mods/Stacking -c Release
```

Code shared between the mods is in `mods/Shared/`.

## Licence

MIT, see [LICENSE](LICENSE).
