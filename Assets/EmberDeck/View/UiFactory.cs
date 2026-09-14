using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// Small helpers for building uGUI from code.
    ///
    /// The whole interface is generated at runtime rather than authored as prefabs. For a
    /// vertical slice that is a deliberate trade: prefabs and scenes are binary-ish YAML
    /// that cannot be reviewed in a diff, merge badly, and drift silently from the code
    /// that drives them. Layout as code stays readable and version-controllable. Authored
    /// prefabs are the right call later, once the layout stops changing every hour.
    /// </summary>
    public static class UiFactory
    {
        static Font _font;

        /// <summary>
        /// The built-in legacy font. Chosen over TextMeshPro on purpose: TMP needs its
        /// "Essential Resources" imported through a dialog before any text renders, which
        /// breaks headless and first-clone runs. Swap to TMP once the project is set up.
        /// </summary>
        public static Font Font =>
            _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        public static RectTransform Panel(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            return (RectTransform)go.transform;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color,
                                 TextAnchor anchor = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            go.transform.SetParent(parent, false);

            var label = go.GetComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = size;
            label.color = color;
            label.alignment = anchor;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>
        /// Gives a panel or button a 9-sliced frame from Resources/Icons, with its border drawn
        /// <paramref name="thickness"/> units wide. Opt-in rather than built into Panel: Panel also makes
        /// health bars, fills, stripes and flash overlays, none of which should have a frame. A missing
        /// frame leaves the plain rectangle, which still works.
        /// </summary>
        public static void Frame(Image image, string spriteId, float thickness)
        {
            var sprite = Icons.Get(spriteId);
            if (image == null || sprite == null) return;
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.pixelsPerUnitMultiplier = sprite.border.x > 0f ? sprite.border.x / thickness : 1f;
        }

        public static Button TextButton(Transform parent, string name, string text, Color background,
                                        Color foreground, int fontSize = 26)
        {
            var rect = Panel(parent, name, background);
            Frame(rect.GetComponent<Image>(), "frame_button", 5f);
            var button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = rect.GetComponent<Image>();
            button.onClick.AddListener(() => AudioDirector.Play(Sfx.Click));

            var label = Label(rect, "Label", text, fontSize, foreground);
            Stretch(label.rectTransform);
            return button;
        }

        /// <summary>Makes a child fill its parent.</summary>
        public static void Stretch(RectTransform rect, float padding = 0f)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(padding, padding);
            rect.offsetMax = new Vector2(-padding, -padding);
        }

        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        /// <summary>
        /// A two-layer bar. The fill is sized with anchors rather than Image.fillAmount
        /// because filled images require a sprite, and this project ships no textures.
        /// </summary>
        public static Image Bar(Transform parent, string name, Color background, Color fill,
                                Vector2 size, Vector2 position)
        {
            var track = Panel(parent, name, background);
            Place(track, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position, size);

            var fillRect = Panel(track, "Fill", fill);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.zero;
            fillRect.offsetMax = Vector2.zero;
            return fillRect.GetComponent<Image>();
        }

        public static void SetBarFill(Image fill, float normalized)
        {
            var rect = (RectTransform)fill.transform;
            rect.anchorMax = new Vector2(Mathf.Clamp01(normalized), 1f);
            rect.offsetMax = Vector2.zero;
            rect.offsetMin = Vector2.zero;
        }
    }
}
