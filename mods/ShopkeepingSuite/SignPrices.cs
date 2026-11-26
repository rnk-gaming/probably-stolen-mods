using System;
using System.Collections.Generic;
using System.Linq;

namespace ShopkeepingSuite
{
    internal sealed class SignMarket
    {
        internal double Factor = 1;
        internal int Count, Seen;
        internal bool HasSign;
    }
    // The rules use data and supplied readers only; they never call the game.
    internal static class SignPrices
    {
        private static readonly Dictionary<string, string> Types = new Dictionary<string, string>
        {
            ["sign_water"] = "WATER", ["sign_food"] = "FOOD", ["sign_material"] = "MATERIAL",
            ["sign_medical"] = "MEDICAL", ["sign_household"] = "HOUSEHOLD_GOOD",
            ["sign_firearm"] = "FIREARM", ["sign_firearm_heavy"] = "FIREARM", ["sign_ruined_machine"] = "MACHINE"
        };
        internal static string GoodsType(string id) => id != null && Types.TryGetValue(id, out string type) ? type : null;
        internal static bool IsSign(string id) => GoodsType(id) != null || id == "sign_berry" || id == "sign_expedition";
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            ["WATER"] = "price_type_water", ["FOOD"] = "price_type_food", ["MATERIAL"] = "price_type_material", ["MEDICAL"] = "price_type_medical",
            ["HOUSEHOLD_GOOD"] = "price_type_household", ["FIREARM"] = "price_type_firearm", ["MACHINE"] = "price_type_machine"
        };
        internal static string NameKey(string type) => Names[type];
        internal static int EventShift(IEnumerable<PriceEntry> entries, string type) => PriceRules.EventShift(entries, tag => tag == type);
        internal static SignMarket Average(IEnumerable<double> factors)
        {
            double[] attempts = factors.Take(60).ToArray();
            double[] valid = attempts.Where(PriceRules.ValidFactor).ToArray();
            // Incremental mean: even sixty double.MaxValue factors must stay finite.
            double mean = 0;
            for (int i = 0; i < valid.Length; i++) mean += (valid[i] - mean) / (i + 1);
            return new SignMarket { Factor = valid.Length == 0 ? 1 : mean, Count = valid.Length, Seen = attempts.Length };
        }
        internal static SignMarket Sample(string type, IEnumerable<GameItem> shop, bool ready,
            Func<GameItem, string> id, Func<GameItem, string, bool> tag, Func<GameItem, double> read)
        {
            if (!ready) return new SignMarket();
            List<GameItem> items = shop.Where(item => item != null).Distinct().ToList();
            if (!items.Any(item => GoodsType(id(item)) == type)) return new SignMarket();
            SignMarket sample = Average(items.Where(item => !IsSign(id(item)) && tag(item, type)).Take(60).Select(read));
            sample.HasSign = true;
            return sample;
        }
    }
}
