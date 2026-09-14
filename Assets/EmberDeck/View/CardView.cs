using System;
using EmberDeck.Content;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// One card in hand. Owns no rules — it renders a CardInstance and reports clicks.
    ///
    /// Laid out like a physical trading card because that is what it is: art across the top
    /// half, a name bar, then rules text. The earlier layout gave the illustration an 86px
    /// square, which is smaller than the detail in the art — a painting at that size is mud,
    /// and the whole point of having art is lost.
    /// </summary>
    public sealed class CardView : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public const float Width = 202f;
        public const float Height = 296f;

        const float Border = 3f;
        const float ArtHeight = 128f;

        public CardInstance Card { get; private set; }

        Image _frame;
        Image _face;
        Image _art;
        Image _typeStripe;
        Image _costBadge;
        Text _costLabel;
        Text _nameLabel;
        Text _descriptionLabel;
        Button _button;

        Vector2 _restPosition;
        CanvasGroup _group;
        bool _placed;
        bool _selected;
        bool _hovered;
        bool _leaving;
        int _siblingBeforeHover = -1;

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

            // The outer rectangle is the rarity border; the face sits inset inside it. One
            // object doing both would mean the border could not change colour independently.
            _frame = GetComponent<Image>();
            _frame.color = RarityColor(card.Data.Rarity);

            var face = UiFactory.Panel(rect, "Face", Palette.CardIdle);
            UiFactory.Stretch(face, Border);
            _face = face.GetComponent<Image>();

            var artPanel = UiFactory.Panel(face, "Art", Palette.ArtWell);
            UiFactory.Place(artPanel, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -6f), new Vector2(Width - 2 * Border - 12f, ArtHeight));
            _art = artPanel.GetComponent<Image>();
            _art.raycastTarget = false;
            if (card.Data.Art != null)
            {
                _art.sprite = card.Data.Art;
                _art.color = Color.white;
                _art.preserveAspect = true;
            }

            // A colour band under the art: the player reads "is this an attack?" from the
            // silhouette long before reading any text.
            var stripe = UiFactory.Panel(face, "TypeStripe", card.Data.TintColor);
            UiFactory.Place(stripe, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -(ArtHeight + 8f)), new Vector2(Width - 2 * Border - 12f, 4f));
            _typeStripe = stripe.GetComponent<Image>();

            // Cost overlaps the art's top-left corner, the way a mana cost does. It is the
            // one number read on every card every turn, so it gets the strongest position.
            var badge = UiFactory.Panel(face, "CostBadge", Palette.Energy);
            UiFactory.Place(badge, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(4f, -4f), new Vector2(44f, 44f));
            _costBadge = badge.GetComponent<Image>();
            _costLabel = UiFactory.Label(badge, "Cost", card.BaseCost.ToString(), 27, Palette.Background);
            UiFactory.Stretch(_costLabel.rectTransform);

            _nameLabel = UiFactory.Label(face, "Name", card.Data.DisplayName, 19, Palette.Ink);
            UiFactory.Place(_nameLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -(ArtHeight + 14f)), new Vector2(Width - 24f, 34f));

            _descriptionLabel = UiFactory.Label(face, "Description", card.Data.BuildDescription(), 16,
                                                Palette.InkMuted);
            UiFactory.Place(_descriptionLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 8f), new Vector2(Width - 26f, Height - ArtHeight - 66f));

            _group = gameObject.AddComponent<CanvasGroup>();

            _button = gameObject.AddComponent<Button>();
            _button.targetGraphic = _frame;
            _button.onClick.AddListener(() => Clicked?.Invoke(this));
        }

        static Color RarityColor(CardRarity rarity) => rarity switch
        {
            CardRarity.Rare     => Palette.RarityRare,
            CardRarity.Uncommon => Palette.RarityUncommon,
            _                   => Palette.RarityCommon,
        };

        /// <summary>
        /// Where the card belongs. The first call places it there; later calls make it glide, so a
        /// hand that re-fans after a play slides into its new shape instead of jumping.
        /// </summary>
        public void SetRestPosition(Vector2 position)
        {
            _restPosition = position;
            if (_placed) return;
            _placed = true;
            ((RectTransform)transform).anchoredPosition = position;
        }

        /// <summary>Starts the card somewhere else — the draw pile — so it travels to its rest position.</summary>
        public void SpawnAt(Vector2 position, float scale)
        {
            _placed = true;
            var rect = (RectTransform)transform;
            rect.anchoredPosition = position;
            rect.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>Sends the card off — to the board when played, to the discard pile otherwise — and destroys it.</summary>
        public void FlyAway(Vector2 to, float endScale, float duration)
        {
            if (_leaving) return;
            _leaving = true;
            _group.blocksRaycasts = false;

            var rect = (RectTransform)transform;
            Vector2 from = rect.anchoredPosition;
            float fromScale = rect.localScale.x;
            var fromRotation = rect.localRotation;

            Motion.Run(this, "leave", duration, t =>
            {
                rect.anchoredPosition = Vector2.LerpUnclamped(from, to, t);
                float s = Mathf.LerpUnclamped(fromScale, endScale, t);
                rect.localScale = new Vector3(s, s, 1f);
                rect.localRotation = Quaternion.Slerp(fromRotation, Quaternion.identity, t);
                _group.alpha = t < 0.55f ? 1f : 1f - (t - 0.55f) / 0.45f;
            }, Motion.OutCubic, 0f, () => { if (this != null) Destroy(gameObject); });
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_leaving) return;
            _hovered = true;
            // Brought to the front so an overlapped card can be read in full while it is pointed at.
            _siblingBeforeHover = transform.GetSiblingIndex();
            transform.SetAsLastSibling();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hovered = false;
            if (_siblingBeforeHover < 0 || transform.parent == null) return;
            transform.SetSiblingIndex(Mathf.Min(_siblingBeforeHover, transform.parent.childCount - 1));
            _siblingBeforeHover = -1;
        }

        void Update()
        {
            if (_leaving) return;

            // Exponential smoothing rather than a timed tween: the target changes whenever the hand
            // re-fans or the selection moves, and this follows a moving target without restarts.
            var rect = (RectTransform)transform;
            Vector2 target = _restPosition + (_selected ? new Vector2(0f, 46f) : _hovered ? new Vector2(0f, 14f) : Vector2.zero);
            float scale = _selected ? 1.06f : _hovered ? 1.04f : 1f;
            float k = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 14f);
            rect.anchoredPosition = Vector2.Lerp(rect.anchoredPosition, target, k);
            float s = Mathf.Lerp(rect.localScale.x, scale, k);
            rect.localScale = new Vector3(s, s, 1f);
        }

        /// <summary>
        /// Selected cards lift out of the hand. It is the cheapest possible feedback and it
        /// removes the single most common confusion in a card game: which card am I holding?
        /// </summary>
        public void Refresh(bool playable, bool selected, int displayedCost)
        {
            _face.color = !playable ? Palette.CardDisabled
                        : selected  ? Palette.CardSelected
                                    : Palette.CardIdle;

            _frame.color = selected
                ? Palette.Ink
                : RarityColor(Card.Data.Rarity) * (playable ? 1f : 0.55f);

            var tint = Card.Data.TintColor;
            _typeStripe.color = playable ? tint : tint * 0.45f;
            _costBadge.color = playable ? Palette.Energy : Palette.Energy * 0.4f;
            _costLabel.text = displayedCost.ToString();
            _nameLabel.color = playable ? Palette.Ink : Palette.InkMuted;
            if (_art.sprite != null)
                _art.color = playable ? Color.white : new Color(1f, 1f, 1f, 0.42f);

            _selected = selected;
        }
    }
}
