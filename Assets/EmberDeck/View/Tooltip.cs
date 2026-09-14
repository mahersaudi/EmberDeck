using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// The one tooltip panel: an icon, a title and a sentence per entry, placed beside whatever the
    /// pointer is over.
    ///
    /// It lives on its own canvas above everything, including the map and the reward screen, so a
    /// reward card can be explained as readily as a card in hand. It has no raycaster on purpose: a
    /// tooltip that can catch the pointer flickers, because the moment it covers the thing it
    /// explains, that thing receives pointer-exit and hides it.
    /// </summary>
    public sealed class Tooltip : MonoBehaviour
    {
        public readonly struct Entry
        {
            public readonly string Title;
            public readonly string Body;
            public readonly string Icon;
            public readonly Color TitleColor;

            public Entry(string title, string body, string icon = null, Color? titleColor = null)
            {
                Title = title;
                Body = body;
                Icon = icon;
                TitleColor = titleColor ?? Palette.Ink;
            }

            public static Entry From(Keywords.Keyword keyword, string title = null) =>
                new(title ?? keyword.Word, keyword.Body, keyword.Icon, keyword.Color);
        }

        const float Width = 340f;
        const float Gap = 14f;
        const float IconSize = 34f;

        static Tooltip _instance;

        Canvas _canvas;
        RectTransform _panel;
        Object _owner;
        readonly List<GameObject> _rows = new();

        static Tooltip Instance
        {
            get
            {
                if (_instance == null) Create();
                return _instance;
            }
        }

        static void Create()
        {
            var host = new GameObject("[Tooltip]", typeof(Canvas), typeof(CanvasScaler));
            DontDestroyOnLoad(host);

            var canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;

            var scaler = host.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _instance = host.AddComponent<Tooltip>();
            _instance._canvas = canvas;

            var panel = UiFactory.Panel(host.transform, "Panel", new Color(0.06f, 0.06f, 0.08f, 0.96f));
            panel.GetComponent<Image>().raycastTarget = false;
            UiFactory.Frame(panel.GetComponent<Image>(), "frame_panel", 4f);
            panel.pivot = new Vector2(0f, 1f);
            panel.sizeDelta = new Vector2(Width, 10f);

            var border = panel.gameObject.AddComponent<Outline>();
            border.effectColor = new Color(1f, 1f, 1f, 0.14f);
            border.effectDistance = new Vector2(1.5f, -1.5f);

            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(14, 14, 12, 12);
            layout.spacing = 10f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _instance._panel = panel;
            panel.gameObject.SetActive(false);
        }

        public static void Show(Object owner, RectTransform target, IReadOnlyList<Entry> entries)
        {
            if (target == null || entries == null || entries.Count == 0)
            {
                Hide(owner);
                return;
            }

            var tooltip = Instance;
            tooltip._owner = owner;

            foreach (var row in tooltip._rows)
            {
                if (row == null) continue;
                row.transform.SetParent(null, false);   // out of the layout now; destroyed at frame end
                Destroy(row);
            }
            tooltip._rows.Clear();

            foreach (var entry in entries)
                tooltip._rows.Add(Row(tooltip._panel, entry));

            tooltip._panel.gameObject.SetActive(true);
            tooltip._panel.SetAsLastSibling();
            LayoutRebuilder.ForceRebuildLayoutImmediate(tooltip._panel);
            tooltip.Place(target);
        }

        public static void Hide(Object owner = null)
        {
            if (_instance == null) return;
            if (owner != null && _instance._owner != owner) return;
            _instance._owner = null;
            _instance._panel.gameObject.SetActive(false);
        }

        /// <summary>True while a tooltip is up — for the capture harness.</summary>
        public static bool IsShowing => _instance != null && _instance._panel.gameObject.activeSelf;

        static GameObject Row(Transform parent, Entry entry)
        {
            var row = new GameObject("Entry", typeof(RectTransform));
            row.transform.SetParent(parent, false);

            var horizontal = row.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 10f;
            horizontal.childAlignment = TextAnchor.UpperLeft;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = false;

            bool hasIcon = Icons.Get(entry.Icon) != null;
            if (hasIcon) Icons.Create(row.transform, "Icon", Icons.Get(entry.Icon), IconSize);

            var column = new GameObject("Text", typeof(RectTransform));
            column.transform.SetParent(row.transform, false);
            var vertical = column.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = 2f;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;
            var columnLayout = column.AddComponent<LayoutElement>();
            columnLayout.preferredWidth = Width - 28f - (hasIcon ? IconSize + 10f : 0f);

            var title = UiFactory.Label(column.transform, "Title", entry.Title, 20, entry.TitleColor, TextAnchor.UpperLeft);
            title.fontStyle = FontStyle.Bold;

            if (!string.IsNullOrEmpty(entry.Body))
                UiFactory.Label(column.transform, "Body", Keywords.Highlight(entry.Body), 17, Palette.InkMuted, TextAnchor.UpperLeft);

            return row;
        }

        /// <summary>
        /// Beside the target, top edges aligned: to the right when there is room, to the left when
        /// not, and always on screen. Both canvases are screen-space overlays, so world positions
        /// are screen pixels and no camera conversion is involved.
        /// </summary>
        void Place(RectTransform target)
        {
            var corners = new Vector3[4];   // bottom-left, top-left, top-right, bottom-right
            target.GetWorldCorners(corners);

            float scale = _canvas.scaleFactor;
            Vector2 size = _panel.rect.size * scale;
            float gap = Gap * scale;
            const float margin = 8f;

            float x = corners[2].x + gap;
            if (x + size.x > Screen.width - margin) x = corners[1].x - gap - size.x;
            x = Mathf.Clamp(x, margin, Mathf.Max(margin, Screen.width - size.x - margin));

            float y = Mathf.Min(corners[1].y, Screen.height - margin);
            y = Mathf.Max(y, size.y + margin);

            _panel.position = new Vector3(x, y, 0f);
        }
    }
}
