using System.Collections.Generic;
using EmberDeck.Combat;
using EmberDeck.Content;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// UI icons, loaded by name from Resources/Icons (rendered by art/render_icons.sh).
    ///
    /// Loaded by name for the same reason as the audio: the scene is generated from code and has
    /// nothing to wire up. A missing icon renders as nothing rather than as a white square, so the
    /// game stays readable — the number beside it still says what it needs to.
    /// </summary>
    public static class Icons
    {
        static readonly Dictionary<string, Sprite> Cache = new();

        public static Sprite Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (!Cache.TryGetValue(id, out var sprite))
            {
                sprite = Resources.Load<Sprite>($"Icons/{id}");
                Cache[id] = sprite;
            }
            return sprite;
        }

        public static Sprite For(StatusType status) => Get(Keywords.For(status)?.Icon);

        public static Sprite For(IntentKind kind) => Get(kind switch
        {
            IntentKind.Attack => "intent_attack",
            IntentKind.Block  => "intent_block",
            IntentKind.Buff   => "intent_buff",
            IntentKind.Debuff => "intent_debuff",
            _                 => "intent_unknown",
        });

        /// <summary>An icon image with a fixed size that layout groups respect.</summary>
        public static Image Create(Transform parent, string name, Sprite sprite, float size)
        {
            var rect = UiFactory.Panel(parent, name, Color.white);
            rect.sizeDelta = new Vector2(size, size);

            var layout = rect.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = size;
            layout.minHeight = layout.preferredHeight = size;

            var image = rect.GetComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            SetSprite(image, sprite);
            return image;
        }

        public static void SetSprite(Image image, Sprite sprite)
        {
            image.sprite = sprite;
            image.color = sprite != null ? Color.white : Color.clear;
        }
    }
}
