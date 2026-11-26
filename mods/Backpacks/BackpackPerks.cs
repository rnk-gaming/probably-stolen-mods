using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using LocalMods;

namespace Backpacks
{
    internal static class BackpackPerks
    {
        internal const string Courier = "rnk.backpacks.seasoned_courier", Quartermaster = "rnk.backpacks.quartermasters_issue";
        // A white glyph per perk, files of our own in Textures/Items (drawn by the Perks
        // mod's art/icons.py). Without a key the game draws its default perk icon, which
        // covers the neighbouring perks.
        internal static string Icon(string id) => Texts.ModId + ":rnk_backpacks_" + id.Substring(id.LastIndexOf('.') + 1);
        private static readonly Dictionary<string, ConfigEntry<bool>> offers = new Dictionary<string, ConfigEntry<bool>>();
        internal static Func<string, bool> Offer = id => !offers.TryGetValue(id, out ConfigEntry<bool> entry) || entry.Value;
        internal static Func<string, bool> Active = id => ModHelper.IsStartingPerkActive(id);
        internal static Action<GameItem> Place = item => GraphUtils.EmporiumTryAdd(EmporiumEntry.Instance.invElement, item);
        internal static Action<string, int, Func<string>, Func<string>> Register = (id, cost, name, description) =>
            ModHelper.AddStartingPerk(Texts.ModId, id, cost, StartingPerk.StartingPerkType.POSITIVE, name, description, Icon(id));
        internal static Action Remove = () => ModHelper.RemoveStartingPerks(Texts.ModId);

        internal static void Configure(string modId)
        {
            ModConfig config = ModConfig.For(modId);
            offers[Courier] = config.Define("offer_seasoned_courier", true, "Backpacks", () => "Offer Seasoned Courier in a new game",
                () => "Restart needed. Adds the starting perk to the new-game screen.");
            offers[Quartermaster] = config.Define("offer_quartermasters_issue", true, "Backpacks", () => "Offer Quartermaster's Issue in a new game",
                () => "Restart needed. Adds the starting perk to the new-game screen.");
        }

        internal static void Enable()
        {
            Storage.Guard("backpack starting perk registration", () =>
            {
                if (!Backpacks.Switch("backpacks_enabled")) return;
                if (Offer(Courier)) Register(Courier, 1, () => Texts.Get("perk_courier_name"), () => Texts.Get("perk_courier_description") + " Stack size needs a stacking mod.");
                if (Offer(Quartermaster)) Register(Quartermaster, 3, () => Texts.Get("perk_quartermaster_name"), () => Texts.Get("perk_quartermaster_description") + " Stack size needs a stacking mod.");
            });
        }

        internal static void Disable() => Storage.Guard("removing backpack starting perks", () => Remove());

        internal static void Give()
        {
            Storage.Guard("starting backpack", () =>
            {
                foreach (string perk in new[] { Courier, Quartermaster })
                {
                    Storage.Guard("starting backpack", () =>
                    {
                        if (!Active(perk)) return;
                        string name = Texts.Get(perk == Courier ? "perk_courier_name" : "perk_quartermaster_name");
                        if (!Backpacks.Switch("backpacks_enabled"))
                        {
                            Storage.Fault(name + " gave nothing because backpack upgrades are off", new InvalidOperationException("Backpack upgrades are disabled"));
                            return;
                        }
                        GameItem item = perk == Courier ? DebugTools.Make(Military.Medium, 0, 1, 1, 1, 0, 2) : DebugTools.MakeFull(Military.Large);
                        if (item == null) throw new InvalidOperationException("The starting backpack was not built completely; not handing it out");
                        try
                        {
                            Place(item);
                            if (item.parentInventory == null)
                                throw new InvalidOperationException(name + " backpack found no room");
                        }
                        catch (Exception error)
                        {
                            if (item.parentInventory == null) Storage.Guard("discarding an unplaced starting backpack", () => Material.Destroy(item));
                            Storage.Fault(name + " backpack found no room", error);
                        }
                    });
                }
            });
        }
    }

    [HarmonyPatch]
    internal static class StartingBackpacksPatch
    {
        internal static MethodBase TargetMethod() => AccessTools.Method(typeof(StartingPerkContent), "HandleNewGamePerk", Type.EmptyTypes);
        internal static bool Prepare()
        {
            try { return TargetMethod() is MethodInfo method && method.IsStatic && !method.ContainsGenericParameters && method.ReturnType == typeof(void) && method.GetParameters().Length == 0; }
            catch (Exception error) { Storage.Fault("starting backpack patch shape", error); return false; }
        }
        internal static void Postfix() => BackpackPerks.Give();
    }
}
