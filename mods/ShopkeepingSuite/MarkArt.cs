using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UI;

namespace ShopkeepingSuite
{
    internal static class MarkArt
    {
        internal static int PriceHeight(int kind, int size) => Math.Abs(kind) == 2 ? size * 2 - 1 : size;
        internal static byte[] PriceRaster(int kind, int size)
        {
            int height = PriceHeight(kind, size); var pixels = new byte[size * height];
            if (kind == 0 || Math.Abs(kind) > 2) return pixels;
            int middle = size / 2;
            for (int arrow = 0; arrow < Math.Abs(kind); arrow++)
            {
                int bottom = arrow * (size - 1);
                for (int y = 1; y < size - 1; y++)
                {
                    pixels[(bottom + y) * size + middle] = 2;
                    int depth = size - 2 - y;
                    if (depth <= Math.Max(1, (size - 3) / 2))
                        for (int x = Math.Max(1, middle - depth); x <= Math.Min(size - 2, middle + depth); x++) pixels[(bottom + y) * size + x] = 2;
                }
            }
            for (int y = 0; y < height; y++) for (int x = 0; x < size; x++)
            {
                if (pixels[y * size + x] != 0) continue;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && nx < size && ny >= 0 && ny < height && pixels[ny * size + nx] == 2) pixels[y * size + x] = 1;
                }
            }
            if (kind < 0)
                for (int y = 0; y < height / 2; y++) for (int x = 0; x < size; x++)
                { byte value = pixels[y * size + x]; pixels[y * size + x] = pixels[(height - 1 - y) * size + x]; pixels[(height - 1 - y) * size + x] = value; }
            return pixels;
        }
        internal static string Shape(int kind) => kind == 1 ? "dot" : kind == 2 || kind == 3 ? "triangle"
            : kind == 4 ? "diamond" : kind == 5 ? "square" : kind == 6 ? "hollow square" : "none";
        internal static int Size(int setting) => setting <= 1 ? 5 : setting >= 3 ? 8 : 6;
        // 0 transparent, 1 dark outline, 2 the kind's palette colour.
        internal static byte[] Pixels(int kind) => Raster(kind, 6);
        internal static byte[] Raster(int kind, int size)
        {
            var pixels = new byte[size * size];
            if (kind < 1 || kind > 6) return pixels;
            float middle = (size - 1) / 2f;
            for (int y = 1; y < size - 1; y++) for (int x = 1; x < size - 1; x++)
            {
                float dx = Math.Abs(x - middle), dy = Math.Abs(y - middle);
                bool fill = kind == 1 ? dx <= (size - 3) / 3f && dy <= (size - 3) / 3f
                    : kind == 2 || kind == 3 ? dx <= (size - 1 - y) / 2f
                    : kind == 4 ? dx + dy <= size / 2f - 1
                    : kind == 5 || x == 1 || y == 1 || x == size - 2 || y == size - 2;
                if (fill) pixels[y * size + x] = 2;
            }
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                if (pixels[y * size + x] != 0 || kind == 6 && x > 1 && y > 1 && x < size - 2 && y < size - 2) continue;
                for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx >= 0 && nx < size && ny >= 0 && ny < size && pixels[ny * size + nx] == 2) pixels[y * size + x] = 1;
                }
            }
            return pixels;
        }
        private sealed class Visual { internal Image Image; internal int Kind, Size; }
        private static readonly ConditionalWeakTable<GameItem, Visual> visuals = new ConditionalWeakTable<GameItem, Visual>();
        private static readonly ConditionalWeakTable<GameItem, Visual> priceVisuals = new ConditionalWeakTable<GameItem, Visual>();
        private static readonly List<WeakReference<Image>> live = new List<WeakReference<Image>>();
        private static readonly List<WeakReference<Image>> priceLive = new List<WeakReference<Image>>();
        private static readonly Sprite[,] sprites = new Sprite[7, 3];
        private static readonly Sprite[,] priceSprites = new Sprite[5, 3];
        private static Sprite Picture(int kind, int size, bool price)
        {
            int index = (size - 4) / 2;
            Sprite[,] pictures = price ? priceSprites : sprites;
            int key = price ? kind + 2 : kind;
            if (pictures[key, index] != null) return pictures[key, index];
            Texture2D texture = null; Sprite sprite = null;
            try
            {
                byte[] mask = price ? PriceRaster(kind, size) : Raster(kind, size);
                int height = price ? PriceHeight(kind, size) : size;
                var pixels = new Color[mask.Length]; Color colour = price ? (kind > 0 ? new Color(.3f, .95f, .35f, 1) : new Color(1f, .25f, .2f, 1)) : Marks.Colour(kind);
                for (int i = 0; i < pixels.Length; i++) pixels[i] = mask[i] == 2 ? colour : mask[i] == 1 ? new Color(.06f, .06f, .06f, 1) : Color.clear;
                texture = new Texture2D(size, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                texture.SetPixels(pixels); texture.Apply(false, true);
                sprite = Sprite.Create(texture, new Rect(0, 0, size, height), new Vector2(price ? 1 : 0, 1), 1);
                pictures[key, index] = sprite; return sprite;
            }
            catch
            {
                if (sprite != null) UnityEngine.Object.Destroy(sprite);
                if (texture != null) UnityEngine.Object.Destroy(texture);
                throw;
            }
        }
        internal static void Draw(GameItem item, int kind, int size)
            => DrawAt(item, kind, size, false);
        internal static void DrawPrice(GameItem item, int kind, int size)
            => DrawAt(item, kind, size, true);
        private static void DrawAt(GameItem item, int kind, int size, bool price)
        {
            if (!(item is GameItemElement element) || element.IsDestroyed()) return;
            if (kind == 0) { HideAt(item, price); return; }
            Image background = element.background?.image;
            if (background == null) { HideAt(item, price); return; }
            Visual visual = (price ? priceVisuals : visuals).GetOrCreateValue(item);
            if (visual.Image == null)
            {
                var node = new GameObject(price ? "Shopkeeping price mark" : "Shopkeeping mark", typeof(RectTransform), typeof(Image));
                visual.Image = node.GetComponent<Image>();
                visual.Kind = visual.Size = 0;
                (price ? priceLive : live).Add(new WeakReference<Image>(visual.Image));
                visual.Image.raycastTarget = false;
                // A child is above the picture itself. First sibling is below the
                // picture's other children, including any stack count badge.
                visual.Image.rectTransform.SetParent(background.rectTransform, false);
                visual.Image.rectTransform.SetAsFirstSibling();
                visual.Image.rectTransform.anchorMin = visual.Image.rectTransform.anchorMax = visual.Image.rectTransform.pivot = new Vector2(price ? 1 : 0, 1);
                visual.Image.rectTransform.anchoredPosition = new Vector2(price ? -1 : 1, -1);
            }
            if (visual.Kind != kind || visual.Size != size)
            {
                visual.Image.sprite = Picture(kind, size, price);
                visual.Image.rectTransform.sizeDelta = new Vector2(size, price ? PriceHeight(kind, size) : size);
                visual.Kind = kind; visual.Size = size;
            }
            visual.Image.enabled = true;
        }
        internal static void Hide(GameItem item)
            => HideAt(item, false);
        private static void HideAt(GameItem item, bool price)
        {
            if (item != null && (price ? priceVisuals : visuals).TryGetValue(item, out Visual visual) && visual.Image != null) visual.Image.enabled = false;
        }
        internal static void Clear()
            => ClearImages(live);
        internal static void ClearPrice() => ClearImages(priceLive);
        private static void ClearImages(List<WeakReference<Image>> images)
        {
            for (int i = images.Count - 1; i >= 0; i--)
                if (images[i].TryGetTarget(out Image image) && image != null) image.enabled = false;
                else images.RemoveAt(i);
        }
    }
}
