using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// The shapes a hit is drawn with: an expanding ring and a soft glow.
    ///
    /// Both sprites are generated once at runtime rather than painted. A ring and a radial
    /// gradient are two lines of maths each, and a hand-painted PNG for them would be one more
    /// file to keep in step with the palette — while an impact needs to be tinted differently for
    /// a burn, a blocked hit and a heavy blow.
    ///
    /// Everything here is view-only, like Motion: it draws on the effects layer above the board
    /// and never touches combat state.
    /// </summary>
    public static class Fx
    {
        const int Size = 128;

        static Sprite _ring;
        static Sprite _glow;

        /// <summary>A soft annulus, for the shockwave of a hit landing.</summary>
        public static Sprite Ring => _ring ??= Build(ring: true);

        /// <summary>A radial gradient, for the light a hit throws.</summary>
        public static Sprite Glow => _glow ??= Build(ring: false);

        static Sprite Build(bool ring)
        {
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false)
            {
                name = ring ? "fx_ring" : "fx_glow",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave,
            };

            var pixels = new Color32[Size * Size];
            const float centre = (Size - 1) * 0.5f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float dx = (x - centre) / centre;
                    float dy = (y - centre) / centre;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);

                    // The ring peaks just inside the edge and falls away on both sides; the glow is
                    // brightest at the centre. Both fade to nothing by r = 1 so no edge shows.
                    float a = ring
                        ? Mathf.Exp(-((r - 0.76f) * (r - 0.76f)) / (2f * 0.115f * 0.115f))
                        : Mathf.Pow(Mathf.Clamp01(1f - r), 2.2f);
                    if (r > 1f) a = 0f;

                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);

            var sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
            sprite.name = texture.name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        /// <summary>
        /// A ring that expands out of a point and fades — the shockwave of a landed hit. Sized in
        /// the layer's units, so a heavy blow can simply ask for a bigger one.
        /// </summary>
        public static void Burst(RectTransform layer, Vector2 position, Color color, float size,
                                 float delay = 0f, float duration = 0.42f)
        {
            if (layer == null || Settings.ReducedMotion) return;

            var image = Image(layer, "Burst", Ring, color, position, size);
            var rect = image.rectTransform;
            Motion.Run(image, null, duration, t =>
            {
                float s = Mathf.LerpUnclamped(0.35f, 1.25f, Motion.OutCubic(t));
                rect.sizeDelta = new Vector2(size * s, size * s);
                var c = color;
                c.a = color.a * (1f - t) * (1f - t);
                image.color = c;
            }, Motion.Linear, delay, () => { if (image != null) Object.Destroy(image.gameObject); });
        }

        /// <summary>A flare of light at a point: fast in, fast out. Drawn under the ring of the same hit.</summary>
        public static void Flare(RectTransform layer, Vector2 position, Color color, float size,
                                 float delay = 0f, float duration = 0.3f)
        {
            if (layer == null || Settings.ReducedMotion) return;

            var image = Image(layer, "Flare", Glow, color, position, size);
            var rect = image.rectTransform;
            Motion.Run(image, null, duration, t =>
            {
                // Grows fast, then keeps growing while it dies, so the light reads as thrown outward.
                float s = Mathf.LerpUnclamped(0.5f, 1.15f, Motion.OutCubic(t));
                rect.sizeDelta = new Vector2(size * s, size * s);
                var c = color;
                c.a = color.a * (t < 0.25f ? t / 0.25f : 1f - (t - 0.25f) / 0.75f);
                image.color = c;
            }, Motion.Linear, delay, () => { if (image != null) Object.Destroy(image.gameObject); });
        }

        /// <summary>
        /// A shield raised: the Block icon swells over whoever gained it, holds, then shrinks away. Not
        /// decoration — it is how the player sees that the card they played did something, and a Block
        /// card that only changes a small number in a badge feels like it did nothing at all. Shown with
        /// reduced motion too, without the swell.
        /// </summary>
        public static void Shield(RectTransform layer, Vector2 position, float size, float delay = 0f, bool shatter = false)
        {
            if (layer == null) return;
            var sprite = Icons.Get("res_block");
            if (sprite == null) return;

            var image = Image(layer, shatter ? "ShieldHit" : "Shield", sprite, Palette.Block, position, size);
            var rect = image.rectTransform;
            float duration = Pace.S(shatter ? 0.45f : 0.75f);
            bool still = Settings.ReducedMotion;

            Motion.Run(image, null, duration, t =>
            {
                // Raised: swell past full size and settle, hold, then shrink toward nothing.
                // Struck: already up, jolt outward, and break away faster.
                float scale, alpha;
                if (shatter)
                {
                    scale = still ? 1f : 1f + 0.35f * Motion.OutCubic(t);
                    alpha = 1f - t;
                }
                else
                {
                    scale = still ? 1f : t < 0.3f ? Motion.OutBack(t / 0.3f) : t < 0.7f ? 1f : 1f - 0.6f * ((t - 0.7f) / 0.3f);
                    alpha = t < 0.15f ? t / 0.15f : t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
                }
                rect.localScale = new Vector3(scale, scale, 1f);
                var c = Palette.Block;
                c.a = 0.9f * alpha;
                image.color = c;
            }, Motion.Linear, delay, () => { if (image != null) Object.Destroy(image.gameObject); });

            if (!still)
                Burst(layer, position, new Color(Palette.Block.r, Palette.Block.g, Palette.Block.b, shatter ? 0.9f : 0.6f),
                      size * (shatter ? 1.6f : 1.3f), delay + (shatter ? 0f : Pace.S(0.12f)), Pace.S(0.5f));
        }

        static Image Image(RectTransform layer, string name, Sprite sprite, Color color, Vector2 position, float size)
        {
            var host = new GameObject(name, typeof(RectTransform), typeof(Image));
            host.transform.SetParent(layer, false);

            var image = host.GetComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            var start = color;
            start.a = 0f;
            image.color = start;

            UiFactory.Place(image.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position,
                            new Vector2(size, size));
            return image;
        }
    }
}
