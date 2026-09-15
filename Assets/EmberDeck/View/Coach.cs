using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// First-run tips: one short note at a time, pointing at the thing it explains, shown the first
    /// time that thing matters and never again.
    ///
    /// Tips rather than a scripted tutorial fight. A scripted fight teaches a deck the player will
    /// never hold, and everyone who already knows the genre has to sit through it or find the skip;
    /// a tip that appears when Heat first rises explains Heat to the player who needs it, in their
    /// own run, and costs a veteran one click. A tip also counts as done when the player does what it
    /// describes, so someone who plays a card before reading "click a card" is not told to anyway.
    ///
    /// Seen tips live in PlayerPrefs, like Settings: they belong to the player, not to a run, and
    /// abandoning a run must not replay them.
    /// </summary>
    public sealed class Coach : MonoBehaviour
    {
        public enum Side { Above, Below, Left, Right }

        sealed class Tip
        {
            public string Id;
            public string Title;
            public string Body;
            public Func<IEnumerable<RectTransform>> Targets;
            public Side Side;
        }

        const string SeenKey = "emberdeck.tutorial.seen";
        const string EnabledKey = "emberdeck.tutorial.enabled";
        const float Width = 420f;
        const float Gap = 22f;
        const float Padding = 10f;
        const float EdgeThickness = 4f;

        static Coach _instance;
        static HashSet<string> _seen;
        static bool _enabled = true;
        static bool _persist = true;

        /// <summary>While this returns true the tip is hidden without being dismissed: menus, pause, settings.</summary>
        public static Func<bool> Suspended;

        Canvas _canvas;
        CanvasGroup _group;
        RectTransform _bubble;
        Text _title;
        Text _body;
        readonly Image[] _edges = new Image[4];   // bottom, top, left, right
        readonly List<Tip> _queue = new();
        Tip _current;

        public static bool Enabled
        {
            get
            {
                EnsureLoaded();
                return _enabled;
            }
        }

        /// <summary>The tip bubble while one is on screen and not hidden behind a menu, for PadNavigator.</summary>
        public static Transform NavScope =>
            _instance != null && _instance._current != null && _instance._bubble.gameObject.activeInHierarchy && _instance._group.alpha > 0.5f
                ? _instance._bubble
                : null;

        /// <summary>The tip on screen, or null — for the capture harness.</summary>
        public static string CurrentId => _instance != null ? _instance._current?.Id : null;

        /// <summary>
        /// Turns tips on or off. Turning them on starts them over, which is what someone looking for
        /// that switch wants: they are either handing the game to a new player or want a refresher.
        /// </summary>
        public static void SetEnabled(bool on)
        {
            EnsureLoaded();
            _enabled = on;
            if (on) _seen.Clear();
            Save();
            if (!on && _instance != null) _instance.Clear(markSeen: false);
        }

        /// <summary>
        /// Capture-harness use: every tip unseen, and nothing written to PlayerPrefs, so a capture run
        /// neither depends on nor changes what the person at this machine has already seen.
        /// </summary>
        public static void UseMemoryOnly()
        {
            _persist = false;
            _seen = new HashSet<string>();
            _enabled = true;
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        /// <summary>
        /// Capture-harness only: marks one tip unseen again. The harness presses End Turn before it runs
        /// out of Energy, which rightly completes the end-turn tip early, so it has to be forgotten to be
        /// photographed.
        /// </summary>
        public static void Forget(string id)
        {
            EnsureLoaded();
            _seen.Remove(id);
        }
#endif

        /// <summary>
        /// Queues a tip unless it was seen already. Targets are read every frame rather than once, so
        /// the highlight follows cards as they deal in and move; an empty target list centres the tip.
        /// </summary>
        public static void Show(string id, string title, string body, Func<IEnumerable<RectTransform>> targets, Side side)
        {
            EnsureLoaded();
            if (!_enabled || _seen.Contains(id)) return;

            var coach = Instance;
            if (coach._current?.Id == id || coach._queue.Exists(t => t.Id == id)) return;

            coach._queue.Add(new Tip { Id = id, Title = title, Body = body, Targets = targets, Side = side });
            if (coach._current == null) coach.Next();
        }

        /// <summary>The player did what the tip describes: never show it, and take it down if it is up.</summary>
        public static void Complete(string id)
        {
            EnsureLoaded();
            if (_seen.Add(id)) Save();
            if (_instance == null) return;

            _instance._queue.RemoveAll(t => t.Id == id);
            if (_instance._current?.Id == id) _instance.Next();
        }

        /// <summary>
        /// The screen changed. A tip that was on screen counts as read; queued tips are dropped unseen,
        /// so they can still appear the next time what they explain comes up.
        /// </summary>
        public static void EndScreen()
        {
            if (_instance != null) _instance.Clear(markSeen: true);
        }

        static void EnsureLoaded()
        {
            if (_seen != null) return;
            _seen = new HashSet<string>();
            if (!_persist) return;

            foreach (var id in PlayerPrefs.GetString(SeenKey, "").Split(','))
                if (id.Length > 0) _seen.Add(id);
            _enabled = PlayerPrefs.GetInt(EnabledKey, 1) == 1;
        }

        static void Save()
        {
            if (!_persist) return;
            PlayerPrefs.SetString(SeenKey, string.Join(",", _seen));
            PlayerPrefs.SetInt(EnabledKey, _enabled ? 1 : 0);
            PlayerPrefs.Save();
        }

        static Coach Instance
        {
            get
            {
                if (_instance == null) Create();
                return _instance;
            }
        }

        // ── Building ─────────────────────────────────────────────────────────────────

        /// <summary>
        /// Its own canvas, above the game and below the tooltip, so a tip can point at anything on any
        /// screen and a tooltip opened while reading one still draws on top.
        /// </summary>
        static void Create()
        {
            var host = new GameObject("[Coach]", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            DontDestroyOnLoad(host);

            var canvas = host.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 400;

            var scaler = host.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _instance = host.AddComponent<Coach>();
            _instance._canvas = canvas;
            _instance._group = host.GetComponent<CanvasGroup>();
            _instance.Build(host.transform);
        }

        void Build(Transform host)
        {
            for (int i = 0; i < _edges.Length; i++)
            {
                var edge = UiFactory.Panel(host, $"CoachEdge{i}", Palette.Energy);
                edge.pivot = Vector2.zero;
                _edges[i] = edge.GetComponent<Image>();
                _edges[i].raycastTarget = false;
                edge.gameObject.SetActive(false);
            }

            _bubble = UiFactory.Panel(host, "CoachBubble", new Color(0.09f, 0.085f, 0.11f, 0.98f));
            UiFactory.Frame(_bubble.GetComponent<Image>(), "frame_panel", 4f);
            _bubble.pivot = new Vector2(0f, 1f);
            _bubble.sizeDelta = new Vector2(Width, 10f);

            var accent = UiFactory.Panel(_bubble, "Accent", Palette.Energy);
            accent.gameObject.AddComponent<LayoutElement>().ignoreLayout = true;
            accent.anchorMin = new Vector2(0f, 1f);
            accent.anchorMax = new Vector2(1f, 1f);
            accent.pivot = new Vector2(0.5f, 1f);
            accent.offsetMin = new Vector2(4f, -8f);
            accent.offsetMax = new Vector2(-4f, -4f);
            accent.GetComponent<Image>().raycastTarget = false;

            var layout = _bubble.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 20, 16);
            layout.spacing = 8f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;

            var fitter = _bubble.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _title = UiFactory.Label(_bubble, "CoachTitle", "", 23, Palette.Energy, TextAnchor.UpperLeft);
            _title.fontStyle = FontStyle.Bold;

            _body = UiFactory.Label(_bubble, "CoachBody", "", 19, Palette.Ink, TextAnchor.UpperLeft);
            _body.lineSpacing = 1.1f;

            var row = new GameObject("CoachButtons", typeof(RectTransform));
            row.transform.SetParent(_bubble, false);
            var horizontal = row.AddComponent<HorizontalLayoutGroup>();
            horizontal.spacing = 10f;
            horizontal.childAlignment = TextAnchor.MiddleRight;
            horizontal.childControlWidth = true;
            horizontal.childControlHeight = true;
            horizontal.childForceExpandWidth = false;
            horizontal.childForceExpandHeight = false;
            row.AddComponent<LayoutElement>().preferredHeight = 48f;

            var skip = UiFactory.TextButton(row.transform, "CoachSkip", "Skip tutorial", Palette.PanelDark, Palette.InkMuted, 18);
            Size(skip, 150f, 42f);
            skip.onClick.AddListener(() => SetEnabled(false));

            var gotIt = UiFactory.TextButton(row.transform, "CoachGotIt", "Got it", Palette.PanelRaised, Palette.Ink, 21);
            Size(gotIt, 130f, 42f);
            gotIt.onClick.AddListener(Dismiss);
            var gotItHint = NavHint.On(gotIt);
            gotItHint.Priority = 10;
            gotItHint.Cancel = true;

            _bubble.gameObject.SetActive(false);
        }

        static void Size(Button button, float width, float height)
        {
            var element = button.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;
        }

        // ── Flow ─────────────────────────────────────────────────────────────────────

        void Dismiss()
        {
            if (_current == null) return;
            if (_seen.Add(_current.Id)) Save();
            Next();
        }

        void Next()
        {
            _current = null;
            while (_queue.Count > 0)
            {
                var candidate = _queue[0];
                _queue.RemoveAt(0);
                if (_seen.Contains(candidate.Id)) continue;
                _current = candidate;
                break;
            }

            if (_current == null)
            {
                _bubble.gameObject.SetActive(false);
                SetEdges(false);
                return;
            }

            _title.text = _current.Title;
            _body.text = Keywords.Highlight(_current.Body);
            _bubble.gameObject.SetActive(true);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_bubble);
            Follow();
            Motion.Punch(_bubble, 0.04f, 0.25f);
        }

        void Clear(bool markSeen)
        {
            if (markSeen && _current != null && _seen.Add(_current.Id)) Save();
            _queue.Clear();
            _current = null;
            _bubble.gameObject.SetActive(false);
            SetEdges(false);
        }

        void LateUpdate()
        {
            if (_current == null) return;

            bool hidden = Suspended != null && Suspended();
            _group.alpha = hidden ? 0f : 1f;
            _group.blocksRaycasts = !hidden;
            if (hidden) return;

            Follow();

            float pulse = 0.55f + 0.45f * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f));
            foreach (var edge in _edges)
            {
                var color = edge.color;
                color.a = pulse;
                edge.color = color;
            }
        }

        // ── Placement ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Rings the targets and sets the tip beside them on its side, kept on screen. Every canvas
        /// here is a screen-space overlay, so world positions are screen pixels on all of them.
        /// </summary>
        void Follow()
        {
            float scale = _canvas.scaleFactor;
            var box = Bounds(_current.Targets?.Invoke());

            if (box == null)
            {
                SetEdges(false);
                Place(new Rect(Screen.width * 0.5f, Screen.height * 0.5f, 0f, 0f), Side.Below, scale, centred: true);
                return;
            }

            float pad = Padding * scale;
            var ring = new Rect(box.Value.xMin - pad, box.Value.yMin - pad, box.Value.width + pad * 2f, box.Value.height + pad * 2f);
            float t = EdgeThickness * scale;
            SetEdge(0, ring.xMin, ring.yMin - t, ring.width, t, scale);
            SetEdge(1, ring.xMin, ring.yMax, ring.width, t, scale);
            SetEdge(2, ring.xMin - t, ring.yMin - t, t, ring.height + t * 2f, scale);
            SetEdge(3, ring.xMax, ring.yMin - t, t, ring.height + t * 2f, scale);
            SetEdges(true);

            Place(ring, _current.Side, scale, centred: false);
        }

        void Place(Rect target, Side side, float scale, bool centred)
        {
            Vector2 size = _bubble.rect.size * scale;
            float gap = Gap * scale;
            const float margin = 12f;

            float x, y;   // the bubble's top-left corner
            if (centred)
            {
                x = target.center.x - size.x * 0.5f;
                y = target.center.y + size.y * 0.5f;
            }
            else switch (side)
            {
                case Side.Above: x = target.center.x - size.x * 0.5f; y = target.yMax + gap + size.y; break;
                case Side.Below: x = target.center.x - size.x * 0.5f; y = target.yMin - gap; break;
                case Side.Left:  x = target.xMin - gap - size.x; y = target.center.y + size.y * 0.5f; break;
                default:         x = target.xMax + gap; y = target.center.y + size.y * 0.5f; break;
            }

            x = Mathf.Clamp(x, margin, Mathf.Max(margin, Screen.width - size.x - margin));
            y = Mathf.Clamp(y, Mathf.Min(size.y + margin, Screen.height - margin), Screen.height - margin);
            _bubble.position = new Vector3(x, y, 0f);
        }

        static Rect? Bounds(IEnumerable<RectTransform> targets)
        {
            if (targets == null) return null;

            var corners = new Vector3[4];
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            bool any = false;
            foreach (var target in targets)
            {
                if (target == null || !target.gameObject.activeInHierarchy) continue;
                target.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    xMin = Mathf.Min(xMin, corner.x);
                    yMin = Mathf.Min(yMin, corner.y);
                    xMax = Mathf.Max(xMax, corner.x);
                    yMax = Mathf.Max(yMax, corner.y);
                }
                any = true;
            }
            return any ? Rect.MinMaxRect(xMin, yMin, xMax, yMax) : null;
        }

        void SetEdge(int index, float x, float y, float width, float height, float scale)
        {
            var rect = _edges[index].rectTransform;
            rect.position = new Vector3(x, y, 0f);
            rect.sizeDelta = new Vector2(width / scale, height / scale);
        }

        void SetEdges(bool on)
        {
            foreach (var edge in _edges)
                if (edge.gameObject.activeSelf != on) edge.gameObject.SetActive(on);
        }
    }
}
