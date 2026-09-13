using System.Collections.Generic;
using EmberDeck.Combat;
using EmberDeck.Content;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// The whole interface for one fight. It reads the model and writes nothing back except
    /// through CombatEngine — so the rules cannot accidentally depend on a button existing,
    /// and the balance simulator runs the identical combat with no view at all.
    /// </summary>
    public sealed class CombatView : MonoBehaviour
    {
        [Header("Content")]
        [SerializeField] RunConfig _config;

        [Header("Seed")]
        [Tooltip("Off means every run is identical — the only way to debug a reported fight.")]
        [SerializeField] bool _useRandomSeed = true;
        [SerializeField] int _fixedSeed = 12345;

        CombatSession _session;
        CombatState State => _session.State;
        CombatEngine Engine => _session.Engine;

        RectTransform _root;
        RectTransform _enemyRow;
        RectTransform _handRow;
        RectTransform _overlay;

        readonly List<EnemyView> _enemyViews = new();
        readonly List<CardView> _cardViews = new();
        CardView _selectedCard;

        Image _playerHealthFill;
        Text _playerHealthLabel;
        Text _playerBlockLabel;
        Image _playerBlockBadge;
        Text _playerStatusLabel;
        Text _energyLabel;
        Text _turnLabel;
        Text _drawLabel;
        Text _discardLabel;
        Text _seedLabel;
        Text _overlayLabel;
        Button _endTurnButton;

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only binding used by ContentGenerator when it builds the scene.
        ///
        /// Assigns the field directly rather than going through SerializedObject: in batch
        /// mode ApplyModifiedPropertiesWithoutUndo silently fails to write an object
        /// reference — no exception, no false return, just a null in the saved scene. A
        /// direct assignment is also compile-checked, where "_config" as a string is not.
        /// </summary>
        // public, not internal: the generator lives in Assembly-CSharp-Editor and this type
        // in Assembly-CSharp, and internal does not cross an assembly boundary.
        public void EditorBindConfig(RunConfig config) => _config = config;
#endif

        void Start()
        {
            EnsureEventSystem();
            BuildStaticUi();
            StartNewCombat();
        }

        // ── Setup ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// uGUI needs an EventSystem to receive clicks. The module here must match the
        /// project's active input backend (Project Settings > Player > Active Input
        /// Handling): this project uses the legacy Input Manager, so StandaloneInputModule
        /// is the correct one. Get this pair wrong and the game runs perfectly while
        /// silently ignoring every click — with no error to point at.
        /// </summary>
        static void EnsureEventSystem()
        {
            if (FindFirstObjectByType<EventSystem>() != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        void BuildStaticUi()
        {
            var canvasGo = new GameObject("CombatCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);

            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _root = (RectTransform)canvasGo.transform;

            var background = UiFactory.Panel(_root, "Background", Palette.Background);
            UiFactory.Stretch(background);

            _enemyRow = UiFactory.Panel(_root, "EnemyRow", new Color(0f, 0f, 0f, 0f));
            UiFactory.Place(_enemyRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -150f), new Vector2(1400f, 320f));

            _handRow = UiFactory.Panel(_root, "Hand", new Color(0f, 0f, 0f, 0f));
            UiFactory.Place(_handRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 56f), new Vector2(1400f, 300f));

            BuildPlayerPanel();
            BuildHud();
            BuildOverlay();
        }

        void BuildPlayerPanel()
        {
            var panel = UiFactory.Panel(_root, "PlayerPanel", Palette.PanelDark);
            UiFactory.Place(panel, new Vector2(0f, 0f), new Vector2(0f, 0f),
                            new Vector2(40f, 340f), new Vector2(320f, 150f));

            var title = UiFactory.Label(panel, "Title", "EMBER", 22, Palette.Ink, TextAnchor.UpperLeft);
            UiFactory.Place(title.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(14f, -12f), new Vector2(200f, 28f));

            _playerHealthFill = UiFactory.Bar(panel, "Health", Palette.HealthTrack, Palette.Health,
                                              new Vector2(280f, 26f), new Vector2(0f, 2f));

            _playerHealthLabel = UiFactory.Label(panel, "HealthText", "", 19, Palette.Ink);
            UiFactory.Place(_playerHealthLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, 2f), new Vector2(280f, 26f));

            var blockBadge = UiFactory.Panel(panel, "BlockBadge", Palette.Block);
            UiFactory.Place(blockBadge, new Vector2(1f, 1f), new Vector2(1f, 1f),
                            new Vector2(-12f, -10f), new Vector2(52f, 36f));
            _playerBlockBadge = blockBadge.GetComponent<Image>();
            _playerBlockLabel = UiFactory.Label(blockBadge, "BlockText", "", 21, Palette.Background);
            UiFactory.Stretch(_playerBlockLabel.rectTransform);

            _playerStatusLabel = UiFactory.Label(panel, "Statuses", "", 17, Palette.InkMuted, TextAnchor.LowerLeft);
            UiFactory.Place(_playerStatusLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                            new Vector2(14f, 10f), new Vector2(290f, 24f));
        }

        void BuildHud()
        {
            var energyOrb = UiFactory.Panel(_root, "EnergyOrb", Palette.Energy);
            UiFactory.Place(energyOrb, new Vector2(0f, 0f), new Vector2(0f, 0f),
                            new Vector2(70f, 190f), new Vector2(96f, 96f));
            _energyLabel = UiFactory.Label(energyOrb, "EnergyText", "", 38, Palette.Background);
            UiFactory.Stretch(_energyLabel.rectTransform);

            _endTurnButton = UiFactory.TextButton(_root, "EndTurn", "End Turn", Palette.PanelRaised, Palette.Ink, 28);
            UiFactory.Place((RectTransform)_endTurnButton.transform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                            new Vector2(-50f, 200f), new Vector2(230f, 76f));
            _endTurnButton.onClick.AddListener(OnEndTurnClicked);

            _turnLabel = UiFactory.Label(_root, "Turn", "", 22, Palette.InkMuted, TextAnchor.UpperRight);
            UiFactory.Place(_turnLabel.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                            new Vector2(-40f, -30f), new Vector2(300f, 30f));

            // Split to the two bottom corners. A single label on the left sat directly under
            // the hand, and the lowest cards covered it.
            _drawLabel = UiFactory.Label(_root, "DrawPile", "", 19, Palette.InkMuted, TextAnchor.LowerLeft);
            UiFactory.Place(_drawLabel.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                            new Vector2(40f, 30f), new Vector2(220f, 26f));

            _discardLabel = UiFactory.Label(_root, "DiscardPile", "", 19, Palette.InkMuted, TextAnchor.LowerRight);
            UiFactory.Place(_discardLabel.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f),
                            new Vector2(-40f, 30f), new Vector2(260f, 26f));

            _seedLabel = UiFactory.Label(_root, "Seed", "", 17, new Color(0.4f, 0.4f, 0.46f), TextAnchor.UpperLeft);
            UiFactory.Place(_seedLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(40f, -30f), new Vector2(400f, 26f));
        }

        void BuildOverlay()
        {
            _overlay = UiFactory.Panel(_root, "Overlay", new Color(0.05f, 0.05f, 0.07f, 0.88f));
            UiFactory.Stretch(_overlay);

            _overlayLabel = UiFactory.Label(_overlay, "Result", "", 72, Palette.Ink);
            UiFactory.Place(_overlayLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, 60f), new Vector2(900f, 110f));

            var again = UiFactory.TextButton(_overlay, "Again", "New Run", Palette.PanelRaised, Palette.Ink, 30);
            UiFactory.Place((RectTransform)again.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, -70f), new Vector2(260f, 78f));
            again.onClick.AddListener(StartNewCombat);

            _overlay.gameObject.SetActive(false);
        }

        // ── Combat lifecycle ─────────────────────────────────────────────────────────

        void StartNewCombat()
        {
            if (_config == null)
            {
                Debug.LogError("[EmberDeck] CombatView has no RunConfig assigned.");
                enabled = false;
                return;
            }

            _session?.End();
            ClearChildren(_enemyRow);
            ClearChildren(_handRow);
            _enemyViews.Clear();
            _cardViews.Clear();
            _selectedCard = null;
            _overlay.gameObject.SetActive(false);

            int seed = _useRandomSeed ? Random.Range(int.MinValue, int.MaxValue) : _fixedSeed;
            _session = new CombatSession(_config, seed);

            _session.State.Bus.Subscribe<CombatStateChangedEvent>(OnStateChanged);
            _session.State.Bus.Subscribe<CombatEndedEvent>(OnCombatEnded);

            _session.Begin();

            BuildEnemyViews();
            _seedLabel.text = $"seed {seed}";
            Redraw();
        }

        void BuildEnemyViews()
        {
            float spacing = 300f;
            float startX = -(State.Enemies.Count - 1) * spacing * 0.5f;

            for (int i = 0; i < State.Enemies.Count; i++)
            {
                var view = EnemyView.Create(_enemyRow, State.Enemies[i]);
                UiFactory.Place((RectTransform)view.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                new Vector2(startX + i * spacing, 0f), new Vector2(260f, 300f));
                view.Clicked += OnEnemyClicked;
                _enemyViews.Add(view);
            }
        }

        void OnStateChanged(CombatStateChangedEvent _) => Redraw();

        void OnCombatEnded(CombatEndedEvent evt)
        {
            _overlay.gameObject.SetActive(true);
            _overlayLabel.text = evt.PlayerWon ? "VICTORY" : "DEFEAT";
            _overlayLabel.color = evt.PlayerWon ? Palette.Victory : Palette.Defeat;
        }

        // ── Input ────────────────────────────────────────────────────────────────────

        void OnCardClicked(CardView view)
        {
            if (_session == null || State.IsOver) return;

            // Cards that need no target play on the first click; cards that do are selected
            // first and then aimed. One interaction model, no modes to explain.
            if (view.Card.Data.Target == TargetMode.SingleEnemy)
            {
                _selectedCard = _selectedCard == view ? null : view;
                Redraw();
                return;
            }

            Engine.TryPlayCard(view.Card, State.Player);
            _selectedCard = null;
            Redraw();
        }

        void OnEnemyClicked(EnemyView view)
        {
            if (_session == null || State.IsOver || _selectedCard == null || !view.Enemy.IsAlive) return;

            Engine.TryPlayCard(_selectedCard.Card, view.Enemy);
            _selectedCard = null;
            Redraw();
        }

        void OnEndTurnClicked()
        {
            if (_session == null || State.IsOver) return;
            _selectedCard = null;
            Engine.EndPlayerTurn();
            Redraw();
        }

        // ── Rendering ────────────────────────────────────────────────────────────────

        void Redraw()
        {
            if (_session == null) return;

            SyncHand();

            bool targeting = _selectedCard != null;
            foreach (var enemyView in _enemyViews)
                enemyView.Refresh(targeting);

            var player = State.Player;
            UiFactory.SetBarFill(_playerHealthFill, player.MaxHp > 0 ? (float)player.Hp / player.MaxHp : 0f);
            _playerHealthLabel.text = $"{player.Hp} / {player.MaxHp}";
            _playerBlockBadge.gameObject.SetActive(player.Block > 0);
            _playerBlockLabel.text = player.Block.ToString();
            _playerStatusLabel.text = EnemyView.DescribeStatuses(player);

            _energyLabel.text = $"{State.Energy}/{State.EnergyPerTurn}";
            _turnLabel.text = $"Turn {State.TurnNumber}";
            _drawLabel.text = $"Draw {State.DrawPile.Count}";
            _discardLabel.text = $"Discard {State.DiscardPile.Count}" +
                                 (State.ExhaustPile.Count > 0 ? $"    Exhaust {State.ExhaustPile.Count}" : "");
            _endTurnButton.interactable = !State.IsOver;

            foreach (var cardView in _cardViews)
            {
                var card = cardView.Card;
                int cost = Engine.GetCardCost(card);
                bool playable = !State.IsOver && cost <= State.Energy;
                cardView.Refresh(playable, cardView == _selectedCard, cost);
            }
        }

        /// <summary>
        /// Rebuilds the hand only when its contents actually changed. Recreating every card
        /// object each redraw would throw away the selection and make any future animation
        /// impossible — the view has to keep identity across frames.
        /// </summary>
        void SyncHand()
        {
            bool matches = _cardViews.Count == State.Hand.Count;
            if (matches)
            {
                for (int i = 0; i < _cardViews.Count; i++)
                {
                    if (_cardViews[i].Card == State.Hand[i]) continue;
                    matches = false;
                    break;
                }
            }
            if (matches) { LayoutHand(); return; }

            var selectedInstance = _selectedCard != null ? _selectedCard.Card : null;
            if (selectedInstance != null && !State.Hand.Contains(selectedInstance))
                selectedInstance = null;

            ClearChildren(_handRow);
            _cardViews.Clear();

            foreach (var card in State.Hand)
            {
                var view = CardView.Create(_handRow, card);
                view.Clicked += OnCardClicked;
                _cardViews.Add(view);
            }

            _selectedCard = selectedInstance != null
                ? _cardViews.Find(view => view.Card == selectedInstance)
                : null;

            LayoutHand();
        }

        void LayoutHand()
        {
            int count = _cardViews.Count;
            if (count == 0) return;

            // Cards overlap once the hand grows, and fan slightly. The fan is not decoration:
            // it keeps every card's top edge visible, so a ten-card hand stays readable.
            float step = Mathf.Min(CardView.Width + 16f, 1100f / Mathf.Max(1, count));
            float startX = -(count - 1) * step * 0.5f;

            for (int i = 0; i < count; i++)
            {
                float centred = count > 1 ? (i - (count - 1) * 0.5f) / ((count - 1) * 0.5f) : 0f;

                // Arc UPWARD from the ends rather than dropping the ends downward, so no card
                // ever sits lower than the row itself and the bottom edge stays predictable.
                float lift = (1f - Mathf.Abs(centred)) * 22f;
                var rect = (RectTransform)_cardViews[i].transform;

                rect.SetSiblingIndex(i);
                // Shallow: past about 3 degrees the rules text becomes noticeably harder to read.
                rect.localRotation = Quaternion.Euler(0f, 0f, -centred * 3f);
                _cardViews[i].SetRestPosition(new Vector2(startX + i * step, lift));
            }
        }

        static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i);
                // Destroy only takes effect at the end of the frame. Detaching now keeps
                // sibling indices correct for objects created later in this same frame.
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
        }

        void OnDestroy() => _session?.End();
    }
}
