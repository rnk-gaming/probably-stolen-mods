Off the Books: Station Economy Compatibility 2.6.2
RnK Gaming

WHAT IT DOES
Corrects several faults in Station Economy, in memory. It changes none of Station
Economy's files, and it does nothing when Station Economy is not running.
Station Economy is required.
It fixes six things:
1. On a screen wider than 16:9, the phone (F7, or a double-click on the Phone item)
   lands in the black area beside the game's picture where you cannot see it. The mod
   keeps it inside the picture when it first appears. You can drag it anywhere
   after that, also onto the black sides. On a 16:9 or narrower screen this changes
   nothing.
2. Phone texts that are too long for their place are cut off with dots. The mod makes
   such a text smaller first and cuts it only if it still does not fit.
3. English that Station Economy leaves in Chinese, and English lost when your
   shopkeeper has a name of your own, is filled in.
4. A long name or message preview is cut before it is translated, so the cut piece
   stays half Chinese. The mod translates it first and then cuts the English. A
   contact's avatar shows the first letter of the translated name.
5. Station Economy stops hoppers from filling a Furnace and feeds the Furnace itself.
   The mod lets hoppers fill it like any other inventory, and corrects the feeding.
6. Some of Station Economy's visitors who sell off goods offer the whole lot at a
   discount, such as "Half price", but the lot costs more than the items one by one.
   The mod makes the lot cost less, as the seller says. This changes prices.

HOW TO USE IT
Nothing to do. Each part works by itself when its setting is on. A change of a
setting shows on texts made after it: close the phone and open it again. Names
are refreshed within a second.

SETTINGS
They are in the Mods list and in Options, under "Off the Books: Station Economy
Compatibility". All are on unless the text says otherwise.
Keep the phone inside the game's picture: the first place of the phone is inside
the 16:9 picture.
Show the phone over the black sides: the phone is drawn over the black sides, so it
stays visible when you drag it there.
Shrink phone texts that do not fit: a text that is too long is made smaller before it
is cut. A text that fits is left as it is.
Smallest text size (% of its own): how small a phone text may become before it is cut,
from 50 to 100. The default is 65. 100 switches the shrinking off.
Add missing English to Station Economy: adds English lines from a table to Station
Economy's translator. The table is station_economy_additions.csv in this mod's
Localization folder. A line of ours replaces Station Economy's own line for the same
text. Switching it off, or disabling the mod, restores the original table for the
next translation.
Keep Station Economy's English with your own shopkeeper name: keeps the full English
lines when you have chosen a name of your own or someone uses another form of address.
A Chinese name is kept as written, a Latin name is written given name first, and a
default part uses its English form. A family name shared with another character can
show your name in a line about them. It acts only when the game is not in Chinese.
Cut long lines after translating them: message previews are translated before they
are cut. It acts only when the game is not in Chinese.
Latin initial for a translated name: a contact's avatar shows the first letter or
digit of the translated name. Chinese names without a translation are left as they are.
Hoppers fill the Furnace as in the base game: a hopper fills the Furnace's input with
what the filter lets through, above the intake's retained stock, as much as there is
room for. The pieces arrive separately and do not stack in the input. It takes effect
at the next hopper transfer.
Feed the Furnace in full through a hopper: when Station Economy pulls feed for the
Furnace itself (it does when the input holds fewer than three pieces), every piece it
takes is counted, and what a run did not use goes back. It takes effect at the next pull.
Wholesale offers of Station Economy's sellers are a discount: the whole lot costs less
than the pieces, as the seller says. It applies to the next visitor.

WHAT IT CHANGES
In a save: nothing. In the economy: the whole-lot price of those sellers, when the
last setting is on. In Station Economy: its phone position and texts, its English,
and how its Furnace is fed, in memory only. Each part checks the shape of the code
it corrects and stays off if Station Economy has changed.

REMOVING IT
Switch the mod off or delete its folder. Station Economy then behaves as it did
without this mod. The mod writes nothing to a save.

PLAYER.LOG
Every line starts with [Station Economy Compat]. At start each part that connects
writes one line, for example "keeping Station Economy's phone inside the game's
picture". A part whose target in Station Economy has changed says so and is left
off. A fault is reported once and the original behaviour continues.

FOR STATION ECONOMY'S AUTHOR
You are welcome to take these English lines into Station Economy. The file
station-economy-english-additions.txt in this folder holds them in the form of your
own table: one line per text, the Chinese, a tab, the English, with {0}, {1} where a
value goes in and \n for a line break. It can be appended to your table as it is.
Some lines are your own, in the other forms your screens look them up in (with a
leading dot, or without their tags); the rest are new. Where a line of ours has the
same Chinese as one of yours, ours is the later one and wins.
