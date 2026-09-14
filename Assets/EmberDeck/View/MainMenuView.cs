using System;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// The first screen: continue, start over, settings, quit.
    ///
    /// The game used to open straight into the map, silently resuming whatever run was saved. That
    /// is fine for a developer and wrong for a player, who needs to choose between the run they left
    /// and a fresh one — and needs to be told what the saved run is before choosing.
    ///
    /// Starting a new run over a saved one asks for a second click. Abandoning a run is the one
    /// irreversible thing on this screen, and a single misclick should not cost an hour of progress.
    /// </summary>
    public sealed class MainMenuView : MonoBehaviour
    {
        public event Action ContinueChosen;
        public event Action NewRunChosen;
        public event Action SettingsChosen;
        public event Action QuitChosen;

        Button _continue;
        Text _continueDetail;
        Text _newRunLabel;
        bool _hasSave;
        bool _confirmingAbandon;

        public bool IsOpen => gameObject.activeSelf;

        public static MainMenuView Create(Transform parent, Sprite backdrop)
        {
            var root = UiFactory.Panel(parent, "MainMenu", Palette.Background);
            UiFactory.Stretch(root);
            var view = root.gameObject.AddComponent<MainMenuView>();
            view.Build(root, backdrop);
            return view;
        }

        void Build(RectTransform root, Sprite backdrop)
        {
            // The boss on the right: the run's destination, seen before the run begins. Alpha 0.3 reads
            // far stronger than it sounds, because UI blends in linear colour space — which suits it,
            // since the title sits clear of the portrait.
            if (backdrop != null)
            {
                var art = UiFactory.Panel(root, "Backdrop", new Color(1f, 1f, 1f, 0.3f));
                UiFactory.Place(art, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-60f, 0f),
                                new Vector2(980f, 980f));
                var image = art.GetComponent<Image>();
                image.sprite = backdrop;
                image.preserveAspect = true;
                image.raycastTarget = false;
            }

            var title = UiFactory.Label(root, "Title", "EMBERDECK", 108, Palette.Energy, TextAnchor.MiddleLeft);
            title.fontStyle = FontStyle.Bold;
            UiFactory.Place(title.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(150f, 270f), new Vector2(1000f, 130f));

            var subtitle = UiFactory.Label(root, "Subtitle", "A roguelike deckbuilder of fire and forge", 28,
                                           Palette.InkMuted, TextAnchor.MiddleLeft);
            UiFactory.Place(subtitle.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(156f, 190f), new Vector2(1000f, 40f));

            _continue = MenuButton(root, "Continue", "Continue", 60f, () => ContinueChosen?.Invoke());
            _continueDetail = UiFactory.Label(root, "ContinueDetail", "", 22, Palette.InkMuted, TextAnchor.MiddleLeft);
            UiFactory.Place(_continueDetail.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(490f, 60f), new Vector2(700f, 40f));

            var newRun = MenuButton(root, "NewRun", "New Run", -40f, OnNewRun);
            _newRunLabel = newRun.GetComponentInChildren<Text>();

            MenuButton(root, "Settings", "Settings", -140f, () =>
            {
                ResetConfirm();
                SettingsChosen?.Invoke();
            });
            MenuButton(root, "Quit", "Quit", -240f, () => QuitChosen?.Invoke());

            var footer = UiFactory.Label(root, "Footer", "M  mute        Esc  pause menu", 18, Palette.InkMuted,
                                         TextAnchor.LowerLeft);
            UiFactory.Place(footer.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                            new Vector2(40f, 30f), new Vector2(600f, 30f));

            gameObject.SetActive(false);
        }

        static Button MenuButton(Transform parent, string name, string text, float y, Action onClick)
        {
            var button = UiFactory.TextButton(parent, name, text, Palette.PanelRaised, Palette.Ink, 32);
            UiFactory.Place((RectTransform)button.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f),
                            new Vector2(150f, y), new Vector2(310f, 80f));
            button.onClick.AddListener(() => onClick());
            return button;
        }

        /// <param name="continueDetail">A one-line summary of the saved run, or null when there is none.</param>
        public void Show(string continueDetail)
        {
            _hasSave = continueDetail != null;
            _continue.gameObject.SetActive(_hasSave);
            _continueDetail.text = continueDetail ?? "";
            ResetConfirm();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide() => gameObject.SetActive(false);

        void OnNewRun()
        {
            if (_hasSave && !_confirmingAbandon)
            {
                _confirmingAbandon = true;
                _newRunLabel.text = "Abandon run?";
                _newRunLabel.color = Palette.Defeat;
                return;
            }
            ResetConfirm();
            NewRunChosen?.Invoke();
        }

        void ResetConfirm()
        {
            _confirmingAbandon = false;
            if (_newRunLabel == null) return;
            _newRunLabel.text = "New Run";
            _newRunLabel.color = Palette.Ink;
        }
    }
}
