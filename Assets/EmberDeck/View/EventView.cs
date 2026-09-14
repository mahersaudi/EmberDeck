using System;
using System.Collections.Generic;
using EmberDeck.Run;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// A "?" node: the scene, its choices, and what came of the one taken.
    ///
    /// Every choice shows exactly what it does before it is taken, and a choice that cannot be taken
    /// stays on screen with the reason beside it. Hiding an unaffordable option hides the fact that it
    /// existed, and "I could have had that with 10 more gold" is information the player should keep.
    /// </summary>
    public sealed class EventView : MonoBehaviour
    {
        const float ChoiceHeight = 88f;
        const float ChoiceGap = 14f;

        public event Action Left;

        Text _title;
        Text _body;
        RectTransform _choices;
        Text _outcome;
        Button _continue;
        readonly List<GameObject> _choiceObjects = new();

        RunEvent _event;
        EventContext _context;

        public bool IsOpen => gameObject.activeSelf;

        public static EventView Create(Transform parent)
        {
            var root = UiFactory.Panel(parent, "EventScreen", Palette.Background);
            UiFactory.Stretch(root);
            var view = root.gameObject.AddComponent<EventView>();
            view.Build(root);
            return view;
        }

        void Build(RectTransform root)
        {
            _title = UiFactory.Label(root, "EventTitle", "", 48, Palette.Energy);
            UiFactory.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(1200f, 64f));

            _body = UiFactory.Label(root, "EventBody", "", 26, Palette.Ink, TextAnchor.UpperCenter);
            _body.lineSpacing = 1.15f;
            UiFactory.Place(_body.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -220f), new Vector2(1000f, 150f));

            _choices = (RectTransform)new GameObject("Choices", typeof(RectTransform)).transform;
            _choices.SetParent(root, false);
            UiFactory.Place(_choices, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -420f), new Vector2(900f, 10f));

            _outcome = UiFactory.Label(root, "EventOutcome", "", 28, Palette.Victory);
            UiFactory.Place(_outcome.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -440f), new Vector2(1100f, 140f));

            _continue = UiFactory.TextButton(root, "EventContinue", "Continue", Palette.PanelRaised, Palette.Ink, 28);
            UiFactory.Place((RectTransform)_continue.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 100f), new Vector2(260f, 72f));
            _continue.onClick.AddListener(() =>
            {
                Hide();
                Left?.Invoke();
            });

            gameObject.SetActive(false);
        }

        public void Show(RunEvent evt, EventContext context)
        {
            _event = evt;
            _context = context;

            _title.text = evt.Title;
            _body.text = Keywords.Highlight(evt.Body);

            foreach (var choice in _choiceObjects)
                if (choice != null) { choice.transform.SetParent(null, false); Destroy(choice); }
            _choiceObjects.Clear();

            string blockedColor = ColorUtility.ToHtmlStringRGB(Palette.Defeat);
            for (int i = 0; i < evt.Choices.Count; i++)
            {
                var choice = evt.Choices[i];
                string blocked = choice.Blocked(context);

                var panel = UiFactory.Panel(_choices, $"EventChoice{i}", blocked == null ? Palette.PanelRaised : Palette.PanelDark);
                UiFactory.Place(panel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -i * (ChoiceHeight + ChoiceGap)),
                                new Vector2(900f, ChoiceHeight));

                var button = panel.gameObject.AddComponent<Button>();
                button.targetGraphic = panel.GetComponent<Image>();
                button.interactable = blocked == null;
                var captured = choice;
                button.onClick.AddListener(() => Choose(captured));

                var label = UiFactory.Label(panel, "Label", choice.Label, 26, blocked == null ? Palette.Ink : Palette.InkMuted,
                                            TextAnchor.MiddleLeft);
                label.fontStyle = FontStyle.Bold;
                UiFactory.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 16f), new Vector2(860f, 36f));

                string effect = Keywords.Highlight(choice.Effect);
                if (blocked != null) effect += $"   <color=#{blockedColor}>({blocked})</color>";
                var effectLabel = UiFactory.Label(panel, "Effect", effect, 20, Palette.InkMuted, TextAnchor.MiddleLeft);
                UiFactory.Place(effectLabel.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, -18f), new Vector2(860f, 30f));

                _choiceObjects.Add(panel.gameObject);
            }

            _choices.gameObject.SetActive(true);
            _outcome.gameObject.SetActive(false);
            _continue.gameObject.SetActive(false);
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide() => gameObject.SetActive(false);

        void Choose(EventChoice choice)
        {
            AudioDirector.Play(Sfx.Click);
            string outcome = EventService.Take(_event, choice, _context);
            if (outcome == null) return;

            _choices.gameObject.SetActive(false);
            _outcome.text = Keywords.Highlight(outcome);
            _outcome.gameObject.SetActive(true);
            _continue.gameObject.SetActive(true);
            Motion.Punch(_outcome.transform, 0.08f, 0.35f);
            AudioDirector.Play(Sfx.Reward, 0.7f);
        }
    }
}
