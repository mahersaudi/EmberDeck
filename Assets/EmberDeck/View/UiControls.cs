using System;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// Settings controls built from plain rectangles, like everything else in this UI.
    ///
    /// Unity's default controls need its built-in sprites, which are only reachable from editor
    /// code; a slider made of three coloured panels needs nothing. Options are steppers — a value
    /// between two arrows — rather than dropdowns: a code-built dropdown is a template, a scroll
    /// view and a mask, and a stepper is three objects that work the same with a mouse or arrow keys.
    /// </summary>
    public static class UiControls
    {
        public const float RowWidth = 760f;
        public const float RowHeight = 56f;
        const float LabelWidth = 220f;

        /// <summary>A labelled row, top-anchored at <paramref name="y"/> in its parent.</summary>
        public static RectTransform Row(Transform parent, string name, string label, float y)
        {
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            var rect = (RectTransform)row.transform;
            UiFactory.Place(rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, y),
                            new Vector2(RowWidth, RowHeight));

            var text = UiFactory.Label(rect, "Label", label, 24, Palette.Ink, TextAnchor.MiddleLeft);
            UiFactory.Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                            new Vector2(LabelWidth, RowHeight));
            return rect;
        }

        /// <summary>A 0–1 slider with a percentage readout on the right of the row.</summary>
        public static Slider VolumeSlider(RectTransform row, float value, Action<float> changed, out Text readout)
        {
            var track = UiFactory.Panel(row, "Track", Palette.PanelRaised);
            UiFactory.Place(track, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(LabelWidth + 20f, 0f),
                            new Vector2(400f, 14f));

            var fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(track, false);
            UiFactory.Stretch((RectTransform)fillArea.transform);
            var fill = UiFactory.Panel(fillArea.transform, "Fill", Palette.Energy);
            fill.GetComponent<Image>().raycastTarget = false;
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0f, 1f);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;

            var handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(track, false);
            UiFactory.Stretch((RectTransform)handleArea.transform);
            var handle = UiFactory.Panel(handleArea.transform, "Handle", Palette.Ink);
            // The slider stretches the handle to the track's height; this adds 20 above and below.
            handle.sizeDelta = new Vector2(20f, 20f);

            var slider = track.gameObject.AddComponent<Slider>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.SetValueWithoutNotify(value);

            var text = UiFactory.Label(row, "Readout", Percent(value), 22, Palette.InkMuted, TextAnchor.MiddleRight);
            UiFactory.Place(text.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero,
                            new Vector2(90f, RowHeight));
            readout = text;

            slider.onValueChanged.AddListener(v =>
            {
                text.text = Percent(v);
                changed?.Invoke(v);
            });
            return slider;
        }

        public static string Percent(float value) => $"{Mathf.RoundToInt(value * 100f)}%";

        public sealed class Stepper
        {
            readonly Func<string> _read;
            public Text Value;
            public Button Previous;
            public Button Next;

            public Stepper(Func<string> read) => _read = read;

            public void Refresh() => Value.text = _read();
        }

        /// <summary>A value between two arrows; each arrow calls <paramref name="step"/> with -1 or +1.</summary>
        public static Stepper AddStepper(RectTransform row, string name, Func<string> read, Action<int> step)
        {
            var stepper = new Stepper(read);

            stepper.Previous = UiFactory.TextButton(row, name + "Previous", "<", Palette.PanelRaised, Palette.Ink, 26);
            UiFactory.Place((RectTransform)stepper.Previous.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(LabelWidth + 20f, 0f), new Vector2(52f, 46f));

            stepper.Value = UiFactory.Label(row, "Value", "", 23, Palette.Ink);
            UiFactory.Place(stepper.Value.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(LabelWidth + 80f, 0f), new Vector2(284f, 46f));

            stepper.Next = UiFactory.TextButton(row, name + "Next", ">", Palette.PanelRaised, Palette.Ink, 26);
            UiFactory.Place((RectTransform)stepper.Next.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(LabelWidth + 372f, 0f), new Vector2(52f, 46f));

            stepper.Previous.onClick.AddListener(() => { step(-1); stepper.Refresh(); });
            stepper.Next.onClick.AddListener(() => { step(+1); stepper.Refresh(); });
            stepper.Refresh();
            return stepper;
        }
    }
}
