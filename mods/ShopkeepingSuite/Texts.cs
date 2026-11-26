using System;
using System.Collections.Generic;
using System.Globalization;

namespace ShopkeepingSuite
{
    internal static class Texts
    {
        internal static Func<string, string> Localise = key => ModHelper.GetLocalized(SuiteMod.Id, key);
        internal static readonly Dictionary<string, string> English = new Dictionary<string, string>
        {
            ["title"] = "Off the Books: Shopkeeping Suite",
            ["hands_home_label"] = "Hands are emptied at home",
            ["hands_home_description"] = "After an outing the game leaves an item in your hand when there is no room for it at home, and all but one unit of a stack. You cannot reach it there until the next outing. With this on, such an item is brought into the back room, the shop or the showcase as soon as there is room, and a notice says so.",
            ["hands_brought"] = "Brought in from your hand: {0}",
            ["hands_item"] = "an item",
            ["hands_full"] = "{0} is still in your hand: there is no room at home. Make room in the back room or the shop and it is brought in.",
            ["hands_backpack"] = "{0} is still in the outing backpack slot. Take it out on your next outing.",
            ["certificate_first_label"] = "Appraisal customers are rolled first",
            ["certificate_first_description"] = "The game rolls for the showcase's customers in a fixed order and stops when the day is full, so with several signs out the appraiser's certificate is seldom reached. With this on, the certificate's customer is rolled first. The signs behind it then share one customer less on the days an appraisal customer comes. A change takes effect the next time the list is made, at the latest the next morning.",
            ["price_now"] = "Price now: {0}", ["price_market"] = "market", ["price_event"] = "an event", ["price_more"] = "+{0} more",
            ["price_sign_now"] = "{0} prices now: {1}", ["price_sign_usual"] = "as usual",
            ["price_sign_market"] = "market {0} over {1} goods in the shop",
            ["price_sign_empty"] = "no goods of this kind in the shop to read the market from",
            ["price_sign_unknown"] = "no valid market readings for this kind in the shop",
            ["price_type_water"] = "Water", ["price_type_food"] = "Food", ["price_type_material"] = "Material",
            ["price_type_medical"] = "Medical", ["price_type_household"] = "Household goods",
            ["price_type_firearm"] = "Firearm", ["price_type_machine"] = "Machine",
            ["mark_price_label"] = "Mark goods that are dearer or cheaper than usual",
            ["mark_price_description"] = "Green up arrows for dearer goods, red down arrows for cheaper goods, including your goods and offers at the counter, even after closing. A supplier sign summarises that kind's price level now, from events and, with Station Economy, goods of that kind in the shop. Events of the game and of mods count; Station Economy's market counts when that mod runs. Changes take effect now.",
            ["tooltip_price_label"] = "Say in the tooltip why",
            ["tooltip_price_description"] = "Shows the price change from events of the game and of mods, with Station Economy's market when that mod runs. A supplier sign names its kind of goods and the number of shop goods used for its market average. Changes take effect now.",
            ["price_margin_label"] = "Price mark from (%)",
            ["price_margin_description"] = "One arrow from this whole percentage change, up or down. Events of the game and of mods count; Station Economy's market counts when that mod runs. Default: 5. Changes take effect now.",
            ["price_margin_strong_label"] = "Double mark from (%)",
            ["price_margin_strong_description"] = "Two arrows from this whole percentage change, up or down. A value below the first margin counts as equal to it. Events of the game and of mods count; Station Economy's market counts when that mod runs. Default: 20. Changes take effect now.",
            ["other_services"] = "Other services ({0})",
            ["section_sales"] = "Sales", ["section_purchases"] = "Purchases", ["section_costs"] = "Costs",
            ["section_other_costs"] = "Other costs", ["section_result"] = "Result",
            ["daily_gained"] = "Gained", ["daily_lost"] = "Lost", ["daily_change"] = "Change", ["no_change_today"] = "No change today",
            ["and_more"] = "... and {0} more",
            ["day_closed"] = "DAY {0} CLOSED",
            ["reopen_hint"] = "{0} opens this again",
            ["reputation_change"] = "{0} {1}",
            ["money_in_label"] = "Money in", ["money_out_label"] = "Money out", ["net_label"] = "Net",
            ["days_label"] = "Days recorded", ["day_label"] = "Day {0}", ["service_name"] = "Service ({0})",
            ["name_label"] = "Name", ["faction_label"] = "Faction", ["intent_label"] = "Intent",
            ["budget_label"] = "Budget left", ["wanted_label"] = "Wanted items", ["affordable_label"] = "Within budget",
            ["standing_today"] = "Standing today",
            ["standing_start"] = "Start", ["standing_now"] = "Now",
            ["standing_gained"] = "Gained: {0}", ["standing_lost"] = "Lost: {0}",
            ["standing_effect"] = "Now in effect: {0}", ["standing_no_effect"] = "No longer in effect: {0}",
            ["standing_bonus"] = "the bonus at {0}", ["standing_step"] = "the step at {0}", ["standing_level"] = "level {0}",
            ["paper_window_label"] = "Show the window as a paper",
            ["paper_window_description"] = "Shows the Suite's window on a paper in the style of the lease agreement. Off: the plain window. Changes take effect the next time the window is opened.",
            ["standing_tally_label"] = "Keep the day's gains and losses of standing",
            ["standing_tally_description"] = "Adds what was gained and lost with each faction today to the Standing and Today pages. Changes take effect now.",
            ["reputation_feed_label"] = "Show changes in standing",
            ["tablet_feed_label"] = "Show changes in standing on the tablet",
            ["tablet_feed_description"] = "Shows each gain and loss of standing with a faction on the shop's tablet, with bonuses and penalties gained or lost. Changes take effect now.",
            ["reputation_feed_description"] = "Shows each gain and loss of standing with a faction as it happens, with the amount. Changes take effect now.",
            ["appraisal_fill_label"] = "Fill in the correct appraisal value",
            ["appraisal_fill_description"] = "Writes the correct value into the appraisal panel when you put the item under the magnifying glass. You can still change it. This gives the answer. Changes take effect now.",
            ["counterfeit_check_label"] = "Show what the counterfeit check would find",
            ["counterfeit_check_description"] = "Names the issues of an injector, a cigarette pack or a food stamp in its tooltip, the magnifier window and the tablet. This gives the answer. Changes take effect now.",
            ["counterfeit_faults"] = "Faults", ["counterfeit_no_faults"] = "No faults",
            ["counterfeit_line"] = "Counterfeit check: {0}", ["counterfeit_sound"] = "nothing wrong",
            ["counterfeit_indicator"] = "Indicator", ["counterfeit_label"] = "Label", ["counterfeit_serial"] = "Serial",
            ["counterfeit_body"] = "Body", ["counterfeit_color"] = "Color", ["counterfeit_lettering"] = "Lettering",
            ["counterfeit_seal"] = "Seal", ["counterfeit_logo"] = "Logo", ["counterfeit_box"] = "Box",
            ["counterfeit_amount"] = "Amount", ["counterfeit_chip"] = "Chip",
            ["wanted"] = "Wanted by this customer", ["too_dear"] = "Wanted, but over their budget",
            ["wanted_inside"] = "Wanted by this customer: {0} item(s) inside",
            ["inspection_inside"] = "A search would find {0} item(s) inside",
            ["stolen"] = "Stolen goods (heat: {0})", ["contraband"] = "Contraband (level: {0})", ["contraband_plain"] = "Contraband", ["worth"] = "Worth: {0}",
            ["today"] = "Today", ["run"] = "Run", ["customer"] = "Customer", ["standing"] = "Standing",
            ["money_in"] = "Money in: {0}", ["money_out"] = "Money out: {0}", ["net"] = "Net: {0}",
            ["yesterday"] = "Yesterday", ["last_day"] = "Last day recorded", ["day_net"] = "Day {0}: net {1}", ["days"] = "Days recorded: {0}", ["line"] = "{0}: {1}",
            ["sales"] = "Sales", ["items_sold"] = "Items sold", ["purchases"] = "Purchases", ["items_bought"] = "Items bought",
            ["services"] = "Services", ["rent"] = "Rent", ["mortgage"] = "Mortgage", ["fees"] = "Fees and bribes",
            ["seized"] = "Goods seized", ["burgled"] = "Stolen from the shop", ["other"] = "Other income",
            ["upgrades"] = "Upgrades", ["margin"] = "Known sales margin", ["service_line"] = "Service ({0}): {1}",
            ["nobody"] = "Nobody at the counter", ["budget"] = "Budget left: {0}", ["no_budget"] = "No spending limit",
            ["wants_count"] = "Wanted items: {0}; within budget: {1}", ["client_line"] = "{0} — {1} — {2}",
            ["reputation"] = "{0}: {1} ({2})", ["no_standing"] = "No standing recorded",
            ["intent_buy"] = "Buying goods", ["intent_sell"] = "Selling goods", ["intent_sellnbuy"] = "Buying and selling goods",
            ["intent_inspection"] = "Inspection", ["intent_appraisal"] = "Appraisal",
            ["open_key_label"] = "Open Shopkeeping Suite", ["open_key_description"] = "Toggle the shop window. Default: F6.",
            ["enabled_label"] = "Enable Shopkeeping Suite",
            ["mark_size_label"] = "Mark size", ["mark_size_description"] = "How large the marks on items are drawn. Changes take effect now.",
            ["mark_same_kind_label"] = "Mark containers holding the same kind",
            ["mark_risk_label"] = "Mark risky goods on offer",
            ["mark_inspection_label"] = "Mark goods an inspection would find",
            ["mark_wanted_label"] = "Mark goods the customer wants",
            ["mark_wanted_inside_label"] = "Mark containers that hold what the customer wants",
            ["tooltip_wanted_label"] = "Show wanted goods in tooltips",
            ["tooltip_risk_label"] = "Show stolen goods and contraband in tooltips",
            ["tooltip_appraisal_label"] = "Show the correct appraisal value",
            ["ledger_label"] = "Keep the ledger", ["window_label"] = "Enable the shop window",
            ["ledger_at_closing_label"] = "Open today's ledger when the shutter closes",
            ["enabled_description"] = "All aids can be switched off here. Changes take effect now.",
            ["mark_same_kind_description"] = "White corner dots while dragging. Changes take effect now; start a new drag to find containers again.",
            ["mark_risk_description"] = "Red triangles for stolen goods, purple triangles for contraband offered by someone else. Changes take effect now.",
            ["mark_inspection_description"] = "Light red diamonds for your goods a search would find and containers holding them while security is at the counter. Goods in a Smuggler's Bay or hidden container are not marked. Changes take effect now.",
            ["mark_wanted_description"] = "Light green filled squares for wanted goods, light orange hollow squares if too dear. Changes take effect now.",
            ["mark_wanted_inside_description"] = "With wanted marks on, marks your containers holding wanted goods, including nested, locked and hidden stock. Light green if any wanted item inside is affordable, light orange otherwise. Changes take effect now.",
            ["tooltip_wanted_description"] = "Shows whether a buying customer wants your item and can afford it. Changes take effect now.",
            ["tooltip_risk_description"] = "Shows heat and contraband level on someone else's goods. Changes take effect now.",
            ["tooltip_appraisal_description"] = "Gives the answer to an appraisal while an appraisal customer is at the counter. Changes take effect now.",
            ["ledger_description"] = "Counts future sales and costs only while enabled. Missed events cannot be recovered. Changes take effect now.",
            ["window_description"] = "Today, Run, Customer and Standing in one window. Changes take effect now.",
            ["ledger_at_closing_description"] = "Opens the Today page after closing the shutter. Changes take effect now."
        };

        internal static string Get(string key, params object[] values)
        {
            string fallback = English[key], text = fallback;
            try { string found = Localise(key); if (!string.IsNullOrWhiteSpace(found) && found != key) text = found; }
            catch (Exception) { }
            try { return string.Format(CultureInfo.InvariantCulture, text, values); }
            catch (Exception) { return string.Format(CultureInfo.InvariantCulture, fallback, values); }
        }
    }
}
