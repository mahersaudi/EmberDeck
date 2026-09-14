using System;
using System.Collections.Generic;
using EmberDeck.Content;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// The rest site: recover health, or work the forge to upgrade one card.
    ///
    /// A rest that only heals is not a decision — it is a free top-up the map hands out. Making
    /// it heal OR upgrade turns every rest into a question about the next few fights: survive
    /// them, or be stronger for them.
    ///
    /// The upgrade picker shows each card as it will be AFTER the upgrade. Showing the current
    /// card and making the player imagine the improvement asks them to do the comparison in
    /// their head, which is exactly the part the screen should do for them.
    /// </summary>
    public sealed class RestView : MonoBehaviour
    {
        const int PickerColumns = 6;
        // Full size while the choices fit on one row; the description text is what the player
        // is comparing, and at 72% it is hard to read. Only a second row needs the smaller size.
        const float OneRowScale = 1f;
        const float MultiRowScale = 0.72f;

        RectTransform _choices;
        RectTransform _picker;
        RectTransform _grid;
        Text _subtitle;
        Text _healLabel;
        Button _smithButton;

        readonly List<CardView> _pickerCards = new();
        List<CardData> _upgradable = new();

        public event Action HealChosen;
        public event Action<CardData> UpgradeChosen;

        public static RestView Create(Transform parent)
        {
            var root = UiFactory.Panel(parent, "RestScreen", new Color(0.05f, 0.05f, 0.07f, 1f));
            UiFactory.Stretch(root);

            var view = root.gameObject.AddComponent<RestView>();
            view.Build(root);
            return view;
        }

        void Build(RectTransform root)
        {
            var title = UiFactory.Label(root, "RestTitle", "REST SITE", 40, Palette.Victory);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -110f), new Vector2(900f, 54f));

            _subtitle = UiFactory.Label(root, "RestSubtitle", "", 22, Palette.InkMuted);
            UiFactory.Place(_subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -170f), new Vector2(1200f, 32f));

            // ── The two choices ─────────────────────────────────────────────────────────
            _choices = UiFactory.Panel(root, "Choices", new Color(0f, 0f, 0f, 0f));
            UiFactory.Place(_choices, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, -20f), new Vector2(900f, 220f));

            var heal = UiFactory.TextButton(_choices, "Heal", "", Palette.PanelRaised, Palette.Ink, 26);
            UiFactory.Place((RectTransform)heal.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(-220f, 0f), new Vector2(380f, 180f));
            _healLabel = heal.GetComponentInChildren<Text>();
            heal.onClick.AddListener(() => HealChosen?.Invoke());

            _smithButton = UiFactory.TextButton(_choices, "Smith", "Smith\nUpgrade a card",
                                                Palette.PanelRaised, Palette.Ink, 26);
            UiFactory.Place((RectTransform)_smithButton.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(220f, 0f), new Vector2(380f, 180f));
            _smithButton.onClick.AddListener(ShowPicker);

            // ── The upgrade picker ──────────────────────────────────────────────────────
            _picker = UiFactory.Panel(root, "Picker", new Color(0f, 0f, 0f, 0f));
            UiFactory.Stretch(_picker);

            // Cards are drawn at full size inside a scaled grid. CardView resets its own scale
            // when it refreshes, so scaling the cards themselves would be undone immediately.
            _grid = UiFactory.Panel(_picker, "Grid", new Color(0f, 0f, 0f, 0f));
            UiFactory.Place(_grid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, -30f), new Vector2(10f, 10f));

            var back = UiFactory.TextButton(_picker, "Back", "Back", Palette.PanelRaised, Palette.InkMuted, 24);
            UiFactory.Place((RectTransform)back.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 40f), new Vector2(200f, 60f));
            back.onClick.AddListener(ShowChoices);

            gameObject.SetActive(false);
        }

        public void Show(int healAmount, int hp, int maxHp, List<CardData> upgradable)
        {
            _upgradable = upgradable ?? new List<CardData>();

            int healed = Mathf.Min(maxHp, hp + healAmount);
            _healLabel.text = $"Rest\nHeal {healed - hp} HP   ({hp} → {healed})";

            // A deck with nothing left to upgrade must not offer the forge and then show an
            // empty screen behind it.
            _smithButton.interactable = _upgradable.Count > 0;

            ShowChoices();
            gameObject.SetActive(true);
        }

        public void Hide() => gameObject.SetActive(false);

        void ShowChoices()
        {
            _subtitle.text = "Rest to recover, or work the forge to upgrade a card.";
            _choices.gameObject.SetActive(true);
            _picker.gameObject.SetActive(false);
        }

        void ShowPicker()
        {
            foreach (var view in _pickerCards)
                if (view != null) { view.transform.SetParent(null, false); Destroy(view.gameObject); }
            _pickerCards.Clear();

            _subtitle.text = "Choose a card to upgrade. Each is shown as it will be after upgrading.";

            int rows = Mathf.Max(1, Mathf.CeilToInt(_upgradable.Count / (float)PickerColumns));
            _grid.localScale = Vector3.one * (rows == 1 ? OneRowScale : MultiRowScale);
            const float stepX = CardView.Width + 24f;
            const float stepY = CardView.Height + 30f;

            for (int i = 0; i < _upgradable.Count; i++)
            {
                var original = _upgradable[i];
                int column = i % PickerColumns;
                int row = i / PickerColumns;
                int columnsInRow = Mathf.Min(PickerColumns, _upgradable.Count - row * PickerColumns);

                var view = CardView.Create(_grid, new CardInstance(original.Upgrade));
                UiFactory.Place((RectTransform)view.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                Vector2.zero, new Vector2(CardView.Width, CardView.Height));
                view.SetRestPosition(new Vector2((column - (columnsInRow - 1) * 0.5f) * stepX,
                                                 -(row - (rows - 1) * 0.5f) * stepY));
                view.Refresh(playable: true, selected: false, displayedCost: original.Upgrade.Cost);

                view.Clicked += _ => UpgradeChosen?.Invoke(original);
                _pickerCards.Add(view);
            }

            _choices.gameObject.SetActive(false);
            _picker.gameObject.SetActive(true);
        }
    }
}
