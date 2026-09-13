using System;
using EmberDeck.Content;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>One card in hand. Owns no rules — it renders a CardInstance and reports clicks.</summary>
    public sealed class CardView : MonoBehaviour
    {
        public const float Width = 190f;
        public const float Height = 268f;

        public CardInstance Card { get; private set; }

        Image _background;
        Image _typeStripe;
        Text _costLabel;
        Image _costBadge;
        Text _nameLabel;
        Text _descriptionLabel;
        Button _button;

        Vector2 _restPosition;

        public event Action<CardView> Clicked;

        public static CardView Create(Transform parent, CardInstance card)
        {
            var root = UiFactory.Panel(parent, $"Card_{card.Data.Id}", Palette.CardIdle);
            root.sizeDelta = new Vector2(Width, Height);

            var view = root.gameObject.AddComponent<CardView>();
            view.Build(card);
            return view;
        }

        void Build(CardInstance card)
        {
            Card = card;
            var rect = (RectTransform)transform;
            _background = GetComponent<Image>();

            // A colour band per card type: the player reads "is this an attack?" from the
            // silhouette long before reading any text.
            var stripe = UiFactory.Panel(rect, "TypeStripe", card.Data.TintColor);
            stripe.anchorMin = new Vector2(0f, 1f);
            stripe.anchorMax = new Vector2(1f, 1f);
            stripe.pivot = new Vector2(0.5f, 1f);
            stripe.anchoredPosition = Vector2.zero;
            stripe.sizeDelta = new Vector2(0f, 8f);
            _typeStripe = stripe.GetComponent<Image>();

            var badge = UiFactory.Panel(rect, "CostBadge", Palette.Energy);
            UiFactory.Place(badge, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(10f, -18f), new Vector2(42f, 42f));
            _costBadge = badge.GetComponent<Image>();

            _costLabel = UiFactory.Label(badge, "Cost", card.BaseCost.ToString(), 26, Palette.Background);
            UiFactory.Stretch(_costLabel.rectTransform);

            _nameLabel = UiFactory.Label(rect, "Name", card.Data.DisplayName, 22, Palette.Ink);
            UiFactory.Place(_nameLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(18f, -22f), new Vector2(Width - 70f, 34f));

            _descriptionLabel = UiFactory.Label(rect, "Description", card.Data.BuildDescription(), 17,
                                                Palette.InkMuted, TextAnchor.UpperCenter);
            UiFactory.Place(_descriptionLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, -18f), new Vector2(Width - 28f, Height - 120f));

            _button = gameObject.AddComponent<Button>();
            _button.targetGraphic = _background;
            _button.onClick.AddListener(() => Clicked?.Invoke(this));
        }

        public void SetRestPosition(Vector2 position)
        {
            _restPosition = position;
            ((RectTransform)transform).anchoredPosition = position;
        }

        /// <summary>
        /// Selected cards lift out of the hand. It is the cheapest possible feedback and it
        /// removes the single most common confusion in a card game: which card am I holding?
        /// </summary>
        public void Refresh(bool playable, bool selected, int displayedCost)
        {
            _background.color = !playable ? Palette.CardDisabled
                              : selected  ? Palette.CardSelected
                                          : Palette.CardIdle;

            var tint = Card.Data.TintColor;
            _typeStripe.color = playable ? tint : tint * 0.45f;
            _costBadge.color = playable ? Palette.Energy : Palette.Energy * 0.4f;
            _costLabel.text = displayedCost.ToString();
            _nameLabel.color = playable ? Palette.Ink : Palette.InkMuted;

            var rect = (RectTransform)transform;
            rect.anchoredPosition = _restPosition + (selected ? new Vector2(0f, 46f) : Vector2.zero);
            rect.localScale = Vector3.one * (selected ? 1.06f : 1f);
        }
    }
}
