using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace EmberDeck.View
{
    /// <summary>
    /// A small tween runner for the UI.
    ///
    /// Written rather than imported: DOTween would be the project's first third-party asset, and
    /// the game needs about six kinds of motion — move, punch, shake, flash, float and delay.
    /// Everything here is view-only. It never touches combat state, so the rules and the
    /// simulator behave identically whether or not anything animates.
    ///
    /// Tweens are keyed by owner and channel. Starting a tween on a channel that is already
    /// running replaces it, so three hits landing in one frame restart a single shake rather
    /// than stacking three that fight over the same position.
    /// </summary>
    public sealed class Motion : MonoBehaviour
    {
        sealed class Tween
        {
            public Object Owner;
            public bool HasOwner;
            public string Channel;
            public float Delay;
            public float Duration;
            public float Elapsed;
            public Func<float, float> Ease;
            public Action<float> Step;
            public Action Begin;
            public Action Done;
            public bool Started;
        }

        static Motion _instance;
        static readonly Dictionary<RectTransform, Vector2> ShakeOrigins = new();

        readonly List<Tween> _tweens = new();
        readonly List<Tween> _frame = new();

        static Motion Instance
        {
            get
            {
                if (_instance != null) return _instance;
                var host = new GameObject("[Motion]");
                DontDestroyOnLoad(host);
                _instance = host.AddComponent<Motion>();
                return _instance;
            }
        }

        // ── Core ─────────────────────────────────────────────────────────────────────

        public static void Run(Object owner, string channel, float duration, Action<float> step,
                               Func<float, float> ease = null, float delay = 0f,
                               Action done = null, Action begin = null)
        {
            var tweens = Instance._tweens;
            if (channel != null)
                for (int i = tweens.Count - 1; i >= 0; i--)
                    if (tweens[i].HasOwner && tweens[i].Owner == owner && tweens[i].Channel == channel)
                        tweens.RemoveAt(i);

            tweens.Add(new Tween
            {
                Owner = owner,
                HasOwner = owner != null,
                Channel = channel,
                Duration = Mathf.Max(0.0001f, duration),
                Delay = delay,
                Ease = ease ?? OutCubic,
                Step = step,
                Begin = begin,
                Done = done,
            });
        }

        /// <summary>Runs an action after a delay. With an owner, it is dropped if the owner is destroyed first.</summary>
        public static void After(float delay, Action action, Object owner = null) =>
            Run(owner, null, 0.0001f, null, Linear, delay, action);

        void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _frame.Clear();
            _frame.AddRange(_tweens);

            foreach (var tween in _frame)
            {
                if (!_tweens.Contains(tween)) continue;   // replaced earlier this frame

                if (tween.HasOwner && tween.Owner == null)
                {
                    _tweens.Remove(tween);
                    if (tween.Owner is RectTransform) ShakeOrigins.Remove((RectTransform)tween.Owner);
                    continue;
                }

                if (tween.Delay > 0f)
                {
                    tween.Delay -= dt;
                    if (tween.Delay > 0f) continue;
                }

                try
                {
                    if (!tween.Started)
                    {
                        tween.Started = true;
                        tween.Begin?.Invoke();
                    }

                    tween.Elapsed += dt;
                    float t = Mathf.Clamp01(tween.Elapsed / tween.Duration);
                    tween.Step?.Invoke(tween.Ease(t));
                    if (t < 1f) continue;

                    _tweens.Remove(tween);
                    tween.Done?.Invoke();
                }
                catch (Exception e)
                {
                    // A view destroyed mid-tween must not take the whole runner down with it.
                    _tweens.Remove(tween);
                    Debug.LogException(e);
                }
            }
        }

        // ── Easing ───────────────────────────────────────────────────────────────────

        public static float Linear(float t) => t;

        public static float OutCubic(float t)
        {
            float u = 1f - t;
            return 1f - u * u * u;
        }

        public static float InCubic(float t) => t * t * t;

        public static float OutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }

        // ── Effects ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Shakes a rect around its resting position. The resting position is remembered until
        /// the shake finishes, so a second hit during the first shake cannot bake an offset in.
        /// </summary>
        public static void Shake(RectTransform rect, float strength, float duration = 0.32f, float delay = 0f)
        {
            if (rect == null) return;
            if (Settings.ReducedMotion) return;
            if (!ShakeOrigins.TryGetValue(rect, out var origin))
            {
                origin = rect.anchoredPosition;
                ShakeOrigins[rect] = origin;
            }

            float seed = UnityEngine.Random.value * 100f;
            Run(rect, "shake", duration, t =>
            {
                float fall = (1f - t) * (1f - t);
                float x = (Mathf.PerlinNoise(seed, t * 18f) - 0.5f) * 2f;
                float y = (Mathf.PerlinNoise(seed + 7.3f, t * 18f) - 0.5f) * 2f;
                rect.anchoredPosition = origin + new Vector2(x, y) * strength * fall;
            }, Linear, delay, () =>
            {
                if (rect != null) rect.anchoredPosition = origin;
                ShakeOrigins.Remove(rect);
            });
        }

        /// <summary>A quick scale bump. Only for objects nothing else scales.</summary>
        public static void Punch(Transform target, float amount = 0.12f, float duration = 0.28f, float delay = 0f)
        {
            if (target == null) return;
            Run(target, "punch", duration,
                t => target.localScale = Vector3.one * (1f + amount * Mathf.Sin(t * Mathf.PI) * (1f - 0.5f * t)),
                Linear, delay,
                () => { if (target != null) target.localScale = Vector3.one; });
        }

        /// <summary>Shows a colour over a graphic and fades it out.</summary>
        public static void Flash(Graphic graphic, Color color, float duration = 0.3f, float delay = 0f)
        {
            if (graphic == null) return;
            Run(graphic, "flash", duration, t =>
            {
                var c = color;
                c.a = color.a * (1f - t);
                graphic.color = c;
            }, OutCubic, delay, () =>
            {
                if (graphic == null) return;
                var c = color;
                c.a = 0f;
                graphic.color = c;
            });
        }

        /// <summary>
        /// A number that pops, rises and fades — damage, block, statuses. Placed on a layer above
        /// the board so it is never hidden behind the card or enemy it belongs to.
        /// </summary>
        public static void FloatText(RectTransform layer, Vector2 position, string text, Color color,
                                     int size = 40, float delay = 0f, float rise = 80f)
        {
            if (layer == null) return;

            var label = UiFactory.Label(layer, "Float", text, size, color);
            label.raycastTarget = false;
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);

            var rect = label.rectTransform;
            UiFactory.Place(rect, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), position,
                            new Vector2(360f, size * 1.6f));
            var hidden = color;
            hidden.a = 0f;
            label.color = hidden;

            float drift = UnityEngine.Random.Range(-18f, 18f);
            Run(label, null, 0.95f, t =>
            {
                rect.anchoredPosition = position + new Vector2(drift * t, rise * OutCubic(t));
                float pop = t < 0.18f ? OutBack(t / 0.18f) : 1f;
                rect.localScale = Vector3.one * Mathf.LerpUnclamped(0.5f, 1f, pop);
                var c = color;
                c.a = t < 0.6f ? 1f : 1f - (t - 0.6f) / 0.4f;
                label.color = c;
            }, Linear, delay, () => { if (label != null) Destroy(label.gameObject); });
        }

        /// <summary>The centre of <paramref name="target"/>, as an anchored position on a centre-anchored child of <paramref name="layer"/>.</summary>
        public static Vector2 PointIn(RectTransform layer, RectTransform target, Vector2 offset = default)
        {
            var world = target.TransformPoint(target.rect.center);
            Vector2 local = layer.InverseTransformPoint(world);
            return local - layer.rect.center + offset;
        }
    }
}
