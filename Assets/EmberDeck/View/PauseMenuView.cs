using System;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// The in-run menu, opened with Escape.
    ///
    /// It states what leaving costs, because the answer is not obvious: the run is written to disk
    /// each time the map is shown, so leaving from a fight, a reward or a rest site returns the
    /// player to their last visit to the map. Saying so here is cheaper than a mid-combat save, and
    /// far better than a player discovering it by losing a fight they thought was kept.
    /// </summary>
    public sealed class PauseMenuView : MonoBehaviour
    {
        public event Action Resumed;
        public event Action SettingsChosen;
        public event Action MainMenuChosen;
        public event Action QuitChosen;

        public bool IsOpen => gameObject.activeSelf;

        public static PauseMenuView Create(Transform parent)
        {
            var root = UiFactory.Panel(parent, "PauseMenu", new Color(0f, 0f, 0f, 0.72f));
            UiFactory.Stretch(root);
            var view = root.gameObject.AddComponent<PauseMenuView>();
            view.Build(root);
            return view;
        }

        void Build(RectTransform root)
        {
            var card = UiFactory.Panel(root, "Card", Palette.PanelDark);
            UiFactory.Frame(card.GetComponent<Image>(), "frame_panel", 5f);
            UiFactory.Place(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(560f, 600f));

            var title = UiFactory.Label(card, "PauseTitle", "PAUSED", 44, Palette.Ink);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -40f), new Vector2(480f, 60f));

            var note = UiFactory.Label(card, "PauseNote",
                                       "The run is saved each time you reach the map. Leaving now returns you to your last visit there.",
                                       19, Palette.InkMuted);
            UiFactory.Place(note.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -110f), new Vector2(460f, 60f));

            NavHint.On(MenuButton(card, "Resume", "Resume", -200f, () => Resumed?.Invoke())).Cancel = true;
            MenuButton(card, "PauseSettings", "Settings", -290f, () => SettingsChosen?.Invoke());
            MenuButton(card, "MainMenu", "Main Menu", -380f, () => MainMenuChosen?.Invoke());
            MenuButton(card, "PauseQuit", "Quit", -470f, () => QuitChosen?.Invoke());

            gameObject.SetActive(false);
        }

        static Button MenuButton(Transform parent, string name, string text, float y, Action onClick)
        {
            var button = UiFactory.TextButton(parent, name, text, Palette.PanelRaised, Palette.Ink, 28);
            UiFactory.Place((RectTransform)button.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, y), new Vector2(380f, 70f));
            button.onClick.AddListener(() => onClick());
            return button;
        }

        public void Show()
        {
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide() => gameObject.SetActive(false);
    }
}
