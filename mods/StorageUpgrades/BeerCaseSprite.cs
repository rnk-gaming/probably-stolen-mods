using System;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using LocalMods;

namespace LocalMods
{
    internal static partial class StorageRules
    {
        internal static int BeerPictureIndex(int count, int capacity)
        {
            if (count <= 0 || capacity <= 0) return 0;
            return (int)Math.Min(8, ((long)count * 8 + capacity - 1) / capacity);
        }
    }
}

namespace StorageUpgrades
{
    internal static class BeerCaseSprite
    {
        private const string Owner = "mod.rnk.storage-upgrades.beer-case-picture";
        internal static bool Active;
        internal static Func<GameInventory, int> Count = inventory => inventory.childItems.Count;
        internal static Action<GameItem, string, string> SetPicture = (item, atlas, picture) => item.SetSpriteAndShape(atlas, picture);

        private static bool Signature(MethodInfo method, Type owner, string name, Type result, params Type[] arguments)
            => method != null && method.DeclaringType == owner && method.Name == name && !method.IsStatic
                && !method.ContainsGenericParameters && method.ReturnType == result
                && method.GetParameters().Select(parameter => parameter.ParameterType).SequenceEqual(arguments);

        internal static bool Fits(MethodInfo update, MethodInfo setPicture)
            => Signature(update, typeof(ContainerItemDirectory), "UpdateBeerCaseSprite", typeof(void), typeof(GameItem), typeof(GameInventory))
                && update.IsPrivate
                && Signature(setPicture, typeof(GameItem), "SetSpriteAndShape", typeof(GameItem), typeof(string), typeof(string))
                && setPicture.IsPublic;

        internal static void Install()
        {
            Active = false;
            Storage.Guard("beer case picture patch", () =>
            {
                MethodInfo update = typeof(ContainerItemDirectory).GetMethod("UpdateBeerCaseSprite",
                    BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly, null,
                    new[] { typeof(GameItem), typeof(GameInventory) }, null);
                MethodInfo setter = typeof(GameItem).GetMethod("SetSpriteAndShape",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly, null,
                    new[] { typeof(string), typeof(string) }, null);
                if (!Fits(update, setter)) throw new InvalidOperationException("the Beer Case picture methods have a different shape; no picture patch applied");
                PatchChecked(update);
                Active = true;
            });
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void PatchChecked(MethodInfo update)
        {
            if (Harmony.GetPatchInfo(update)?.Owners.Contains(Owner) == true) return;
            new Harmony(Owner).Patch(update, prefix: new HarmonyMethod(typeof(BeerCaseSprite), nameof(Prefix)));
        }

        internal static void Disable()
        {
            Active = false;
            Storage.Guard("removing beer case picture patch", () => new Harmony(Owner).UnpatchAll(Owner));
        }
        internal static bool Prefix(GameItem __0, GameInventory __1)
        {
            try
            {
                if (!Active || __0 == null || __1 == null || __0.identifier != "beerCase") return true;
                // Use the actual grid, including while a save is being filled. Tags and
                // the join switch do not say how many bottles an existing case holds.
                int[][] grids = Storage.Sizes(__0);
                if (grids == null || grids.Length != 1 || grids[0] == null || grids[0].Length != 2
                    || grids[0][0] <= 0 || grids[0][1] <= 0) return true;
                long area = (long)grids[0][0] * grids[0][1];
                if (area % 4 != 0 || area / 4 > int.MaxValue) return true;
                int capacity = (int)(area / 4);
                int count = Count(__1);
                if (count < 0) return true;
                // A base case with up to eight bottles is the game's own business. More
                // than eight in a grid that reads as a base case (a save still being
                // filled) would ask for a picture that does not exist: show it full.
                if (capacity <= 8 && count <= 8) return true;
                if (capacity < 8) capacity = 8;
                SetPicture(__0, "Items/beer_case", "beer_case_" + StorageRules.BeerPictureIndex(count, capacity));
                return false;
            }
            catch (Exception error) { Storage.Fault("beer case picture", error); return true; }
        }
    }
}
