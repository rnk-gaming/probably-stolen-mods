Off the Books: Scavenging Pool 2.3.0
RnK Gaming

WHAT IT DOES
Lets you use one trip of a night for scavenging and keep the other trips for
trading.
The game gives fresh scavenging attempts every time you arrive at the dumping
ground. This mod puts the attempts of all the trips counted into one pool for the
night. You can use them in one visit. Leaving and returning the same night does not
give fresh attempts. The next night starts a new pool.
With five attempts per trip and three trips counted, you get 15 attempts. Bonuses
from perks, events and mods count too: seven attempts per trip and three trips give 21.
It does not give you more trips and does not spend the other trips. It does not
remove wound risk. It changes no file of the game or of another mod.

WHAT IT NEEDS
Nothing. If Transit Quota is running, the mod counts that mod's trips. Otherwise it
counts the game's own outings.

HOW TO USE IT
Switch the mod on and restart the game. Scavenge as usual. With three trips counted
and five attempts per trip, you should have 15 attempts. Use some, leave and return
the same night: the attempts already used still count. The next night starts fresh.

SETTINGS
They are in Options under "Off the Books: Scavenging Pool", and in the Mods list
under "Scavenging pool".
One pool of attempts per night: on. Off gives the game's fresh set of attempts on every
arrival.
Count the night's trips into the pool: on. Off makes the pool one trip's attempts, with
your perks and events. It is still kept for the night, so leaving and coming back
does not give it again.
Trips counted into the pool (0 = automatic): 0, from 0 to 10. Automatic counts tonight's
trips, when you leave the shop, up to "Most trips counted into the pool". A number
from 1 to 10 counts that many trips' worth of attempts, whatever outings you have, and
is not capped.
Most trips counted into the pool: 4, from 1 to 10. Automatic counting never goes above
this. It takes effect at the next search or update of the attempts.
Count Transit Quota's trips instead of the game's counter: on. With Transit Quota
running, its quota for the night is counted. One-time cards you carry are not part of
it, so five cards in your pack no longer make nine trips. Off, or without Transit
Quota, the game's own outings are counted. It takes effect at the next night's count.
If Transit Quota cannot be read, the game's counter is used.
Wound chance starts over for each trip's worth: on. Wound chances rise within each trip's
worth of attempts, then start over, as they did on separate visits. Off lets them keep
rising through the whole pool.

WHAT IT CHANGES
In the economy: you have more finds in one night, from the larger pool of attempts.
In a save: it keeps its counters for the night in the save's mod data.

REMOVING IT
Switch the mod off or delete its folder. The numbers it keeps in the save are then
ignored.

PLAYER.LOG
Lines start with [Scavenging Pool]. Once a night one line says how many trips are
counted: "[Scavenging Pool] trips tonight: ...", with the source of the number and
the cap. Other lines are warnings, for example when Transit Quota or a game hook has
a shape the mod cannot use. A fault is reported once and the game's own rule is used.

TRANSLATIONS
Translations are very welcome: every text is in Localization/en.csv. Send a translated
copy on GitHub: https://github.com/rnk-gaming/probably-stolen-mods/issues
