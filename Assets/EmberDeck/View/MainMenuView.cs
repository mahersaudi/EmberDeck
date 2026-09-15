using System;
using System.Collections.Generic;
using EmberDeck.Content;
using EmberDeck.Run;
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

        RunConfig _config;
        Text _embersValue;
        Image _embersFill;
        Text _nextUnlock;
        Text _unlockList;
        RectTransform _difficultyRow;
        Text _difficultyValue;
        Text _difficultyHint;

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
            NavHint.On(_continue).Priority = 10;
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

            BuildProgress(root);

            var footer = UiFactory.Label(root, "Footer", "M  mute        Esc  pause menu", 18, Palette.InkMuted,
                                         TextAnchor.LowerLeft);
            UiFactory.Place(footer.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                            new Vector2(40f, 30f), new Vector2(600f, 30f));

            gameObject.SetActive(false);
        }

        /// <summary>
        /// Between the buttons and the boss portrait: Embers earned, the next unlock, the whole track, and
        /// the difficulty once there is more than one. Progress is the reason for one more run, so it sits
        /// where the next run is started rather than on a screen of its own.
        /// </summary>
        void BuildProgress(RectTransform root)
        {
            var panel = UiFactory.Panel(root, "Progress", Palette.PanelDark);
            UiFactory.Frame(panel.GetComponent<Image>(), "frame_panel", 4f);
            UiFactory.Place(panel, new Vector2(0f, 0.5f), new Vector2(0f, 1f), new Vector2(520f, 10f), new Vector2(340f, 330f));

            var title = UiFactory.Label(panel, "EmbersTitle", "EMBERS", 20, Palette.Energy, TextAnchor.MiddleLeft);
            UiFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(150f, 34f));

            _embersValue = UiFactory.Label(panel, "EmbersValue", "0", 30, Palette.Ink, TextAnchor.MiddleRight);
            UiFactory.Place(_embersValue.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-18f, -10f), new Vector2(150f, 38f));

            _embersFill = UiFactory.Bar(panel, "EmbersBar", Palette.HeatTrack, Palette.Energy, new Vector2(304f, 10f), new Vector2(0f, 110f));

            _nextUnlock = UiFactory.Label(panel, "NextUnlock", "", 17, Palette.InkMuted, TextAnchor.MiddleLeft);
            UiFactory.Place(_nextUnlock.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -70f), new Vector2(304f, 26f));

            // A raycast target of its own, so it can carry the track's tooltip without the panel's.
            _unlockList = UiFactory.Label(panel, "UnlockList", "", 17, Palette.Ink, TextAnchor.UpperLeft);
            _unlockList.lineSpacing = 1.15f;
            _unlockList.raycastTarget = true;
            UiFactory.Place(_unlockList.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -104f), new Vector2(304f, 150f));
            TooltipTrigger.Attach(_unlockList.gameObject, DescribeUnlocks);

            _difficultyHint = UiFactory.Label(panel, "DifficultyHint", "Win a run to unlock Difficulty 1", 16, Palette.InkMuted, TextAnchor.MiddleLeft);
            UiFactory.Place(_difficultyHint.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 14f), new Vector2(304f, 40f));

            _difficultyRow = (RectTransform)new GameObject("DifficultyRow", typeof(RectTransform)).transform;
            _difficultyRow.SetParent(panel, false);
            UiFactory.Place(_difficultyRow, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(18f, 14f), new Vector2(304f, 40f));

            var label = UiFactory.Label(_difficultyRow, "DifficultyLabel", "Difficulty", 19, Palette.Ink, TextAnchor.MiddleLeft);
            UiFactory.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(110f, 40f));

            var previous = UiFactory.TextButton(_difficultyRow, "DifficultyPrevious", "<", Palette.PanelRaised, Palette.Ink, 22);
            UiFactory.Place((RectTransform)previous.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(112f, 0f), new Vector2(40f, 38f));
            previous.onClick.AddListener(() => StepDifficulty(-1));

            _difficultyValue = UiFactory.Label(_difficultyRow, "DifficultyValue", "", 19, Palette.Energy);
            _difficultyValue.raycastTarget = true;
            UiFactory.Place(_difficultyValue.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(156f, 0f), new Vector2(100f, 38f));
            TooltipTrigger.Attach(_difficultyValue.gameObject, DescribeDifficulty);

            var next = UiFactory.TextButton(_difficultyRow, "DifficultyNext", ">", Palette.PanelRaised, Palette.Ink, 22);
            UiFactory.Place((RectTransform)next.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(260f, 0f), new Vector2(40f, 38f));
            next.onClick.AddListener(() => StepDifficulty(+1));
        }

        /// <summary>Refreshes the progress panel from the profile. Call before Show.</summary>
        public void SetProgress(RunConfig config)
        {
            _config = config;
            var data = Profile.Data;
            _embersValue.text = data.embers.ToString();

            var nextUnlock = config != null ? UnlockService.Next(config, data.embers) : null;
            int from = config != null ? UnlockService.PreviousThreshold(config, data.embers) : 0;
            float fill = nextUnlock == null ? 1f : Mathf.Clamp01((data.embers - from) / (float)Mathf.Max(1, nextUnlock.Threshold - from));
            UiFactory.SetBarFill(_embersFill, fill);
            _nextUnlock.text = nextUnlock == null ? "Everything is unlocked" : $"Next: {nextUnlock.DisplayName} at {nextUnlock.Threshold}";

            var lines = new List<string>();
            string muted = ColorUtility.ToHtmlStringRGB(Palette.InkMuted);
            if (config != null)
                foreach (var unlock in config.Unlocks)
                {
                    if (unlock == null) continue;
                    lines.Add(data.embers >= unlock.Threshold
                        ? $"+  {unlock.DisplayName}"
                        : $"<color=#{muted}>{unlock.Threshold,3}  {unlock.DisplayName}</color>");
                }
            _unlockList.text = string.Join("\n", lines);

            _difficultyRow.gameObject.SetActive(data.maxDifficulty > 0);
            _difficultyHint.gameObject.SetActive(data.maxDifficulty == 0);
            RefreshDifficulty();
        }

        void StepDifficulty(int step)
        {
            Profile.SetDifficulty(Profile.Data.difficulty + step);
            RefreshDifficulty();
        }

        void RefreshDifficulty()
        {
            int level = Profile.Data.difficulty;
            _difficultyValue.text = level == 0 ? "Normal" : level.ToString();
        }

        IReadOnlyList<Tooltip.Entry> DescribeUnlocks()
        {
            var entries = new List<Tooltip.Entry>();
            if (_config == null) return entries;
            int embers = Profile.Data.embers;
            foreach (var unlock in _config.Unlocks)
            {
                if (unlock == null) continue;
                bool open = embers >= unlock.Threshold;
                entries.Add(new Tooltip.Entry(open ? unlock.DisplayName : $"{unlock.DisplayName}  ({unlock.Threshold} Embers)",
                                              unlock.Describe(), null, open ? Palette.Energy : Palette.InkMuted));
            }
            entries.Add(new Tooltip.Entry("Earning Embers",
                                          $"{UnlockService.PerFloor} per floor reached, {UnlockService.PerElite} per elite, "
                                          + $"{UnlockService.PerBoss} per boss, and {UnlockService.ForWinning} more for winning. "
                                          + "Unlocked cards and relics join the reward pools of new runs."));
            return entries;
        }

        IReadOnlyList<Tooltip.Entry> DescribeDifficulty()
        {
            var data = Profile.Data;
            var rules = DifficultyRules.RulesAt(data.difficulty);
            string body = rules.Count == 0 ? "The game as designed." : string.Join("\n", rules);
            if (data.maxDifficulty < DifficultyRules.Max)
                body += $"\nWin at difficulty {data.maxDifficulty} to unlock difficulty {data.maxDifficulty + 1}.";
            return new[] { new Tooltip.Entry(DifficultyRules.Name(data.difficulty), body) };
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
