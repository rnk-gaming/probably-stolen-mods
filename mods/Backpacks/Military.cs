using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

using LocalMods;

namespace Backpacks
{
    internal static class Military
    {
        internal const string Medium = "rnk.backpacks.scavengers_pack", Large = "rnk.backpacks.haulers_pack";
        // Items in saves made before the ids changed; remove when no such save is in use.
        internal const string LegacyMedium = "rnk_backpacks_scavengers_pack", LegacyLarge = "rnk_backpacks_haulers_pack";
        internal static string Canonical(string id) => id == LegacyMedium ? Medium : id == LegacyLarge ? Large : id;
        private static string PictureKey(string id) => Canonical(id) == Medium ? LegacyMedium : Canonical(id) == Large ? LegacyLarge : id;
        private static string modId;
        private static ConfigEntry<bool> enabled;
        internal static Func<bool> Enabled = () => enabled == null || enabled.Value;
        internal static Action<string, string> RegisterPicture = BuildPicture;
        internal static Func<string, bool> HasPicture = key => ModLoader.Mods.Exists(mod => mod.Manifest.ID == modId && mod.Sprites.ContainsKey(key));
        internal static Action<GameItem, string> Picture = (item, key) => item.SetSpriteAndShapeFromMod(modId, key);

        internal static bool IsMilitary(string id) => Canonical(id) == Medium || Canonical(id) == Large;

        internal static void Configure(string id)
        {
            modId = id;
            enabled = ModConfig.For(id).Define("own_backpacks_enabled", true, "Backpacks",
                () => "Scavenger's Pack and Hauler's Pack", () => "Allow pictures, upgrades and new sources for Scavenger's Pack and Hauler's Pack.");
        }

        // The mod is not yet in the loader's list while it is being initialised.
        // The pictures are made when
        // the loader says our assets are loaded, which is before any save is read, and
        // again, if still missing, when a military backpack is created.
        private static LoadedMod own;
        internal static void Subscribe()
        {
            Storage.Guard("own pack picture hook", () => ModHook.OnModAssetsLoaded += mod =>
            {
                Storage.Guard("own pack pictures after assets", () =>
                {
                    if (mod?.Manifest == null || mod.Manifest.ID != modId) return;
                    own = mod;
                    LoadPictures();
                });
            });
        }

        internal static void LoadPictures()
        {
            Storage.Guard("own pack pictures", () =>
            {
                // Saved sprite references must resolve before saves are read, even
                // when new item changes are off. This also allows enabling later.
                Storage.Guard("medium own pack picture", () => RegisterPicture(LegacyMedium, "simple_backpack_medium"));
                Storage.Guard("large own pack picture", () => RegisterPicture(LegacyLarge, "simple_backpack_large"));
            });
        }

        // Olive drab recolouring: the cloth, the
        // outlines and the buckles take one muted olive hue at 82 % of their lightness;
        // strongly coloured pixels, the orange trim, are kept as they are.
        internal static Color Recolour(Color pixel)
        {
            float value = Math.Max(pixel.r, Math.Max(pixel.g, pixel.b));
            float minimum = Math.Min(pixel.r, Math.Min(pixel.g, pixel.b));
            float saturation = value <= 0 ? 0 : (value - minimum) / value;
            if (pixel.a <= 0 || saturation >= 0.5f) return pixel;
            float green = value * 0.82f, blue = green * (1 - 0.26f);
            // Hue 78 degrees: 0.3 of the way from yellow to green.
            return new Color(blue + (green - blue) * 0.7f, green, blue, pixel.a);
        }

        private static void BuildPicture(string key, string sourceKey)
        {
            var ownMod = own ?? ModLoader.Mods?.Find(mod => mod?.Manifest != null && mod.Manifest.ID == modId);
            if (ownMod == null) throw new InvalidOperationException("Loaded mod sprite table missing");
            if (ownMod.Sprites.ContainsKey(key)) return;
            Sprite source = RenderHandler.LoadFromAtlas("Items/items_backpack2", sourceKey);
            if (source == null) throw new InvalidOperationException("Backpack atlas sprite missing");
            Rect rect = source.rect;
            int width = (int)rect.width, height = (int)rect.height;
            Texture2D texture = null;
            Sprite sprite = null;
            try
            {
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.filterMode = FilterMode.Point;
                texture.wrapMode = TextureWrapMode.Clamp;
                for (int y = 0; y < height; y++) for (int x = 0; x < width; x++)
                    texture.SetPixel(x, y, Recolour(source.texture.GetPixel((int)rect.x + x, (int)rect.y + y)));
                texture.Apply();
                sprite = Sprite.Create(texture, new Rect(0, 0, width, height),
                    new Vector2(source.pivot.x / width, source.pivot.y / height), source.pixelsPerUnit);
                if (sprite == null) throw new InvalidOperationException("Own pack sprite creation failed");
                ownMod.Sprites.Add(key, sprite);
            }
            catch
            {
                if (sprite != null) UnityEngine.Object.Destroy(sprite);
                if (texture != null) UnityEngine.Object.Destroy(texture);
                throw;
            }
        }

