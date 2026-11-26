using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace Stacking
{
    internal static class TypeGuard
    {
        internal static Func<Type> FindType = () => AccessTools.TypeByName("StationEconomy.Stacking");
        internal static Func<MethodInfo> FindIdentical = () => AccessTools.Method("StationEconomy.Stacking:Identical", new[] { typeof(GameItem), typeof(GameItem) });
        internal static Func<FieldInfo> FindItemTypes = () => AccessTools.Field(typeof(GameItem), "itemTypes");
        internal static Func<FieldInfo> FindAllTypes = () => AccessTools.Field(typeof(TypeHelper), "ALL_TYPES_LIST");
        internal static Action<Harmony, MethodInfo, HarmonyMethod> Patch = (harmony, target, prefix) => harmony.Patch(target, prefix: prefix);
        internal static bool Installed;
        private static bool gaveUp;
        private static string[] typeNames;

        internal static bool Fits(Type type, MethodInfo method, FieldInfo itemTypes, FieldInfo allTypes)
        {
            if (type == null || method == null || method.DeclaringType != type || !method.IsPublic || !method.IsStatic
                || method.IsGenericMethod || method.ContainsGenericParameters || method.ReturnType != typeof(bool)
                || itemTypes == null || itemTypes.IsStatic || itemTypes.FieldType != typeof(List<string>)
                || !itemTypes.DeclaringType.IsAssignableFrom(typeof(GameItem))
                || allTypes == null || !allTypes.IsStatic || allTypes.FieldType != typeof(List<string>)) return false;
            ParameterInfo[] args = method.GetParameters();
            return args.Length == 2 && args[0].ParameterType == typeof(GameItem) && args[1].ParameterType == typeof(GameItem);
        }

        internal static void TryInstall()
        {
            Harmony harmony = null;
            MethodInfo target = null, prefix = null;
            try
            {
                if (!Bridge.Active || Installed || gaveUp || !Bridge.Running()) return;
                Type type = FindType();
                if (type == null) return; // Workshop assemblies may arrive after ours.
                target = FindIdentical();
                FieldInfo items = FindItemTypes(), all = FindAllTypes();
                if (!Fits(type, target, items, all) || !(all.GetValue(null) is List<string> names))
                {
                    gaveUp = true;
                    Bridge.Report("types shape", "Station Economy or the game's item types are laid out differently; item types were not patched.");
                    return;
                }
                // A snapshot in the game's order, read once rather than on each pair.
                typeNames = names.ToArray();
                harmony = new Harmony(Bridge.HarmonyId);
                prefix = AccessTools.Method(typeof(TypeGuard), nameof(Prefix));
                Patch(harmony, target, new HarmonyMethod(prefix));
                Installed = true;
                Bridge.Report("types installed", "an item split off a stack keeps its types (with Station Economy's stacking).");
            }
            catch (Exception error)
            {
                gaveUp = true; Installed = false; typeNames = null;
                try { if (harmony != null && target != null && prefix != null) harmony.Unpatch(target, prefix); }
                catch (Exception removal) { Bridge.Fault("removing an incomplete item types patch", removal); }
                Bridge.Fault("connecting item types to Station Economy", error);
            }
        }

        private static readonly Func<TagSystem, string, bool> KnownAndOn = (tags, name) =>
            typeNames != null && Array.IndexOf(typeNames, name) >= 0 && Bridge.TagOn(tags, name);

        internal static void Reset() { Installed = gaveUp = false; typeNames = null; }

        // A void prefix always lets Economy compare the items, also after our fault.
        // No finalizer: Economy's own exceptions must still reach its caller.
        private static void Prefix(GameItem __0, GameItem __1)
        {
            try
            {
                if (!Installed || ReferenceEquals(__0, null) || ReferenceEquals(__1, null) || ReferenceEquals(__0, __1)
                    || __0.identifier != __1.identifier || !Bridge.On() || !Bridge.Switch("types_follow_marks")) return;
                Mend(__0, __1);
                Mend(__1, __0);
            }
            catch (Exception error) { Bridge.Fault("letting item types follow their marks", error); }
        }

        // Only a type the other item of the pair holds is put back. The game sets
        // some marks with a type's name on items that never had the type (a book is
        // marked DOCUMENT without being one), so a mark alone proves nothing.
        private static void Mend(GameItem item, GameItem other)
        {
            List<string> types = item.itemTypes, wanted = other.itemTypes;
            if (types == null || wanted == null || ReferenceEquals(types, wanted) || item.state == null) return;
            int before = types.Count;
            Rules.FollowMarks(types, wanted, item.state, KnownAndOn);
            // Nothing is allocated on the usual path, with no additions to report.
            for (int i = before; i < types.Count; i++)
                Bridge.Report("types added:" + item.identifier + ":" + types[i],
                    "restored type " + types[i] + " on " + item.identifier + " from its enabled mark.");
        }
    }
}
