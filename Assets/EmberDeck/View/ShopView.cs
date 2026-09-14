using System;
using System.Collections.Generic;
using EmberDeck.Content;
using EmberDeck.Run;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// The shop: spend gold on cards, a relic, or removing a card from the deck.
    ///
    /// Gold turns fights into a currency and the map into a plan: a route through two shops is a
    /// different run from a route through two elites. Prices are coloured by whether they can be
    /// paid now, so the question "can I afford this?" is answered before it is asked.
    /// </summary>
    public sealed class ShopView : MonoBehaviour
    {
        const float OfferSpacing = CardView.Width + 60f;
        const int PickerColumns = 8;

        public event Action Left;

        sealed class Offer
        {
            public ShopItem Item;
            public GameObject Slot;
            public CardView View;
            public Text Price;
        }

        sealed class PotionRow
        {
            public GameObject Root;
            public Image Icon;
            public Text Name;
            public Button Buy;
            public Text BuyLabel;
        }

        RunState _run;
        ShopStock _stock;

        Text _gold;
        RectTransform _offers;
        readonly List<Offer> _offerViews = new();

        Text _relicName;
        Text _relicText;
        Button _relicBuy;
        Text _relicBuyLabel;

        Button _remove;
        Text _removeLabel;

        readonly List<PotionRow> _potionRows = new();

        RectTransform _picker;
        RectTransform _pickerGrid;
        readonly List<GameObject> _pickerCards = new();

        public bool IsOpen => gameObject.activeSelf;

        public static ShopView Create(Transform parent)
        {
            var root = UiFactory.Panel(parent, "ShopScreen", new Color(0.05f, 0.05f, 0.07f, 1f));
            UiFactory.Stretch(root);
            var view = root.gameObject.AddComponent<ShopView>();
            view.Build(root);
            return view;
        }

        void Build(RectTransform root)
        {
            var title = UiFactory.Label(root, "ShopTitle", "SHOP", 44, Palette.Energy);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(800f, 56f));

            _gold = UiFactory.Label(root, "ShopGold", "", 30, Palette.Energy);
            UiFactory.Place(_gold.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -108f), new Vector2(800f, 40f));

            var hint = UiFactory.Label(root, "ShopHint", "Cards, a relic, potions, or removing a card. Gold you keep carries on to the next shop.",
                                       20, Palette.InkMuted);
            UiFactory.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -148f), new Vector2(1300f, 30f));

            _offers = (RectTransform)new GameObject("Offers", typeof(RectTransform)).transform;
            _offers.SetParent(root, false);
            UiFactory.Place(_offers, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(10f, 10f));

            // ── The relic ──
            var relicPanel = UiFactory.Panel(root, "RelicOffer", Palette.PanelDark);
            UiFactory.Place(relicPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-580f, -300f), new Vector2(520f, 170f));
            Header(relicPanel, "RELIC", Palette.RarityRare);
            _relicName = UiFactory.Label(relicPanel, "RelicName", "", 24, Palette.Ink, TextAnchor.UpperLeft);
            UiFactory.Place(_relicName.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -40f), new Vector2(320f, 32f));
            _relicText = UiFactory.Label(relicPanel, "RelicText", "", 18, Palette.InkMuted, TextAnchor.UpperLeft);
            UiFactory.Place(_relicText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -74f), new Vector2(320f, 86f));
            _relicBuy = UiFactory.TextButton(relicPanel, "ShopBuyRelic", "", Palette.PanelRaised, Palette.Ink, 22);
            UiFactory.Place((RectTransform)_relicBuy.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(170f, 76f));
            _relicBuyLabel = _relicBuy.GetComponentInChildren<Text>();
            _relicBuy.onClick.AddListener(BuyRelic);

            // ── Removal ──
            var removePanel = UiFactory.Panel(root, "RemoveOffer", Palette.PanelDark);
            UiFactory.Place(removePanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(580f, -300f), new Vector2(520f, 170f));
            Header(removePanel, "CARD REMOVAL", Palette.Victory);
            var removeText = UiFactory.Label(removePanel, "RemoveText",
                                             "Remove one card from your deck. Fewer weak cards means better draws. The price rises each time.",
                                             18, Palette.InkMuted, TextAnchor.UpperLeft);
            UiFactory.Place(removeText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -44f), new Vector2(320f, 110f));
            _remove = UiFactory.TextButton(removePanel, "ShopRemove", "", Palette.PanelRaised, Palette.Ink, 22);
            UiFactory.Place((RectTransform)_remove.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(170f, 76f));
            _removeLabel = _remove.GetComponentInChildren<Text>();
            _remove.onClick.AddListener(OpenPicker);

            // ── Potions ──
            var potionPanel = UiFactory.Panel(root, "PotionOffer", Palette.PanelDark);
            UiFactory.Place(potionPanel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -300f), new Vector2(520f, 170f));
            Header(potionPanel, "POTIONS", Palette.IntentBlock);
            for (int i = 0; i < PotionService.ShopShelf; i++)
            {
                // Nearly transparent rather than invisible: it must still catch the pointer for its tooltip.
                var row = UiFactory.Panel(potionPanel, $"PotionRow{i}", new Color(0f, 0f, 0f, 0.01f));
                UiFactory.Place(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -40f - i * 64f), new Vector2(500f, 60f));
                var icon = Icons.Create(row, "Icon", null, 44f);
                UiFactory.Place((RectTransform)icon.transform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(8f, 0f), new Vector2(44f, 44f));
                var name = UiFactory.Label(row, "Name", "", 22, Palette.Ink, TextAnchor.MiddleLeft);
                UiFactory.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(62f, 0f), new Vector2(260f, 40f));
                var buy = UiFactory.TextButton(row, $"ShopBuyPotion{i}", "", Palette.PanelRaised, Palette.Ink, 20);
                UiFactory.Place((RectTransform)buy.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-8f, 0f), new Vector2(150f, 54f));
                int slot = i;
                buy.onClick.AddListener(() => BuyPotion(slot));
                TooltipTrigger.Attach(row.gameObject, () => DescribePotion(slot));
                _potionRows.Add(new PotionRow { Root = row.gameObject, Icon = icon, Name = name, Buy = buy, BuyLabel = buy.GetComponentInChildren<Text>() });
            }

            var leave = UiFactory.TextButton(root, "ShopLeave", "Leave", Palette.PanelRaised, Palette.Ink, 28);
            UiFactory.Place((RectTransform)leave.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(240f, 70f));
            leave.onClick.AddListener(() =>
            {
                Hide();
                Left?.Invoke();
            });

            // ── The removal picker, over everything else in the shop ──
            _picker = UiFactory.Panel(root, "RemovePicker", new Color(0.03f, 0.03f, 0.05f, 1f));
            UiFactory.Stretch(_picker);
            var pickTitle = UiFactory.Label(_picker, "PickTitle", "Choose a card to remove", 34, Palette.Ink);
            UiFactory.Place(pickTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1000f, 46f));
            _pickerGrid = (RectTransform)new GameObject("Grid", typeof(RectTransform)).transform;
            _pickerGrid.SetParent(_picker, false);
            UiFactory.Place(_pickerGrid, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(10f, 10f));
            _pickerGrid.localScale = Vector3.one * 0.72f;
            var cancel = UiFactory.TextButton(_picker, "ShopRemoveCancel", "Cancel", Palette.PanelRaised, Palette.InkMuted, 24);
            UiFactory.Place((RectTransform)cancel.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(220f, 64f));
            cancel.onClick.AddListener(ClosePicker);
            _picker.gameObject.SetActive(false);

            gameObject.SetActive(false);
        }

        static void Header(Transform panel, string text, Color color)
        {
            var header = UiFactory.Label(panel, "Header", text, 18, color, TextAnchor.UpperLeft);
            UiFactory.Place(header.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -12f), new Vector2(340f, 24f));
        }

        public void Show(RunState run, ShopStock stock)
        {
            _run = run;
            _stock = stock;

            foreach (var offer in _offerViews)
                if (offer.Slot != null) { offer.Slot.transform.SetParent(null, false); Destroy(offer.Slot); }
            _offerViews.Clear();

            float startX = -(stock.Cards.Count - 1) * OfferSpacing * 0.5f;
            for (int i = 0; i < stock.Cards.Count; i++)
            {
                var item = stock.Cards[i];
                var slot = (RectTransform)new GameObject("Offer", typeof(RectTransform)).transform;
                slot.SetParent(_offers, false);
                UiFactory.Place(slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(startX + i * OfferSpacing, 0f),
                                new Vector2(CardView.Width, CardView.Height + 80f));

                var view = CardView.Create(slot, new CardInstance(item.Card));
                UiFactory.Place((RectTransform)view.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                                new Vector2(CardView.Width, CardView.Height));
                view.SetRestPosition(new Vector2(0f, 30f));
                view.Refresh(playable: true, selected: false, displayedCost: item.Card.Cost);

                var price = UiFactory.Label(slot, "Price", "", 26, Palette.Energy);
                UiFactory.Place(price.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                new Vector2(0f, -CardView.Height * 0.5f - 14f), new Vector2(240f, 36f));

                if (item.OnSale)
                {
                    var sale = UiFactory.Label(slot, "Sale", "SALE  ·  HALF PRICE", 18, Palette.Victory);
                    sale.fontStyle = FontStyle.Bold;
                    UiFactory.Place(sale.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                    new Vector2(0f, CardView.Height * 0.5f + 48f), new Vector2(260f, 26f));
                }

                var offer = new Offer { Item = item, Slot = slot.gameObject, View = view, Price = price };
                view.Clicked += _ => BuyCard(offer);
                _offerViews.Add(offer);
            }

            for (int i = 0; i < _potionRows.Count; i++)
            {
                var row = _potionRows[i];
                bool stocked = i < stock.Potions.Count;
                row.Root.SetActive(stocked);
                if (!stocked) continue;
                Icons.SetSprite(row.Icon, Icons.Get(stock.Potions[i].Potion.Icon));
                row.Name.text = stock.Potions[i].Potion.DisplayName;
            }

            Refresh();
            ClosePicker();
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
        }

        public void Hide()
        {
            ClosePicker();
            gameObject.SetActive(false);
        }

        void Refresh()
        {
            _gold.text = $"Gold  {_run.Gold}";

            foreach (var offer in _offerViews)
            {
                offer.Price.text = offer.Item.Sold ? "SOLD" : $"{offer.Item.Price} gold";
                offer.Price.color = offer.Item.Sold ? Palette.InkMuted
                                  : _run.Gold >= offer.Item.Price ? Palette.Energy : Palette.Defeat;
            }

            if (_stock.Relic == null)
            {
                _relicName.text = "Sold out";
                _relicText.text = "You already own every relic.";
                _relicBuy.interactable = false;
                _relicBuyLabel.text = "—";
            }
            else
            {
                _relicName.text = _stock.Relic.DisplayName;
                _relicText.text = Keywords.Highlight(_stock.Relic.Description);
                _relicBuy.interactable = !_stock.RelicSold;
                _relicBuyLabel.text = _stock.RelicSold ? "SOLD" : $"Buy\n{_stock.RelicPrice} gold";
                _relicBuyLabel.color = _stock.RelicSold ? Palette.InkMuted
                                     : _run.Gold >= _stock.RelicPrice ? Palette.Ink : Palette.Defeat;
            }

            bool canRemove = !_stock.RemovalUsed && _run.Deck.Count > ShopService.MinimumDeck;
            _remove.interactable = canRemove;
            _removeLabel.text = _stock.RemovalUsed ? "USED" : $"Remove\n{_stock.RemovalPrice} gold";
            _removeLabel.color = !canRemove ? Palette.InkMuted
                               : _run.Gold >= _stock.RemovalPrice ? Palette.Ink : Palette.Defeat;

            for (int i = 0; i < _potionRows.Count && i < _stock.Potions.Count; i++)
            {
                var row = _potionRows[i];
                var item = _stock.Potions[i];
                bool room = PotionService.HasRoom(_run);
                row.Buy.interactable = !item.Sold;
                row.BuyLabel.text = item.Sold ? "SOLD" : room ? $"Buy\n{item.Price} gold" : "Belt full";
                row.BuyLabel.color = item.Sold || !room ? Palette.InkMuted
                                   : _run.Gold >= item.Price ? Palette.Ink : Palette.Defeat;
            }
        }

        void BuyCard(Offer offer)
        {
            if (offer.Item.Sold) return;
            if (!ShopService.BuyCard(_run, offer.Item))
            {
                Deny(offer.Price.rectTransform);
                return;
            }
            AudioDirector.Play(Sfx.Reward);
            offer.View.FlyAway(new Vector2(0f, 420f), 0.6f, 0.4f);
            Refresh();
        }

        void BuyRelic()
        {
            if (_stock.Relic == null || _stock.RelicSold) return;
            if (!ShopService.BuyRelic(_run, _stock))
            {
                Deny((RectTransform)_relicBuy.transform);
                return;
            }
            AudioDirector.Play(Sfx.Relic);
            Refresh();
        }

        void BuyPotion(int slot)
        {
            if (slot >= _stock.Potions.Count || _stock.Potions[slot].Sold) return;
            if (!ShopService.BuyPotion(_run, _stock.Potions[slot]))
            {
                Deny((RectTransform)_potionRows[slot].Buy.transform);
                return;
            }
            AudioDirector.Play(Sfx.Reward);
            Refresh();
        }

        IReadOnlyList<Tooltip.Entry> DescribePotion(int slot)
        {
            if (_stock == null || slot >= _stock.Potions.Count) return Array.Empty<Tooltip.Entry>();
            var potion = _stock.Potions[slot].Potion;
            return new[] { new Tooltip.Entry(potion.DisplayName, potion.BuildDescription(), potion.Icon) };
        }

        /// <summary>Not enough gold: the price shakes rather than a dialog appearing.</summary>
        static void Deny(RectTransform what)
        {
            Motion.Shake(what, 8f, 0.25f);
            AudioDirector.Play(Sfx.Click);
        }

        void OpenPicker()
        {
            if (_stock.RemovalUsed) return;
            if (_run.Gold < _stock.RemovalPrice)
            {
                Deny((RectTransform)_remove.transform);
                return;
            }

            foreach (var card in _pickerCards)
                if (card != null) { card.transform.SetParent(null, false); Destroy(card); }
            _pickerCards.Clear();

            var unique = new List<CardData>();
            foreach (var card in _run.Deck)
                if (card != null && !unique.Contains(card)) unique.Add(card);

            float stepX = CardView.Width + 24f;
            float stepY = CardView.Height + 28f;
            int rows = Mathf.Max(1, Mathf.CeilToInt(unique.Count / (float)PickerColumns));

            for (int i = 0; i < unique.Count; i++)
            {
                int column = i % PickerColumns;
                int row = i / PickerColumns;
                int columnsInRow = Mathf.Min(PickerColumns, unique.Count - row * PickerColumns);

                var card = unique[i];
                var view = CardView.Create(_pickerGrid, new CardInstance(card));
                UiFactory.Place((RectTransform)view.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                                new Vector2(CardView.Width, CardView.Height));
                view.SetRestPosition(new Vector2((column - (columnsInRow - 1) * 0.5f) * stepX, -(row - (rows - 1) * 0.5f) * stepY));
                view.Refresh(playable: true, selected: false, displayedCost: card.Cost);
                view.Clicked += _ => RemoveCard(card);
                _pickerCards.Add(view.gameObject);
            }

            _picker.gameObject.SetActive(true);
            _picker.SetAsLastSibling();
        }

        void RemoveCard(CardData card)
        {
            if (!ShopService.RemoveCard(_run, _stock, card))
            {
                AudioDirector.Play(Sfx.Click);
                return;
            }
            AudioDirector.Play(Sfx.Burn);   // it burns away
            ClosePicker();
            Refresh();
        }

        void ClosePicker()
        {
            if (_picker != null) _picker.gameObject.SetActive(false);
        }
    }
}
