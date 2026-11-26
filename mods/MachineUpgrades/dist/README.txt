Off the Books: Machine Upgrades 3.1.2

WHAT IT DOES
You upgrade eight machines by dragging materials onto the closed machine item, as you
upgrade the game's Fridge. Each machine has tracks for storage, efficiency, performance
and quality, and two machines have a track of their own. Only the game's own materials
are used. The mod needs no other mod.

HOW TO USE IT
Drag a material onto the machine. The machine's tooltip shows the levels under
"Upgrade:". With a mod that stacks the materials, a stack dropped on a machine takes
every step it covers, one after the other, and the rest of the stack stays where it
was. Switch this off with "A dropped stack upgrades as far as it reaches" to get one
step per drop.

THE UPGRADES
The same material gives the same effect where possible:
- Metal Ingot: Storage, +1 row per ingot, up to 12 rows. On machines with power, each
  ingot also raises the nightly energy use by 1.
- Electronic Parts (Common): Efficiency, +5% per step, 40 steps (200%).
- Wire: Performance.
Efficiency does not push a machine's energy use below 1. Low Power Mode can still set
it to 0.

Furnace
- Storage (Metal Ingot), 6 steps: Input and Output 9x6 -> 9x12.
- Efficiency (Electronic Parts (Common)), 40 x 5%.
- Quality (Advanced Flux Agent), 20 x 10%.
- Batches (an empty Furnace), 5 steps: +1 batch per night each. A Turbo Booster runs
  all batches.

3D Printer
- Storage (Metal Ingot), 7 steps: Input 9x6 -> 9x12, Output 9x5 -> 9x12.
- Efficiency, 40 x 5%. Performance (Wire), 20 x 5%.
- Quality (Gun Parts), 25 x 4%.

Hydroponic System (also the Old Hydroponic System)
- Storage (Metal Ingot), 6 steps: Produce Output 9x6 -> 9x12.
- Efficiency, 40 x 5%. Performance (Wire), 20 x 10%.
- Quality (Chemical Supplies (Common)), 40 x 5%.
- Performance and Quality count half on this machine.
- Upgrades have no effect inside Agri Expansion's Integrated Farm, which sets its
  plots' stats itself, and cannot be added there.

Moisture Farm
- Efficiency, 40 x 5%. Performance (Wire), 20 x 5%.
- Quality (Water Filter), 150 x 1%: Pure Water at 150%.

Water Purifier
- Efficiency, 40 x 5%. Performance (Wire), 20 x 5%.
- Quality (Water Filter), 200 x 1%: 100% pure water at 200%.

Evaporator
- Efficiency, 40 x 5%. Performance (Wire), 20 x 5%.

Cryptographic Desequencer
- Efficiency (PowerBloc), 8 x 25%.
- Performance (Module Extractor (Basic)), 8 x 25%.
- Output (an empty Cryptographic Desequencer), 3 steps: +1 copy of each finished card
  per step (x4 at most). The first card goes to the output slot; the extra copies go
  to the shop's storage.

Chem Finisher
- Storage (Metal Ingot), 8 steps: Output 6x4 -> 6x12.

A Furnace or Desequencer used as an upgrade must be empty (an instruction note is fine)
and have no Module Bay Expansion Kit. Its own upgrades from this mod are lost, and its
value moves to the upgraded machine.

WHAT IT CHANGES IN A SAVE AND IN THE ECONOMY
- Upgrade levels are stored on the machine in your save.
- Each upgrade adds the material's value to the machine, like the game's Fridge.
- The Tinkerer perk's 20% chance to keep the material applies.
- Upgrades change what the machines make and how much energy they use.

IF YOU USE OTHER MODS
- Agri Expansion: the Furnace's extra batches also apply to its Ironfruit smelting.

SETTINGS
The only setting is "A dropped stack upgrades as far as it reaches". It is in the Mods
list and in Options, under Off the Books: Machine Upgrades.

BEFORE YOU REMOVE THIS MOD
Empty upgraded machines first. Without the mod the grids return to their usual size,
and items in the extra rows can be lost.
A machine keeps the higher energy use of its Storage upgrade (one per level) after
the mod is removed.

WHEN SOMETHING SEEMS WRONG
Player.log shows a line starting with [Machine Upgrades] when the mod meets a fault.
