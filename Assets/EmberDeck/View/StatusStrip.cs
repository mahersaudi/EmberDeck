using System;
using System.Collections.Generic;
using EmberDeck.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// A row of status chips — icon and number — in place of a line of text.
    ///
    /// "Burn 3   Strength 2   Vulnerable 1" had to be read word by word, and with three statuses
    /// it ran wider than the enemy it belonged to. A chip is recognised by shape and colour before
    /// it is read, and a number that goes up punches so the change is noticed.
    ///
    /// Chips never catch the pointer. The panel they sit on owns the tooltip, which lists every
    /// status with its definition.
    /// </summary>
    public sealed class StatusStrip : MonoBehaviour
    {
        const float IconSize = 24f;

        sealed class Chip
        {
            public GameObject Root;
            public Text Value;
            public int Shown;
        }

        readonly Dictionary<StatusType, Chip> _chips = new();

        public static StatusStrip Create(Transform parent, string name, TextAnchor alignment)
        {
            var host = new GameObject(name, typeof(RectTransform));
            host.transform.SetParent(parent, false);

            var layout = host.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 6f;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            return host.AddComponent<StatusStrip>();
        }

        public void Set(Actor actor)
        {
            foreach (StatusType status in Enum.GetValues(typeof(StatusType)))
            {
                int value = actor?.GetStatus(status) ?? 0;
                _chips.TryGetValue(status, out var chip);

                if (value == 0)
                {
                    if (chip != null) chip.Root.SetActive(false);
                    continue;
                }

                if (chip == null)
                {
                    chip = CreateChip(status);
                    _chips[status] = chip;
                }

                bool appeared = !chip.Root.activeSelf;
                chip.Root.SetActive(true);
                chip.Root.transform.SetAsLastSibling();   // walking the enum in order keeps chips in vocabulary order

                if (value == chip.Shown && !appeared) continue;
                bool grew = value > chip.Shown && !appeared;
                chip.Shown = value;
                chip.Value.text = value.ToString();
                if (grew || appeared) Motion.Punch(chip.Root.transform, 0.3f, 0.3f);
            }
        }

        Chip CreateChip(StatusType status)
        {
            var keyword = Keywords.For(status);

            var root = UiFactory.Panel(transform, $"Status_{status}", new Color(0f, 0f, 0f, 0.55f));
            root.GetComponent<Image>().raycastTarget = false;

            var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(3, 8, 2, 2);
            layout.spacing = 3f;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;

            Icons.Create(root, "Icon", Icons.For(status), IconSize);

            var value = UiFactory.Label(root, "Value", "", 19, keyword?.Color ?? Palette.Ink);
            value.horizontalOverflow = HorizontalWrapMode.Overflow;
            value.fontStyle = FontStyle.Bold;
            var valueLayout = value.gameObject.AddComponent<LayoutElement>();
            valueLayout.minWidth = 10f;
            valueLayout.preferredHeight = IconSize;

            return new Chip { Root = root.gameObject, Value = value, Shown = 0 };
        }
    }
}