        internal static Func<string, Sprite> FindPicture = key =>
        {
            var mod = own ?? ModLoader.Mods?.Find(m => m?.Manifest != null && m.Manifest.ID == modId);
            return mod != null && mod.Sprites.TryGetValue(key, out Sprite sprite) ? sprite : null;
        };
        internal static string PictureOwner => modId;
        internal static bool Ours(string atlas, string key) => modId != null && atlas == modId
            && IsMilitary(key) && (object)FindPicture(key) != null;
        internal static Sprite Icon(string id) => IsMilitary(id) && Enabled() ? FindPicture(PictureKey(id)) : null;

        internal static void Attach(GameItem item, string id)
        {
            Storage.Guard("own pack item picture", () =>
            {
                if (item == null || !IsMilitary(id) || !Enabled()) return;
                Dress(item, id);
            });
        }

        // Attach guards restore faults; the factory must see a dressing failure so
        // that it can discard the inner item rather than return an unfinished pack.
        internal static void Dress(GameItem item, string id)
        {
            id = Canonical(id);
            if (string.IsNullOrEmpty(item.name) || item.name.StartsWith("item_", StringComparison.Ordinal))
                item.name = Texts.Get(id == Medium ? "scavengers_pack_name" : "haulers_pack_name");
            if (string.IsNullOrEmpty(item.shortDescription))
                item.shortDescription = Texts.Get(id == Medium ? "scavengers_pack_description" : "haulers_pack_description");
            item.unitValue = id == Medium ? 700 : 1000;
            string key = PictureKey(id);
            if (!HasPicture(key)) RegisterPicture(key, id == Medium ? "simple_backpack_medium" : "simple_backpack_large");
            if (!HasPicture(key)) throw new InvalidOperationException("Own pack picture unavailable");
            Picture(item, key);
        }
    }

    // The game's Item Spawner makes its icons through a factory entry point that our
    // item patch does not see. Replace the placeholder for our two ids with our picture.
    [HarmonyPatch]
    internal static class MilitarySpawnerIconPatch
    {
        internal static MethodBase TargetMethod() => AccessTools.Method("CustomUIItemSpawner:ResolveIcon", new[] { typeof(string) });
        internal static bool Prepare() => TargetMethod() is MethodInfo method && method.IsStatic && method.ReturnType == typeof(Sprite);
        internal static void Postfix(string identifier, ref Sprite __result)
        {
            try
            {
                Sprite ours = Military.Icon(identifier);
                if ((object)ours != null) __result = ours;
            }
            catch (Exception error) { Storage.Fault("own pack spawner icon", error); }
        }
    }

    // Some of the game's windows show an item by copying its picture's atlas path and
    // sprite name onto a new item with the ordinary routine (the list of backpacks to
    // take when leaving the shop does). For an item with a picture from a mod the atlas
    // path is the mod's id, which that routine cannot load, and the game shows its
    // question mark. When the path is our own mod's id
    // and the name is one of our pictures, the game's own routine for mod pictures is
    // used in its place.
    [HarmonyPatch(typeof(GameItem), nameof(GameItem.SetSpriteAndShape), new[] { typeof(string), typeof(string) })]
    internal static class MilitaryCopiedPicturePatch
    {
        internal static Func<GameItem, string, string, GameItem> FromMod = (item, mod, key) => item.SetSpriteAndShapeFromMod(mod, key);

        internal static bool Prefix(GameItem __instance, string spriteAtlasPath, string spritePath, ref GameItem __result)
        {
            try
            {
                if (!Military.Ours(spriteAtlasPath, spritePath)) return true;
                __result = FromMod(__instance, Military.PictureOwner, spritePath);
                return false;
            }
            catch (Exception error) { Storage.Fault("own pack copied picture", error); return true; }
        }
    }

}
