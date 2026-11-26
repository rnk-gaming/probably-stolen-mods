Off the Books: Shopkeeping Suite 0.7.2

WHAT IT DOES
A set of aids for running your shop. Most of them only show things: coloured marks on
items, price arrows, tooltip lines, a notice of changes in standing, a ledger of the
day's money and a window with four pages. Some give answers away: the appraisal value
and the counterfeit check. Three aids change what happens in your game and are listed
under WHAT IT CHANGES. Each aid has its own switch. The mod needs no other mod. It
reads Station Economy, Scrapyard Expansion and Module Forge when they are installed and
works without them.

HOW TO USE IT
Press F6 to open or close the window. You can change the key in Options. The window
has four pages: Today, Run, Customer and Standing. The Today page opens when the
shutter closes.
- Today: sales, purchases, other costs and the result of the day, with the stamp
  "DAY n CLOSED" after closing. When the standing tally is on, it also lists what each
  faction gained and lost today.
- Run: the totals since the ledger began.
- Customer: the person at the counter and the goods they want and can afford.
- Standing: every faction, with its standing at the start of the day, the change today
  and now. Scavengers (from Scrapyard Expansion) and DSI (from Module Forge) are
  included when those mods are installed. Bonuses and penalties gained or lost since
  the start of the day are listed below.
The window is drawn as a grey-green paper in the style of the lease agreement. Switch
that off for a plain window.
The ledger counts only what happens while it is on. It cannot recover earlier sales or
costs.

Marks appear in an item's top left corner, with a dark outline. They are for the shop.
- White dot: a container holding the same kind as the item you are dragging. Start a
  new drag to find the containers again.
- Red triangle: stolen goods offered by someone else.
- Purple triangle: contraband offered by someone else.
- Light red diamond: your goods that a security search would find, and containers
  holding them, while security is at the counter. Goods inside a Smuggler's Bay or a
  hidden container are not marked.
- Light green filled square: your goods that the buying customer wants and can afford.
- Light orange hollow square: wanted goods that are over the customer's budget.
- A container of yours that holds something the customer wants carries the same marks.
  Its tooltip says how many of the things in it the customer wants.
The game's own animal mark keeps its colour.

Price arrows appear in the top right corner of goods, also outside the shop: green up
means dearer than usual, red down means cheaper than usual. One arrow starts at a
change of 5%, two at 20%. Events of the game and of mods count, and Station Economy's
market counts when that mod runs. The tooltip gives the reason. Only event and market
changes are shown, not bargaining, condition or other price adjustments.
A supplier sign shows the price level of its kind of goods: water, food, material,
medical, household, both firearm signs and the ruined-machine sign. The level comes from
events and, with Station Economy and goods of that kind in the shop, from the market.
Berry and expedition signs get no arrow.

Tooltips can also show whether a buying customer wants your item, stolen goods with
heat, contraband with its level, and, during an appraisal, the correct value.
The counterfeit check names the issues of injectors, cigarette packs and food stamps in
tooltips, in the magnifier window and on the tablet. It needs the game's three check
panels to look as expected and is switched off otherwise.
Changes in standing show as a notice with the amount, and on the shop's tablet. The
notice also names bonuses and penalties crossed between readings.

SETTINGS
All are in Options, under Off the Books: Shopkeeping Suite, and in the Mods list. All
switches start on. Changes take effect at once, except the paper window, which takes
effect the next time the window is opened.
- Enable Shopkeeping Suite: switches all aids off together.
- Open Shopkeeping Suite: the key for the window. Default F6.
- Hands are emptied at home: the hands aid.
- Appraisal customers are rolled first: the certificate aid.
- Fill in the correct appraisal value: writes the answer into the appraisal panel.
- Show the correct appraisal value: shows the answer in the tooltip.
- Show what the counterfeit check would find: the counterfeit check.
- Mark containers holding the same kind: the white dot.
- Mark risky goods on offer: the red and purple triangles.
- Mark goods an inspection would find: the light red diamond.
- Mark goods the customer wants: the green and orange squares.
- Mark containers that hold what the customer wants: the same marks on containers.
  It needs "Mark goods the customer wants".
- Mark size: 1 small, 2 normal, 3 large.
- Mark goods that are dearer or cheaper than usual: the price arrows.
- Say in the tooltip why: the price reason in the tooltip.
- Price mark from (%): the change that gives one arrow. Default 5.
- Double mark from (%): the change that gives two arrows. Default 20. A value below the
  first counts as equal to it.
- Show wanted goods in tooltips: whether a buying customer wants your item and can afford it.
- Show stolen goods and contraband in tooltips: heat and contraband level on someone
  else's goods.
- Show changes in standing: the notice.
- Show changes in standing on the tablet: the same gains and losses on the shop's tablet.
- Keep the day's gains and losses of standing: the tally on Today and Standing. Switching
  it off forgets the day's start; switching it on takes the standing at that moment as
  the start.
- Keep the ledger: counts sales and costs, as described above.
- Enable the shop window: the window with four pages.
- Show the window as a paper: the paper look. Off gives the plain window.
- Open today's ledger when the shutter closes: opens the Today page.

WHAT IT CHANGES
- Fill in the correct appraisal value writes the value onto the item, once per item.
  You can still change it in the panel.
- Hands are emptied at home moves items into your storage. After an outing the game can
  leave an item, or all but one unit of a stack, in a hand slot at home, out of reach.
  After the item has been there for a second, the aid places it in the back room, the
  shop or the showcase as room allows and says so. When there is no room it warns once
  and tries again every few seconds. An item in the outing backpack slot is only
  reported: take it out on your next outing. Some mods refuse to save while a hand holds
  an item.
- Appraisal customers are rolled first moves the appraiser's certificate customer to
  the front of the game's roll. The game rolls for showcase customers in a fixed order
  and stops when the day is full, so several signs can keep the certificate from being
  reached. On days an appraisal customer comes, the signs behind it share one customer
  less.
The other aids change no prices, cash, items, customers or outcomes. The ledger adds
its own notes to the mod's save data. Items brought in from your hands are saved in
their new places by the game.

BEFORE YOU REMOVE THIS MOD
Nothing in a save needs this mod. Switch it off in the Mods menu, restart and remove its
folder.

WHEN SOMETHING SEEMS WRONG
Player.log has a line starting [Shopkeeping Suite] loaded. It lists each aid as on, off
or unavailable. Faults appear as lines starting with [Shopkeeping Suite]. A fault in
the price aid switches it off for the session; a fault in the Station Economy reading
switches off only the market part.
