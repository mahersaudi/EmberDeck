using System;
using System.Collections.Generic;
using EmberDeck.Content;
using EmberDeck.Run;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// The screen that builds the deck a run starts with: thirty cards, taken from everything the
    /// player has.
    ///
    /// The pool is drawn as tiles rather than as full cards. Forty cards at card size is four
    /// screens of scrolling, and building a deck means comparing cards against each other — which
    /// needs them all in view at once. A tile carries what a choice turns on: the art, the name,
    /// the cost, and how many copies are in the deck already. The full rules text is a tap away in
    /// the tooltip, where it is for every other card in the game.
    ///
    /// The rules — thirty cards, and a copy limit that falls with rarity — belong to DeckBuilder;
    /// this screen only shows them. The simulator builds decks under the same rules.
    /// </summary>
    public sealed class DeckBuilderView : MonoBehaviour
    {
        const int Columns = 5;
        const float TileWidth = 264f;
        const float TileHeight = 116f;
        const float TileGap = 12f;
        const float ListWidth = 430f;

        /// <summary>The finished deck. Only raised when it is legal.</summary>
        public event Action<List<CardData>> Confirmed;

        /// <summary>Back, without starting anything.</summary>
        public event Action Cancelled;

        RunConfig _config;
        ICollection<string> _unlocked;
        readonly List<CardData> _pool = new();
        readonly List<CardData> _deck = new();
        readonly List<(CardData Card, RectTransform Root, Image Frame, Text Count)> _tiles = new();

        Text _counter;
        Text _hint;
        Button _start;
        RectTransform _listContent;
        RectTransform _poolContent;

        public bool IsOpen => gameObject.activeSelf;

        public static DeckBuilderView Create(Transform parent)
        {
            var root = UiFactory.Panel(parent, "DeckBuilder", new Color(0.05f, 0.05f, 0.07f, 1f));
            UiFactory.Stretch(root);

            var view = root.gameObject.AddComponent<DeckBuilderView>();
            view.Build();
            root.gameObject.SetActive(false);
            return view;
        }

        void Build()
        {
            var root = (RectTransform)transform;

            var title = UiFactory.Label(root, "Title", "BUILD YOUR DECK", 42, Palette.Energy);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -28f), new Vector2(900f, 52f));

            _counter = UiFactory.Label(root, "Counter", "", 30, Palette.Ink);
            UiFactory.Place(_counter.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -82f), new Vector2(900f, 38f));

            _hint = UiFactory.Label(root, "Hint", "", 20, Palette.InkMuted);
            UiFactory.Place(_hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -120f), new Vector2(1400f, 30f));

            // Back and Suggested on one side, Start on the other: the button that ends the screen is
            // never next to the button that empties it.
            var back = UiFactory.TextButton(root, "DeckBack", "Back", Palette.PanelRaised, Palette.Ink, 24);
            UiFactory.Place((RectTransform)back.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(40f, -30f), new Vector2(150f, 58f));
            back.onClick.AddListener(() => Cancelled?.Invoke());
            NavHint.On(back).Cancel = true;

            var suggested = UiFactory.TextButton(root, "DeckSuggested", "Suggested", Palette.PanelRaised, Palette.Ink, 24);
            UiFactory.Place((RectTransform)suggested.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(200f, -30f), new Vector2(210f, 58f));
            suggested.onClick.AddListener(() =>
            {
                _deck.Clear();
                _deck.AddRange(DeckBuilder.Suggested(_config, _unlocked));
                Refresh();
            });

            var clear = UiFactory.TextButton(root, "DeckClear", "Clear", Palette.PanelDark, Palette.InkMuted, 24);
            UiFactory.Place((RectTransform)clear.transform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(420f, -30f), new Vector2(150f, 58f));
            clear.onClick.AddListener(() =>
            {
                _deck.Clear();
                Refresh();
            });

            _start = UiFactory.TextButton(root, "DeckStart", "Start run", Palette.Energy, Palette.Background, 28);
            UiFactory.Place((RectTransform)_start.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                            new Vector2(-40f, -30f), new Vector2(260f, 58f));
            _start.onClick.AddListener(Confirm);
            NavHint.On(_start).Priority = 20;

            // The pool, scrolling, with the deck beside it.
            _poolContent = BuildScroll(root, "Pool",
                                       new Vector2(0f, 0f), new Vector2(1f, 1f),
                                       new Vector2(40f, 40f), new Vector2(-(ListWidth + 80f), -150f));

            var listPanel = UiFactory.Panel(root, "DeckPanel", Palette.PanelDark);
            listPanel.anchorMin = new Vector2(1f, 0f);
            listPanel.anchorMax = new Vector2(1f, 1f);
            listPanel.pivot = new Vector2(1f, 0.5f);
            listPanel.offsetMin = new Vector2(-(ListWidth + 40f), 40f);
            listPanel.offsetMax = new Vector2(-40f, -150f);
            UiFactory.Frame(listPanel.GetComponent<Image>(), "frame_panel", 4f);

            var listTitle = UiFactory.Label(listPanel, "ListTitle", "Your deck", 26, Palette.Ink);
            UiFactory.Place(listTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -12f), new Vector2(ListWidth - 24f, 34f));

            _listContent = BuildScroll(listPanel, "DeckList",
                                       Vector2.zero, Vector2.one,
                                       new Vector2(12f, 12f), new Vector2(-12f, -54f));
        }

        /// <summary>
        /// A scrolling column. Written out rather than taken from a layout group: the content's height
        /// has to be set from the rows that were actually placed, and a GridLayoutGroup fights with
        /// placing rows by hand.
        /// </summary>
        static RectTransform BuildScroll(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax,
                                         Vector2 offsetMin, Vector2 offsetMax)
        {
            var viewport = UiFactory.Panel(parent, name, Palette.Background);
            viewport.anchorMin = anchorMin;
            viewport.anchorMax = anchorMax;
            viewport.pivot = new Vector2(0.5f, 0.5f);
            viewport.offsetMin = offsetMin;
            viewport.offsetMax = offsetMax;

            // A stencil Mask, not RectMask2D. RectMask2D computes its clip rectangle in canvas space,
            // and the Arabic stage is mirrored by a negative scale — which inverts that rectangle and
            // clips the entire contents away. The mask graphic itself is never drawn.
            var mask = viewport.gameObject.AddComponent<Mask>();
            mask.showMaskGraphic = false;

            var content = UiFactory.Panel(viewport, "Content", new Color(0f, 0f, 0f, 0f));
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = new Vector2(0f, 0f);
            content.offsetMax = new Vector2(0f, 0f);
            content.sizeDelta = new Vector2(0f, 0f);

            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            return content;
        }

        /// <summary>Opens on the deck it is given — the last one played, or the suggested one.</summary>
        public void Show(RunConfig config, ICollection<string> unlocked, List<CardData> deck)
        {
            _config = config;
            _unlocked = unlocked;

            _pool.Clear();
            _pool.AddRange(DeckBuilder.Pool(config, unlocked));

            _deck.Clear();
            if (deck != null && deck.Count > 0) _deck.AddRange(deck);
            else _deck.AddRange(DeckBuilder.Suggested(config, unlocked));

            BuildTiles();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Refresh();
        }

        public void Hide()
        {
            Tooltip.Hide();
            gameObject.SetActive(false);
        }

        void Confirm()
        {
            if (!DeckBuilder.IsLegal(_deck))
            {
                AudioDirector.Play(Sfx.Click, 0.6f);
                Motion.Shake((RectTransform)_start.transform, 8f);
                return;
            }

            AudioDirector.Play(Sfx.Reward, 0.9f);
            Confirmed?.Invoke(new List<CardData>(_deck));
        }

        // ── The pool ─────────────────────────────────────────────────────────────────

        void BuildTiles()
        {
            foreach (var tile in _tiles)
                if (tile.Root != null)
                {
                    tile.Root.SetParent(null, false);
                    Destroy(tile.Root.gameObject);
                }
            _tiles.Clear();

            for (int i = 0; i < _pool.Count; i++)
            {
                var card = _pool[i];
                int column = i % Columns;
                int row = i / Columns;

                var tile = UiFactory.Panel(_poolContent, $"Tile_{card.Id}", Palette.PanelRaised);
                UiFactory.Place(tile, new Vector2(0f, 1f), new Vector2(0f, 1f),
                                new Vector2(column * (TileWidth + TileGap), -row * (TileHeight + TileGap)),
                                new Vector2(TileWidth, TileHeight));
                UiFactory.Frame(tile.GetComponent<Image>(), "frame_panel", 4f);

                var art = UiFactory.Panel(tile, "Art", Palette.ArtWell);
                UiFactory.Place(art, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(10f, 0f),
                                new Vector2(96f, TileHeight - 20f));
                var artImage = art.GetComponent<Image>();
                artImage.raycastTarget = false;
                if (card.Art != null)
                {
                    artImage.sprite = card.Art;
                    artImage.color = Color.white;
                    artImage.preserveAspect = true;
                }

                // The type stripe under the art, as on the card itself: an attack is read from its colour.
                var stripe = UiFactory.Panel(tile, "Stripe", card.TintColor);
                UiFactory.Place(stripe, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(10f, 6f),
                                new Vector2(96f, 4f));
                stripe.GetComponent<Image>().raycastTarget = false;

                var name = UiFactory.Label(tile, "Name", card.DisplayName, 20, Palette.Ink, TextAnchor.UpperLeft);
                UiFactory.Place(name.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(116f, -12f),
                                new Vector2(TileWidth - 128f, 48f));

                var cost = UiFactory.Label(tile, "Cost", $"{card.Cost}", 22, Palette.Energy, TextAnchor.LowerLeft);
                UiFactory.Place(cost.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(116f, 10f),
                                new Vector2(90f, 30f));

                var count = UiFactory.Label(tile, "Count", "", 24, Palette.Ink, TextAnchor.LowerRight);
                UiFactory.Place(count.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-12f, 10f),
                                new Vector2(140f, 30f));

                var button = tile.gameObject.AddComponent<Button>();
                button.targetGraphic = tile.GetComponent<Image>();
                var captured = card;
                button.onClick.AddListener(() => Add(captured));
                TooltipTrigger.Attach(tile.gameObject, () => Describe(captured));

                _tiles.Add((card, tile, tile.GetComponent<Image>(), count));
            }

            int rows = Mathf.Max(1, Mathf.CeilToInt(_pool.Count / (float)Columns));
            _poolContent.sizeDelta = new Vector2(0f, rows * (TileHeight + TileGap) + 8f);
            _poolContent.anchoredPosition = Vector2.zero;
        }

        IReadOnlyList<Tooltip.Entry> Describe(CardData card)
        {
            var entries = new List<Tooltip.Entry>
            {
                new(card.DisplayName, $"{card.BuildDescription()}  ({card.Cost} Energy)"),
            };
            foreach (var keyword in Keywords.ForCard(card))
                entries.Add(Tooltip.Entry.From(keyword));
            return entries;
        }

        void Add(CardData card)
        {
            if (!DeckBuilder.CanAdd(_deck, card))
            {
                AudioDirector.Play(Sfx.Click, 0.5f);
                return;
            }

            _deck.Add(card);
            AudioDirector.Play(Sfx.CardDraw, 0.7f);
            Refresh();
        }

        void Remove(CardData card)
        {
            if (!_deck.Remove(card)) return;
            AudioDirector.Play(Sfx.Click, 0.7f);
            Refresh();
        }

        // ── Drawing the state ────────────────────────────────────────────────────────

        void Refresh()
        {
            _counter.text = $"{_deck.Count} / {DeckBuilder.DeckSize} cards";
            _counter.color = _deck.Count == DeckBuilder.DeckSize ? Palette.Victory : Palette.Ink;
            _hint.text = TouchMode.Active
                ? "Tap a card to add it. Tap it in your deck to take it out."
                : "Click a card to add it. Click it in your deck to take it out.";
            _start.interactable = DeckBuilder.IsLegal(_deck);

            foreach (var tile in _tiles)
            {
                int held = DeckBuilder.CountOf(_deck, tile.Card);
                int max = DeckBuilder.MaxCopies(tile.Card);
                bool room = DeckBuilder.CanAdd(_deck, tile.Card);

                tile.Count.text = held > 0 ? $"{held} / {max}" : $"max {max}";
                tile.Count.color = held > 0 ? Palette.Energy : Palette.InkMuted;
                tile.Frame.color = held > 0 ? Palette.PanelRaised : room ? Palette.PanelDark : Palette.CardDisabled;
            }

            RefreshList();
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        /// <summary>Capture-harness only: the deck in one line.</summary>
        public string DebugSummary() =>
            $"{_deck.Count}/{DeckBuilder.DeckSize} cards, {_pool.Count} in the pool, legal={DeckBuilder.IsLegal(_deck)}";

        /// <summary>Capture-harness only: takes one copy of the first card in the deck out.</summary>
        public string DebugRemoveFirst()
        {
            if (_deck.Count == 0) return "empty";
            var card = _deck[0];
            Remove(card);
            return $"{card.DisplayName}, now {_deck.Count} cards, legal={DeckBuilder.IsLegal(_deck)}, start={_start.interactable}";
        }

        /// <summary>Capture-harness only: adds the first card the deck still has room for.</summary>
        public string DebugAddFirst()
        {
            foreach (var card in _pool)
            {
                if (!DeckBuilder.CanAdd(_deck, card)) continue;
                Add(card);
                return $"{card.DisplayName}, now {_deck.Count} cards, legal={DeckBuilder.IsLegal(_deck)}, start={_start.interactable}";
            }
            return "no room";
        }
#endif

        void RefreshList()
        {
            for (int i = _listContent.childCount - 1; i >= 0; i--)
            {
                var child = _listContent.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }

            // One row per distinct card, in the pool's order, so a deck reads the way the pool does.
            var rows = new List<(CardData Card, int Count)>();
            foreach (var card in _pool)
            {
                int count = DeckBuilder.CountOf(_deck, card);
                if (count > 0) rows.Add((card, count));
            }

            const float rowHeight = 44f;
            for (int i = 0; i < rows.Count; i++)
            {
                var (card, count) = rows[i];
                var row = UiFactory.Panel(_listContent, $"Row_{card.Id}", Palette.PanelRaised);
                UiFactory.Place(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -i * (rowHeight + 6f)),
                                new Vector2(ListWidth - 24f, rowHeight));

                var label = UiFactory.Label(row, "Name", $"{count} x  {card.DisplayName}", 21, Palette.Ink, TextAnchor.MiddleLeft);
                UiFactory.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f),
                                new Vector2(ListWidth - 110f, rowHeight));

                var cost = UiFactory.Label(row, "Cost", card.Cost.ToString(), 21, Palette.Energy, TextAnchor.MiddleRight);
                UiFactory.Place(cost.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f),
                                new Vector2(70f, rowHeight));

                var button = row.gameObject.AddComponent<Button>();
                button.targetGraphic = row.GetComponent<Image>();
                var captured = card;
                button.onClick.AddListener(() => Remove(captured));
                TooltipTrigger.Attach(row.gameObject, () => Describe(captured));
            }

            _listContent.sizeDelta = new Vector2(0f, rows.Count * (rowHeight + 6f) + 8f);
        }
    }
}
