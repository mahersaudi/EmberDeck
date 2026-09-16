using System.Collections.Generic;
using EmberDeck.Combat;
using EmberDeck.Content;
using EmberDeck.Run;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
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
        RunState _run;
        MapView _mapView;
        RestView _restView;
        ShopView _shopView;
        EventView _eventView;
        MainMenuView _mainMenu;
        DeckBuilderView _deckBuilder;
        SettingsView _settings;
        PauseMenuView _pause;
        EndOfRunView _endOfRun;
        bool _settingsFromPause;
        Text _restLabel;
        RectTransform _rewardPanel;
        Text _rewardTitle;
        Text _relicLabel;
        readonly System.Collections.Generic.List<CardView> _rewardViews = new();
        CombatState State => _session.State;
        CombatEngine Engine => _session.Engine;

        RectTransform _root;
        Image _background;
        RectTransform _enemyRow;
        RectTransform _handRow;

        readonly List<EnemyView> _enemyViews = new();

        RectTransform _fxLayer;
        RectTransform _playerPanel;
        Image _playerFlash;
        CardInstance _lastPlayedCard;

        readonly List<(Image frame, Image icon)> _potionSlots = new();
        int _selectedPotion = -1;

        // Hand-row coordinates for cards entering and leaving: the two pile labels, and the board.
        static readonly Vector2 DrawPilePoint = new(-820f, -170f);
        static readonly Vector2 DiscardPilePoint = new(820f, -170f);
        static readonly Vector2 PlayedPoint = new(0f, 330f);
        readonly List<CardView> _cardViews = new();
        CardView _selectedCard;
        CardView _dragCard;

        /// <summary>What the last card played was aimed at, so the card can be thrown at it.</summary>
        RectTransform _lastPlayTarget;

        Image _playerHealthFill;
        Text _playerHealthLabel;
        Text _playerBlockLabel;
        Image _playerBlockBadge;
        StatusStrip _playerStatuses;
        Text _energyLabel;
        Image _heatFill;
        Text _heatLabel;
        Image _heatTrack;
        Text _turnLabel;
        Text _drawLabel;
        Text _discardLabel;
        Text _seedLabel;
        Text _runLabel;
        Button _endTurnButton;
        RectTransform _heatPanel;
        int _cardsPlayedThisFight;
        RunResult _lastResult;
        Language _builtLanguage;

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
            // The interface is built in one language. Changing it reloads the scene (see BuildMenus), and the tip
            // canvas, which survives scene loads, is rebuilt with it.
            _builtLanguage = Loc.Language;
            Coach.Rebuild();
            EnsureEventSystem();
            BuildStaticUi();
            BuildMenus();
            BuildNavigation();
            ShowMainMenu();
        }

        void Update()
        {
            // Time spent in a run, not in its menus.
            if (_run != null && !_mainMenu.IsOpen && !_pause.IsOpen && !_endOfRun.IsOpen && !_settings.IsOpen)
                _run.Stats.Seconds += Time.unscaledDeltaTime;

            // Escape, and B on a pad, arrive through PadNavigator: see OnNavCancel.
        }

        void BuildMenus()
        {
            Sprite backdrop = _config != null && _config.BossEncounter.Count > 0 && _config.BossEncounter[0] != null
                ? _config.BossEncounter[0].Art
                : null;

            _mainMenu = MainMenuView.Create(_root, backdrop);
            _mainMenu.ContinueChosen += () => BeginRun(resume: true);
            _mainMenu.NewRunChosen += OpenDeckBuilder;

            // Every new run goes through the builder: the thirty cards it starts with are the run's
            // first and largest decision, and there is no default worth taking it away for.
            _deckBuilder = DeckBuilderView.Create(_root);
            _deckBuilder.Cancelled += ShowMainMenu;
            _deckBuilder.Confirmed += deck => Curtain.Wipe(() =>
            {
                _deckBuilder.Hide();
                Profile.SetDeck(DeckBuilder.Ids(deck));
                BeginRun(resume: false, deck);
            });
            _mainMenu.SettingsChosen += () => OpenSettings(fromPause: false);
            _mainMenu.QuitChosen += () => Application.Quit();

            _pause = PauseMenuView.Create(_root);
            _pause.Resumed += () => _pause.Hide();
            _pause.SettingsChosen += () => OpenSettings(fromPause: true);
            _pause.MainMenuChosen += ShowMainMenu;
            _pause.QuitChosen += () => Application.Quit();

            _endOfRun = EndOfRunView.Create(_root);
            _endOfRun.NewRunChosen += OpenDeckBuilder;
            _endOfRun.MainMenuChosen += ShowMainMenu;

            _settings = SettingsView.Create(_root);
            _settings.Closed += () =>
            {
                if (Loc.Language != _builtLanguage)
                {
                    SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
                    return;
                }
                if (_settingsFromPause) _pause.Show();
            };

            Coach.Suspended = () => _mainMenu.IsOpen || _pause.IsOpen || _settings.IsOpen || _endOfRun.IsOpen
                                    || _deckBuilder.IsOpen;
        }

        /// <summary>
        /// Leaves whatever is on screen and shows the main menu. Nothing is saved here: the run on
        /// disk is the one written at the last map, which is what Continue will offer.
        /// </summary>
        void ShowMainMenu() => Curtain.Wipe(ShowMainMenuNow);

        void ShowMainMenuNow()
        {
            _deckBuilder.Hide();
            _pause.Hide();
            if (_settings.IsOpen) _settings.gameObject.SetActive(false);
            Tooltip.Hide();
            Coach.EndScreen();

            _session?.End();
            _session = null;
            ClearChildren(_enemyRow);
            ClearChildren(_handRow);
            _enemyViews.Clear();
            _cardViews.Clear();
            _selectedCard = null;
            _dragCard = null;
            _lastPlayTarget = null;
            _mapView?.Hide();
            _restView?.Hide();
            _shopView?.Hide();
            _eventView?.Hide();
            _endOfRun?.Hide();
            _rewardPanel.gameObject.SetActive(false);
            _run = null;

            string detail = null;
            if (_config != null)
            {
                var saved = RunSave.Read(_config);
                if (saved != null)
                    detail = $"Act {saved.Act}    Fight {saved.FightNumber}    {saved.Hp}/{saved.MaxHp} HP    {saved.Deck.Count} cards";
            }

            _mainMenu.SetProgress(_config);
            _mainMenu.Show(detail);
            AudioDirector.PlayMusic(MusicTrack.Map);
        }

        // ── Keyboard and gamepad ─────────────────────────────────────────────────────

        void BuildNavigation()
        {
            var nav = PadNavigator.Create();
            nav.Scope = NavScope;
            nav.InCombat = () => _session != null && !State.IsOver && NavScope() == _root;
            nav.Cancelled += OnNavCancel;
            nav.PauseRequested += () =>
            {
                if (_settings.IsOpen) _settings.Close();
                else TogglePause();
            };
            nav.EndTurnRequested += () =>
            {
                AudioDirector.Play(Sfx.Click);
                OnEndTurnClicked();
            };
        }

        /// <summary>The screen in front: the one keyboard and pad focus may move within.</summary>
        Transform NavScope()
        {
            if (_settings.IsOpen) return _settings.transform;
            if (_deckBuilder.IsOpen) return _deckBuilder.transform;
            if (_pause.IsOpen) return _pause.transform;
            if (_mainMenu.IsOpen) return _mainMenu.transform;
            if (_endOfRun.IsOpen) return _endOfRun.transform;
            if (_rewardPanel.gameObject.activeSelf) return _rewardPanel;
            if (_eventView.IsOpen) return _eventView.transform;
            if (_shopView.gameObject.activeSelf) return _shopView.transform;
            if (_restView.gameObject.activeSelf) return _restView.transform;
            if (_mapView.gameObject.activeSelf) return _mapView.transform;
            return _root;
        }

        /// <summary>
        /// Back where the screen has no Back button of its own. Mid-aim, it puts the card or potion down and
        /// returns focus to it; otherwise it opens or closes the pause menu, as Escape always has.
        /// </summary>
        void OnNavCancel()
        {
            if (_session != null && NavScope() == _root && (_selectedCard != null || _selectedPotion >= 0))
            {
                var card = _selectedCard;
                int potion = _selectedPotion;
                _selectedCard = null;
                _selectedPotion = -1;
                AudioDirector.Play(Sfx.Click, 0.6f);
                Redraw();
                if (card != null) PadNavigator.Focus(card.gameObject);
                else if (potion >= 0 && potion < _potionSlots.Count) PadNavigator.Focus(_potionSlots[potion].frame.gameObject);
                return;
            }
            if (_mainMenu.IsOpen || _endOfRun.IsOpen) return;
            TogglePause();
        }

        GameObject FirstLivingEnemy()
        {
            foreach (var view in _enemyViews)
                if (view != null && view.Enemy.IsAlive) return view.gameObject;
            return null;
        }

        /// <summary>After a play, focus the card that now sits where the played one was, or End Turn when the hand is empty.</summary>
        void FocusHand(int index)
        {
            if (!PadNavigator.Active) return;
            var hand = _cardViews.FindAll(v => v != null && !v.IsLeaving);
            PadNavigator.Focus(hand.Count > 0 ? hand[Mathf.Clamp(index, 0, hand.Count - 1)].gameObject : _endTurnButton.gameObject);
        }

        /// <summary>Opens or closes the pause menu during a run. Public so the capture harness can photograph it.</summary>
        public void TogglePause()
        {
            if (_mainMenu.IsOpen || _endOfRun.IsOpen || _run == null) return;
            if (_pause.IsOpen)
            {
                _pause.Hide();
                return;
            }
            Tooltip.Hide();
            _pause.Show();
        }

        void OpenSettings(bool fromPause)
        {
            _settingsFromPause = fromPause;
            _pause.Hide();
            _settings.Show(allowLanguage: !fromPause);
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
            UiFactory.ConfigureScaler(scaler);

            // Everything hangs off a stage rather than the canvas itself, so a right-to-left language can mirror the
            // whole layout with one scale: the run line moves to the top right, the hand deals from the right, bars
            // fill leftwards. The canvas's own scale belongs to its CanvasScaler. Text is mirrored back where it is
            // drawn (UiText), so it still reads forwards.
            var stage = (RectTransform)new GameObject("Stage", typeof(RectTransform)).transform;
            stage.SetParent(canvasGo.transform, false);
            UiFactory.Stretch(stage);
            if (Loc.IsRtl) stage.localScale = new Vector3(-1f, 1f, 1f);
            if (TouchMode.Active) stage.gameObject.AddComponent<SafeArea>();
            _root = stage;

            var background = UiFactory.Panel(_root, "Background", Palette.Background);
            UiFactory.Stretch(background);
            _background = background.GetComponent<Image>();

            _enemyRow = UiFactory.Panel(_root, "EnemyRow", new Color(0f, 0f, 0f, 0f));
            UiFactory.Place(_enemyRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -150f), new Vector2(1400f, 320f));

            _handRow = UiFactory.Panel(_root, "Hand", new Color(0f, 0f, 0f, 0f));
            UiFactory.Place(_handRow, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 40f), new Vector2(1500f, 330f));

            BuildPlayerPanel();
            BuildHud();

            // Above the board, below the overlay and the map: floating numbers must never hide
            // behind the enemy they belong to, and never float over a reward screen.
            _fxLayer = UiFactory.Panel(_root, "Effects", new Color(0f, 0f, 0f, 0f));
            UiFactory.Stretch(_fxLayer);
            _fxLayer.GetComponent<Image>().raycastTarget = false;

            BuildScreens();
        }

        void BuildPlayerPanel()
        {
            var panel = UiFactory.Panel(_root, "PlayerPanel", Palette.PanelDark);
            _playerPanel = panel;
            UiFactory.Frame(panel.GetComponent<Image>(), "frame_panel", 4f);
            TooltipTrigger.Attach(panel.gameObject, DescribePlayer);
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

            _playerStatuses = StatusStrip.Create(panel, "Statuses", TextAnchor.MiddleLeft);
            UiFactory.Place((RectTransform)_playerStatuses.transform, new Vector2(0f, 0f), new Vector2(0f, 0f),
                            new Vector2(14f, 8f), new Vector2(290f, 30f));

            // Created last so it draws over everything in the panel.
            var flash = UiFactory.Panel(panel, "HitFlash", new Color(1f, 0.2f, 0.2f, 0f));
            UiFactory.Stretch(flash);
            _playerFlash = flash.GetComponent<Image>();
            _playerFlash.raycastTarget = false;
        }

        void BuildHud()
        {
            var energyOrb = UiFactory.Panel(_root, "EnergyOrb", Palette.Energy);
            TooltipTrigger.Attach(energyOrb.gameObject, () => new[]
            {
                Tooltip.Entry.From(Keywords.Find("Energy"),
                                   _session != null ? $"Energy {State.Energy}/{State.EnergyPerTurn}" : "Energy")
            });
            // The bottom-left column is energy, then heat, then the player panel, stacked with
            // a gap. The orb sat at y=190 and its 96px height ran into the heat panel above.
            UiFactory.Place(energyOrb, new Vector2(0f, 0f), new Vector2(0f, 0f),
                            new Vector2(70f, 148f), new Vector2(96f, 96f));
            _energyLabel = UiFactory.Label(energyOrb, "EnergyText", "", 38, Palette.Background);
            UiFactory.Stretch(_energyLabel.rectTransform);

            // Heat has to be on screen at all times. It accumulates across turns and costs
            // HP past the threshold, so a player who cannot see it is being charged for a
            // decision the game never showed them.
            var heatPanel = UiFactory.Panel(_root, "HeatPanel", Palette.PanelDark);
            _heatPanel = heatPanel;
            UiFactory.Frame(heatPanel.GetComponent<Image>(), "frame_panel", 4f);
            TooltipTrigger.Attach(heatPanel.gameObject, DescribeHeat);
            UiFactory.Place(heatPanel, new Vector2(0f, 0f), new Vector2(0f, 0f),
                            new Vector2(40f, 258f), new Vector2(320f, 62f));

            var heatTitle = UiFactory.Label(heatPanel, "HeatTitle", "HEAT", 15, Palette.InkMuted, TextAnchor.UpperLeft);
            UiFactory.Place(heatTitle.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(12f, -6f), new Vector2(120f, 20f));

            _heatFill = UiFactory.Bar(heatPanel, "HeatBar", Palette.HeatTrack, Palette.Energy,
                                      new Vector2(292f, 22f), new Vector2(0f, -10f));
            _heatTrack = _heatFill.transform.parent.GetComponent<Image>();

            _heatLabel = UiFactory.Label(heatPanel, "HeatText", "", 17, Palette.Ink);
            UiFactory.Place(_heatLabel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                            new Vector2(0f, -10f), new Vector2(292f, 22f));

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

            _runLabel = UiFactory.Label(_root, "Run", "", 22, Palette.Ink, TextAnchor.UpperLeft);
            // 700 wide: with the act added, 520 wrapped the map's "HP 63/63" onto a second line, and the Arabic
            // font's wider letters wrapped it again at 640. The map title is centred and starts past 740.
            UiFactory.Place(_runLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(40f, -56f), new Vector2(700f, 30f));

            _seedLabel = UiFactory.Label(_root, "Seed", "", 17, new Color(0.4f, 0.4f, 0.46f), TextAnchor.UpperLeft);
            UiFactory.Place(_seedLabel.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f),
                            new Vector2(40f, -30f), new Vector2(400f, 26f));

            if (TouchMode.Active)
            {
                var menu = UiFactory.TextButton(_root, "TouchMenu", "Menu", Palette.PanelRaised, Palette.Ink, 26);
                UiFactory.Place((RectTransform)menu.transform, new Vector2(1f, 1f), new Vector2(1f, 1f),
                                new Vector2(-40f, -80f), new Vector2(170f, 76f));
                menu.onClick.AddListener(TogglePause);
            }

            BuildPotionBelt();
        }

        void BuildScreens()
        {
            BuildRewardPanel();

            _mapView = MapView.Create(_root);
            _mapView.NodeChosen += OnNodeChosen;

            _restView = RestView.Create(_root);
            _restView.HealChosen += OnRestHeal;
            _restView.UpgradeChosen += OnRestUpgrade;

            _shopView = ShopView.Create(_root);
            _shopView.Left += ShowMap;

            _eventView = EventView.Create(_root);
            _eventView.Left += ShowMap;
        }

        /// <summary>
        /// The pick-one-of-three screen. This is the moment a sequence of fights becomes a
        /// run: it is the first decision that outlives the combat it was made in.
        /// </summary>
        void BuildRewardPanel()
        {
            _rewardPanel = UiFactory.Panel(_root, "Rewards", new Color(0.05f, 0.05f, 0.07f, 0.98f));
            UiFactory.Stretch(_rewardPanel);

            _rewardTitle = UiFactory.Label(_rewardPanel, "RewardTitle", "", 44, Palette.Victory);
            UiFactory.Place(_rewardTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -120f), new Vector2(1000f, 60f));

            var hint = UiFactory.Label(_rewardPanel, "RewardHint", "Choose one card to add to your deck",
                                       22, Palette.InkMuted);
            UiFactory.Place(hint.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -178f), new Vector2(1000f, 34f));

            _relicLabel = UiFactory.Label(_rewardPanel, "RelicGained", "", 22, Palette.RarityRare);
            UiFactory.Place(_relicLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -222f), new Vector2(1100f, 34f));

            var skip = UiFactory.TextButton(_rewardPanel, "Skip", "Skip", Palette.PanelRaised, Palette.InkMuted, 24);
            UiFactory.Place((RectTransform)skip.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 90f), new Vector2(200f, 62f));
            // Skipping is a real option: a deck that never refuses a card drowns its own
            // good cards in filler, and learning that is part of the genre.
            skip.onClick.AddListener(() => TakeReward(null));

            _rewardPanel.gameObject.SetActive(false);
        }

        // ── Combat lifecycle ─────────────────────────────────────────────────────────

        /// <summary>The deck builder, opened on the last deck played so a second run is one tap from the first.</summary>
        void OpenDeckBuilder()
        {
            if (_config == null) return;
            var profile = Profile.Data;
            var unlocked = UnlockService.UnlockedIds(_config, profile.embers);

            Curtain.Wipe(() =>
            {
                _mainMenu.Hide();
                _endOfRun.Hide();
                Coach.EndScreen();
                _deckBuilder.Show(_config, unlocked, DeckBuilder.Resolve(_config, profile.deck, unlocked));
            });
        }

        void BeginRun(bool resume, List<CardData> deck = null)
        {
            _deckBuilder.Hide();
            _mainMenu.Hide();
            _pause.Hide();
            _endOfRun.Hide();
            if (!resume) RunSave.Delete();

            if (_config == null)
            {
                Debug.LogError("[EmberDeck] CombatView has no RunConfig assigned.");
                enabled = false;
                return;
            }

            var resumed = resume ? RunSave.Read(_config) : null;
            if (resumed != null)
            {
                Debug.Log($"[EmberDeck] Resumed run: fight {resumed.FightNumber}, "
                          + $"{resumed.Deck.Count} cards, {resumed.Hp}/{resumed.MaxHp} HP.");
                _run = resumed;
            }
            else
            {
                int runSeed = _useRandomSeed ? Random.Range(int.MinValue, int.MaxValue) : _fixedSeed;
                var profile = Profile.Data;
                _run = RunState.Start(_config, runSeed, Mathf.Clamp(profile.difficulty, 0, profile.maxDifficulty),
                                      UnlockService.UnlockedIds(_config, profile.embers), deck);
            }

            ShowMap();
        }

        /// <summary>Starts the next fight of the current run, keeping deck and health.</summary>
        void StartFight(EncounterData forced = null)
        {
            _session?.End();
            ClearChildren(_enemyRow);
            ClearChildren(_handRow);
            _enemyViews.Clear();
            _cardViews.Clear();
            _selectedCard = null;
            _selectedPotion = -1;
            _endOfRun?.Hide();
            _cardsPlayedThisFight = 0;

            // Queued before anything else so they come first; potion and Heat tips can be raised by the
            // redraws inside Begin.
            Coach.EndScreen();
            Coach.Show("hand", "Play your cards",
                       $"Each card costs the Energy in its corner, and you get {_config.EnergyPerTurn} Energy every turn "
                       + "(the orb, bottom left). "
                       + (TouchMode.Active
                            ? "Tap a card to read it, then tap an enemy to attack. A card with no target, like Guard, "
                              + "plays when you tap it again."
                            : "To attack, click a card and then click an enemy. Cards with no target, like Guard, "
                              + "play on one click."),
                       HandRects, Coach.Side.Above);
            Coach.Show("intent", "Read the enemy",
                       "The icon above an enemy is its next move. A red number is damage coming at you; a shield "
                       + "means it will gain Block. "
                       + (TouchMode.Active
                            ? "Tap an enemy to see exactly what it will do."
                            : "Hover over an enemy to see exactly what it will do."),
                       IntentRects, Coach.Side.Right);

            // One stream per fight, derived from the run seed, so a run replays exactly.
            int seed = _run.Seed ^ (_run.FightNumber * unchecked((int)0x9E3779B1));
            _session = new CombatSession(_config, seed, _run) { ForcedEncounter = forced };

            _session.State.Bus.Subscribe<CombatStateChangedEvent>(OnStateChanged);
            _session.State.Bus.Subscribe<CombatEndedEvent>(OnCombatEnded);

            _session.Begin();

            BuildEnemyViews();

            _session.State.Bus.Subscribe<CardPlayedEvent>(e => _lastPlayedCard = e.Card);
            _session.State.Bus.Subscribe<CardPlayedEvent>(_ =>
            {
                _cardsPlayedThisFight++;
                Coach.Complete("hand");
            });
            // Attached after Begin so the views it animates exist. Nothing needs an effect
            // before the first turn: the opening hand already deals itself in.
            new CombatFeedback(_session.State, _fxLayer, _root, AnchorFor, FlashFor, ViewFor);
            TrackStats(_session.State);
            _run.Stats.FinalEncounter = DescribeEncounter(_session.State);
            AudioDirector.PlayMusic(_run.IsBoss ? MusicTrack.Boss : MusicTrack.Combat);
            var tier = forced != null ? forced.Tier
                     : _run.IsBoss ? EncounterTier.Boss
                     : _run.IsElite ? EncounterTier.Elite
                     : EncounterTier.Early;
            SetBattlefield(tier == EncounterTier.Boss ? "bg_boss" : tier == EncounterTier.Elite ? "bg_elite" : "bg_hallway",
                           forced != null ? forced.Act : _run.Act);
            AudioDirector.Play(Sfx.TurnStart, 0.8f);
            _seedLabel.text = $"seed {_run.Seed}";
            _runLabel.text = $"Fight {_run.FightNumber}    Deck {_run.Deck.Count}    Relics {_run.Relics.Count}    Gold {_run.Gold}";
            Redraw();
        }

        /// <summary>Counts what the end-of-run screen reports. Reads combat, never changes it — like CombatFeedback.</summary>
        void TrackStats(CombatState state)
        {
            var stats = _run.Stats;
            stats.Turns++;   // the first turn began inside Begin, before anything here could subscribe
            state.Bus.Subscribe<TurnStartedEvent>(e => { if (e.IsPlayerTurn) stats.Turns++; });
            state.Bus.Subscribe<CardPlayedEvent>(_ => stats.CardsPlayed++);
            state.Bus.Subscribe<ActorDiedEvent>(e => { if (!e.Actor.IsPlayer) stats.EnemiesDefeated++; });
            state.Bus.Subscribe<DamageAppliedEvent>(e =>
            {
                if (e.HpLost <= 0 || e.Target == null) return;
                if (e.Target.IsPlayer)
                {
                    stats.DamageTaken += e.HpLost;
                    return;
                }
                // Burn and other sourceless loss on an enemy is still the player's damage; only a
                // direct hit counts as a "hit".
                stats.DamageDealt += e.HpLost;
                if (e.Source != null && e.Source.IsPlayer) stats.BiggestHit = Mathf.Max(stats.BiggestHit, e.HpLost);
            });
        }

        static string DescribeEncounter(CombatState state)
        {
            var names = new List<string>();
            foreach (var enemy in state.Enemies)
                if (!names.Contains(enemy.Name)) names.Add(enemy.Name);
            return string.Join(" and ", names);
        }

        void ShowEndOfRun(bool won) => Curtain.Wipe(() => ShowEndOfRunNow(won));

        void ShowEndOfRunNow(bool won)
        {
            if (_run == null) return;
            Tooltip.Hide();
            Coach.EndScreen();
            _pause.Hide();
            _rewardPanel.gameObject.SetActive(false);

            // Floors count through every act, the boss included: Act 2's first row is floor 11 of 20.
            int perAct = (_run.Map?.Grid.Count ?? RunMap.Rows) + 1;
            int floor = (_run.Act - 1) * perAct + (_run.ActiveNode?.Row ?? 0) + 1;
            int floors = perAct * Mathf.Max(1, _config.Acts);

            // Into the profile once per run, however many times its summary is shown.
            if (!_run.ResultRecorded)
            {
                _lastResult = Profile.RecordRun(_run, _config, won, floor);
                PlaytestLog.RecordRun(_run, won, floor, floors, Loc.Language.ToString());
                _run.ResultRecorded = true;
            }
            _endOfRun.Show(_run, won, floor, floors, _lastResult);
            AudioDirector.PlayMusic(won ? MusicTrack.Map : MusicTrack.None);
        }

        IReadOnlyList<Tooltip.Entry> DescribePlayer()
        {
            var entries = new List<Tooltip.Entry>();
            if (_session == null) return entries;

            var player = State.Player;
            entries.Add(new Tooltip.Entry($"{_config.PlayerName}  {player.Hp}/{player.MaxHp} HP",
                                          "Health carries over from fight to fight. Rest sites restore it.",
                                          null, Palette.Health));
            if (player.Block > 0)
                entries.Add(Tooltip.Entry.From(Keywords.Find("Block"), $"Block {player.Block}"));

            foreach (StatusType status in System.Enum.GetValues(typeof(StatusType)))
            {
                int value = player.GetStatus(status);
                var keyword = Keywords.For(status);
                if (value != 0 && keyword != null)
                    entries.Add(Tooltip.Entry.From(keyword, $"{keyword.Word} {value}"));
            }
            return entries;
        }

        /// <summary>Heat with its real numbers: the threshold now, or exactly what overheating will cost.</summary>
        IReadOnlyList<Tooltip.Entry> DescribeHeat()
        {
            var entries = new List<Tooltip.Entry>();
            if (_session == null) return entries;

            entries.Add(Tooltip.Entry.From(Keywords.Find("Heat"), $"Heat {State.Heat}"));
            int threshold = State.OverheatThreshold;
            string title = State.Heat > threshold
                ? $"Overheating: -{State.Heat - threshold} HP at end of turn"
                : $"Overheat threshold {threshold}";
            entries.Add(Tooltip.Entry.From(Keywords.Find("Overheat"), title));
            return entries;
        }

        /// <summary>
        /// Three potion slots under the run line, top-left. Potions are consulted every turn, and the
        /// corner is otherwise empty; putting them by the hand would crowd the one place a player
        /// already looks hardest.
        /// </summary>
        void BuildPotionBelt()
        {
            // A phone's 1080 rows are about 70mm of glass, so a 56-unit slot is a 3.5mm target — under a
            // fingertip. On touch the belt is drawn half again as large, which the empty corner has room for.
            float size = TouchMode.Active ? 84f : 56f;
            float step = TouchMode.Active ? 96f : 64f;

            for (int i = 0; i < PotionService.Slots; i++)
            {
                var slot = UiFactory.Panel(_root, $"PotionSlot{i}", Palette.PanelDark);
                UiFactory.Place(slot, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(40f + i * step, -94f), new Vector2(size, size));
                var icon = Icons.Create(slot, "Icon", null, size - 10f);
                UiFactory.Place((RectTransform)icon.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero,
                                new Vector2(size - 10f, size - 10f));
                var button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = slot.GetComponent<Image>();
                int index = i;
                button.onClick.AddListener(() => OnPotionClicked(index));
                TooltipTrigger.Attach(slot.gameObject, () => DescribePotionSlot(index));
                _potionSlots.Add((slot.GetComponent<Image>(), icon));
            }
        }

        void RefreshPotions()
        {
            for (int i = 0; i < _potionSlots.Count; i++)
            {
                var (frame, icon) = _potionSlots[i];
                var potion = _run != null && i < _run.Potions.Count ? _run.Potions[i] : null;
                Icons.SetSprite(icon, potion != null ? Icons.Get(potion.Icon) : null);
                frame.color = i == _selectedPotion ? Palette.CardSelected : Palette.PanelDark;
            }
        }

        void OnPotionClicked(int slot)
        {
            if (_session == null || State.IsOver || _run == null || slot >= _run.Potions.Count) return;

            var potion = _run.Potions[slot];
            if (potion.Target == TargetMode.SingleEnemy)
            {
                // Aimed like a card: select it, then click an enemy.
                _selectedPotion = _selectedPotion == slot ? -1 : slot;
                _selectedCard = null;
                AudioDirector.Play(Sfx.Click, 0.7f);
                Redraw();
                PadNavigator.Focus(_selectedPotion >= 0 ? FirstLivingEnemy() : _potionSlots[slot].frame.gameObject);
                return;
            }

            PotionService.Use(_run, slot, Engine, null);
            Coach.Complete("potion");
            _selectedPotion = -1;
            Redraw();
        }

        // What the first-run tips point at. Read every frame, so a ring follows cards as they move.
        IEnumerable<RectTransform> HandRects()
        {
            foreach (var view in _cardViews)
                if (view != null) yield return (RectTransform)view.transform;
        }

        IEnumerable<RectTransform> IntentRects()
        {
            foreach (var view in _enemyViews)
                if (view != null && view.Enemy.IsAlive) yield return view.IntentRect;
        }

        IEnumerable<RectTransform> PotionRects()
        {
            foreach (var (frame, _) in _potionSlots)
                yield return frame.rectTransform;
        }

        IEnumerable<RectTransform> RewardRects()
        {
            foreach (var view in _rewardViews)
                if (view != null) yield return (RectTransform)view.transform;
        }

        /// <summary>Tips that wait for their moment: the first Heat, the first potion, the first time nothing is playable.</summary>
        void RaiseCombatTips(bool anyPlayable)
        {
            if (State.IsOver) return;

            if (State.Heat > 0)
                Coach.Show("heat", "Heat",
                           "Fire cards build Heat, and it stays between turns. At the end of your turn you lose 1 HP "
                           + $"for each point of Heat above {State.OverheatThreshold}. Some cards spend Heat for a big effect.",
                           () => new[] { _heatPanel }, Coach.Side.Right);

            if (_run != null && _run.Potions.Count > 0)
                Coach.Show("potion", "Potions",
                           "Potions cost no Energy. "
                           + (TouchMode.Active
                                ? "Tap one to drink it; a potion that hits an enemy is aimed like an attack, by tapping "
                                  + "the enemy next. "
                                : "Click one to drink it; a potion that hits an enemy is aimed like an attack, by clicking "
                                  + "the enemy next. ")
                           + $"You can carry {PotionService.Slots}.",
                           PotionRects, Coach.Side.Below);   // beside them it covered the run line

            // Only after a card has been played: before the opening hand is dealt nothing is playable either.
            if (_cardsPlayedThisFight > 0 && !anyPlayable)
                Coach.Show("endturn", "End your turn",
                           "Nothing left to play? End the turn. Enemies act, then you draw a new hand with full Energy. "
                           + "Block you gained this turn protects you while they attack.",
                           () => new[] { (RectTransform)_endTurnButton.transform }, Coach.Side.Above);
        }

        IReadOnlyList<Tooltip.Entry> DescribePotionSlot(int slot)
        {
            if (_run == null || slot >= _run.Potions.Count)
                return new[] { new Tooltip.Entry("Empty potion slot", "Potions drop from fights and are sold in shops. Drinking one costs no energy.") };

            var potion = _run.Potions[slot];
            string how = potion.Target == TargetMode.SingleEnemy
                ? (TouchMode.Active ? "Tap it, then tap an enemy." : "Click it, then click an enemy.")
                : (TouchMode.Active ? "Tap to drink." : "Click to drink.");
            return new[] { new Tooltip.Entry(potion.DisplayName, $"{potion.BuildDescription()} {how} Costs no energy.", potion.Icon) };
        }

        /// <summary>
        /// The painted battlefield for this fight, darkened so cards and numbers stay the brightest thing
        /// on screen. Falls back to the flat background colour when the painting is missing.
        /// </summary>
        void SetBattlefield(string name, int act = 1)
        {
            // A later act's painting when it exists ("bg_boss_2"), otherwise Act 1's.
            var art = (act > 1 ? Resources.Load<Sprite>($"Backgrounds/{name}_{act}") : null)
                      ?? Resources.Load<Sprite>($"Backgrounds/{name}");
            _background.sprite = art;
            _background.color = art != null ? new Color(0.42f, 0.42f, 0.46f, 1f) : Palette.Background;
        }

        RectTransform AnchorFor(Actor actor)
        {
            if (actor == null) return null;
            if (actor.IsPlayer) return _playerPanel;
            foreach (var view in _enemyViews)
                if (view != null && view.Enemy == actor) return (RectTransform)view.transform;
            return null;
        }

        Graphic FlashFor(Actor actor)
        {
            if (actor == null) return null;
            if (actor.IsPlayer) return _playerFlash;
            foreach (var view in _enemyViews)
                if (view != null && view.Enemy == actor) return view.FlashGraphic;
            return null;
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
                // They walk on one after another, left to right, before the hand is dealt.
                view.Enter(0.05f + i * 0.12f);
                _enemyViews.Add(view);
            }
        }

        /// <summary>The view for an actor, for the effects that have to reach into it — a recoil, a death.</summary>
        EnemyView ViewFor(Actor actor)
        {
            foreach (var view in _enemyViews)
                if (view != null && view.Enemy == actor) return view;
            return null;
        }

        void OnStateChanged(CombatStateChangedEvent _) => Redraw();

        void ShowMap() => Curtain.Wipe(ShowMapNow);

        void ShowMapNow()
        {
            _restView?.Hide();
            _shopView?.Hide();
            _eventView?.Hide();
            Coach.EndScreen();
            _session?.End();
            _session = null;
            ClearChildren(_enemyRow);
            ClearChildren(_handRow);
            _enemyViews.Clear();
            _cardViews.Clear();
            _endOfRun?.Hide();
            _rewardPanel.gameObject.SetActive(false);
            _runLabel.text = $"Act {_run.Act}    Fight {_run.FightNumber}    Deck {_run.Deck.Count}    Relics {_run.Relics.Count}    Gold {_run.Gold}    HP {_run.Hp}/{_run.MaxHp}";
            _mapView.Show(_run.Map, _run.Act, _config.ActName(_run.Act), _config.BossOf(_run.Act)?.DisplayName,
                          finalAct: _run.Act >= _config.Acts);
            if (_run.Act > 1)
                Coach.Show("act2", "A deeper act",
                           "Beating the boss healed you to full. The enemies down here are stronger, and they grow "
                           + "again as you climb. Another boss waits at the top.",
                           null, Coach.Side.Below);
            Coach.Show("map", "The map",
                       "Climb from the bottom to the boss at the top. The glowing nodes are where you can go next; "
                       + (TouchMode.Active
                            ? "tap one to see what it holds, and tap it again to enter. "
                            : "hover over any node to see what it holds. ")
                       + "Fights give cards and gold, rest sites heal, and ? is an event.",
                       _mapView.AvailableNodes, Coach.Side.Left);
            AudioDirector.PlayMusic(MusicTrack.Map);
            // Saving here rather than on every state change means the file is only ever
            // written at a point the game can actually be restarted from.
            RunSave.Write(_run);

            // The map is opaque, so the run header has to be drawn after it. Health and deck
            // size are exactly what the choice between a Rest and an Elite turns on — hiding
            // them on the screen where that choice is made would be the worst place to hide
            // them.
            _runLabel.transform.SetAsLastSibling();
        }

        /// <summary>
        /// Each node type does something different, which is the whole point of a map: the
        /// choice is what kind of turn to spend, not merely which line to follow.
        /// </summary>
        void OnNodeChosen(MapNode node)
        {
            AudioDirector.Play(Sfx.MapSelect);
            Curtain.Wipe(() =>
            {
                Coach.EndScreen();
                _run.Map.Current = node;
                _run.ActiveNode = node;
                node.Visited = true;
                _mapView.Hide();

                switch (node.Type)
                {
                    case NodeType.Rest:
                        OpenRest();
                        break;

                    case NodeType.Shop:
                        OpenShop();
                        break;

                    case NodeType.Event:
                        _eventView.Show(EventService.Pick(_run), EventService.Context(_run, _config));
                        break;

                    case NodeType.Treasure:
                        // A free card with no fight attached, and some gold. The card is still a choice,
                        // and still skippable.
                        int treasureGold = GoldService.ForTreasure(_run);
                        GoldService.Earn(_run, treasureGold);
                        ShowRewards(title: "TREASURE", gold: treasureGold);
                        break;

                    default:
                        StartFight();
                        break;
                }
            });
        }

        void OpenShop()
        {
            _shopView.Show(_run, ShopService.Roll(_run, _config));
        }

        void OpenRest()
        {
            int heal = Mathf.RoundToInt(_run.MaxHp * DifficultyRules.RestHealFraction(_config, _run.Difficulty));
            _restView.Show(heal, _run.Hp, _run.MaxHp, _run.UpgradableCards());
            // Health is what the heal-or-upgrade choice turns on, so keep it visible.
            _runLabel.transform.SetAsLastSibling();
        }

        void OnRestHeal()
        {
            AudioDirector.Play(Sfx.Reward);
            _run.Heal(Mathf.RoundToInt(_run.MaxHp * DifficultyRules.RestHealFraction(_config, _run.Difficulty)));
            ShowMap();
        }

        void OnRestUpgrade(CardData card)
        {
            AudioDirector.Play(Sfx.Upgrade);
            if (_run.UpgradeCard(card)) _run.Stats.CardsUpgraded++;
            ShowMap();
        }

        void OnCombatEnded(CombatEndedEvent evt)
        {
            if (!evt.PlayerWon)
            {
                RunSave.Delete();
                AudioDirector.PlayMusic(MusicTrack.None);
                // The final blow and the defeat sting play out on the board before the summary covers it.
                var lost = _run;
                Motion.After(1.4f, () => { if (_run == lost) ShowEndOfRun(won: false); }, this);
                return;
            }

            // Carry the damage forward before anything else: the reward is chosen knowing
            // how much health survived it.
            _run.Hp = State.Player.Hp;
            _run.Stats.FightsWon++;
            if (_run.IsElite) _run.Stats.ElitesWon++;

            // Beating the last act's boss ends the run: there is no next fight for a card reward to matter in.
            // An earlier act's boss pays out like an elite and more, and the run goes on.
            if (_run.IsFinalBoss(_config))
            {
                RunSave.Delete();
                var won = _run;
                Motion.After(1.2f, () => { if (_run == won) ShowEndOfRun(won: true); }, this);
                return;
            }

            // Elites grant a relic on top of the card. Rolled before the card reward and before
            // the fight counter advances, in the same order RunSimulator uses.
            Content.Relics.RelicData relic = null;
            if (_run.IsElite || _run.IsBoss)
            {
                relic = RelicService.Roll(_run, _config);
                if (relic != null) _run.Relics.Add(relic);
            }

            int gold = GoldService.ForVictory(_run);
            GoldService.Earn(_run, gold);

            var potion = PotionService.RollDrop(_run, _config);
            bool potionKept = PotionService.TryAdd(_run, potion);

            ShowRewards(_run.IsBoss ? $"ACT {_run.Act} COMPLETE" : _run.IsElite ? "ELITE DEFEATED" : "VICTORY", relic, gold, potion, potionKept);
        }

        void ShowRewards(string title, Content.Relics.RelicData relic = null, int gold = 0,
                         PotionData potion = null, bool potionKept = false)
        {
            foreach (var view in _rewardViews)
                if (view != null) Destroy(view.gameObject);
            _rewardViews.Clear();

            var offers = RewardService.Roll(_run.RewardPool(_config), _run.RewardRng(),
                                            eliteOdds: _run.IsElite || _run.IsBoss || _run.ActiveNode?.Type == NodeType.Treasure);
            _rewardTitle.text = title;
            var gains = new List<string>();
            if (gold > 0) gains.Add($"+{gold} gold");
            if (potion != null) gains.Add(potionKept ? $"Potion: {potion.DisplayName}" : $"Found {potion.DisplayName}, but the belt is full");
            if (relic != null) gains.Add($"Relic gained: {relic.DisplayName}  —  {relic.Description}");
            _relicLabel.text = string.Join("      ", gains.ConvertAll(Loc.T));
            if (relic != null) Motion.After(0.5f, () => AudioDirector.Play(Sfx.Relic));

            float spacing = CardView.Width + 60f;
            float startX = -(offers.Count - 1) * spacing * 0.5f;

            for (int i = 0; i < offers.Count; i++)
            {
                var view = CardView.Create(_rewardPanel, new CardInstance(offers[i]));
                UiFactory.Place((RectTransform)view.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                Vector2.zero, new Vector2(CardView.Width, CardView.Height));

                // Refresh re-applies the card's rest position, so setting it through Place
                // alone leaves every reward stacked at the centre — three cards occupying
                // one spot, which reads as a single offer.
                view.SpawnAt(new Vector2(startX + i * spacing, -460f), 0.7f);   // dealt up from below
                view.SetRestPosition(new Vector2(startX + i * spacing, -10f));
                view.Refresh(playable: true, selected: false, displayedCost: offers[i].Cost);

                var chosen = offers[i];
                view.Clicked += _ => TakeReward(chosen);
                _rewardViews.Add(view);
            }

            _rewardPanel.gameObject.SetActive(true);

            Coach.EndScreen();
            Coach.Show("reward", "Choose a reward",
                       "Take one card, or Skip. A smaller deck draws its best cards more often, so only take cards "
                       + "that make it stronger.",
                       RewardRects, Coach.Side.Right);
        }

        void TakeReward(CardData card)
        {
            if (card != null) _run.Stats.CardsAdded++;
            _run.AddCard(card);
            AudioDirector.Play(Sfx.Reward);
            _rewardPanel.gameObject.SetActive(false);

            if (_run.IsFinalBoss(_config))
            {
                RunSave.Delete();
                ShowEndOfRun(won: true);
                return;
            }

            _run.FightNumber++;
            // After FightNumber advances, so the next act's first fight counts as its first.
            if (_run.IsBoss) _run.BeginNextAct();
            ShowMap();
        }

        // ── Input ────────────────────────────────────────────────────────────────────

        void OnCardClicked(CardView view)
        {
            if (_session == null || State.IsOver) return;
            _selectedPotion = -1;

            // Cards that need no target play on the first click; cards that do are selected
            // first and then aimed. One interaction model, no modes to explain.
            //
            // A touch screen takes one more tap for the untargeted ones: there is no hover, so the first
            // tap is how a player reads a card, and playing on it would spend a card nobody had read.
            if (view.Card.Data.Target == TargetMode.SingleEnemy)
            {
                _selectedCard = _selectedCard == view ? null : view;
                AudioDirector.Play(Sfx.Click, 0.7f);
                Redraw();
                // Aiming moves focus to the enemies, so the next press of A plays the card.
                PadNavigator.Focus(_selectedCard != null ? FirstLivingEnemy() : view.gameObject);
                return;
            }

            if (TouchMode.Active && _selectedCard != view)
            {
                _selectedCard = view;
                AudioDirector.Play(Sfx.Click, 0.7f);
                Redraw();
                return;
            }

            int index = _cardViews.IndexOf(view);
            _lastPlayTarget = null;
            Engine.TryPlayCard(view.Card, State.Player);
            _selectedCard = null;
            Redraw();
            FocusHand(index);
        }

        /// <summary>A card has been picked up: drop any other selection, and light up what it can hit.</summary>
        void OnCardDragStarted(CardView view)
        {
            if (_session == null || State.IsOver) return;
            _selectedPotion = -1;
            _selectedCard = null;
            _dragCard = view;
            Tooltip.Hide();
            Redraw();
        }

        /// <summary>
        /// A card let go. A card that needs a target plays on the enemy under the finger — or, when
        /// only one enemy is left alive, anywhere off the hand, since there is nothing to mis-aim at.
        /// A card that needs no target plays when it was dragged clear of the hand. Anything else
        /// glides back, which costs the player nothing.
        /// </summary>
        void OnCardDropped(CardView view, PointerEventData eventData)
        {
            _dragCard = null;
            if (_session == null || State.IsOver || !_cardViews.Contains(view))
            {
                Redraw();
                return;
            }

            bool clear = DroppedClearOfHand(eventData);
            var dropped = EnemyUnder(eventData);
            var target = dropped != null && dropped.Enemy.IsAlive ? dropped : null;

            if (view.Card.Data.Target == TargetMode.SingleEnemy)
            {
                if (target == null && clear)
                {
                    var only = OnlyLivingEnemy();
                    if (only != null) target = only;
                }
                if (target == null)
                {
                    Redraw();
                    return;
                }

                int index = _cardViews.IndexOf(view);
                _lastPlayTarget = (RectTransform)target.transform;
                Engine.TryPlayCard(view.Card, target.Enemy);
                Redraw();
                FocusHand(index);
                return;
            }

            if (!clear && target == null)
            {
                Redraw();
                return;
            }

            int slot = _cardViews.IndexOf(view);
            _lastPlayTarget = target != null ? (RectTransform)target.transform : null;
            Engine.TryPlayCard(view.Card, State.Player);
            Redraw();
            FocusHand(slot);
        }

        /// <summary>Whether the drop happened above the hand, which is what makes it a play and not a return.</summary>
        bool DroppedClearOfHand(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_handRow, eventData.position,
                                                                        eventData.pressEventCamera, out var local))
                return false;
            return local.y - _handRow.rect.center.y > 120f;
        }

        /// <summary>The enemy under the pointer, by what the event system hit — including a hit on a child of it.</summary>
        EnemyView EnemyUnder(PointerEventData eventData)
        {
            var hit = eventData.pointerCurrentRaycast.gameObject;
            var view = hit != null ? hit.GetComponentInParent<EnemyView>() : null;
            if (view != null) return view;

            // pointerCurrentRaycast is empty when the finger is over something that takes no raycasts,
            // such as the effects layer, so ask the event system directly.
            if (EventSystem.current == null) return null;
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            foreach (var result in results)
            {
                if (result.gameObject == null) continue;
                var found = result.gameObject.GetComponentInParent<EnemyView>();
                if (found != null) return found;
            }
            return null;
        }

        EnemyView OnlyLivingEnemy()
        {
            EnemyView only = null;
            foreach (var view in _enemyViews)
            {
                if (view == null || !view.Enemy.IsAlive) continue;
                if (only != null) return null;
                only = view;
            }
            return only;
        }

        void OnEnemyClicked(EnemyView view)
        {
            if (_session == null || State.IsOver || !view.Enemy.IsAlive) return;

            if (_selectedPotion >= 0)
            {
                PotionService.Use(_run, _selectedPotion, Engine, view.Enemy);
                Coach.Complete("potion");
                _selectedPotion = -1;
                Redraw();
                FocusHand(0);
                return;
            }
            if (_selectedCard == null) return;

            int index = _cardViews.IndexOf(_selectedCard);
            _lastPlayTarget = (RectTransform)view.transform;
            Engine.TryPlayCard(_selectedCard.Card, view.Enemy);
            _selectedCard = null;
            Redraw();
            FocusHand(index);
        }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        PointerEventData _debugDrag;
        CardView _debugDragView;

        /// <summary>
        /// Capture-harness only: picks up the first card in hand and holds it over the first living
        /// enemy, exactly as a finger would — the same handlers, the same pointer data. Returns a
        /// description, or null if there was nothing to drag. DebugReleaseDrag drops it.
        /// </summary>
        public string DebugDragCardToEnemy()
        {
            if (_session == null || State.IsOver || _cardViews.Count == 0) return null;
            var enemy = FirstLivingEnemy();
            if (enemy == null) return null;

            var view = _cardViews[0];
            var target = (RectTransform)enemy.transform;
            var world = target.TransformPoint(target.rect.center);

            _debugDrag = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, world),
                button = PointerEventData.InputButton.Left,
            };

            _debugDragView = view;
            view.OnBeginDrag(_debugDrag);
            view.OnDrag(_debugDrag);
            return $"{view.Card.Data.DisplayName} -> {enemy.name}";
        }

        /// <summary>Capture-harness only: lets go of the card held by DebugDragCardToEnemy.</summary>
        public string DebugReleaseDrag()
        {
            if (_debugDrag == null || _debugDragView == null) return null;
            int before = State.Hand.Count;
            _debugDragView.OnEndDrag(_debugDrag);
            _debugDrag = null;
            _debugDragView = null;
            return $"hand {before} -> {State.Hand.Count}";
        }

        /// <summary>Capture-harness only: a one-line description of the run, for save tests.</summary>
        public string DebugRunSummary() =>
            _run == null ? "none"
                         : $"seed={_run.Seed} fight={_run.FightNumber} deck={_run.Deck.Count} "
                           + $"hp={_run.Hp}/{_run.MaxHp} node={_run.Map?.Current?.Row},{_run.Map?.Current?.Column}"
                           + $" upgraded={CountUpgraded()}";

        int CountUpgraded()
        {
            int count = 0;
            foreach (var card in _run.Deck)
                if (card != null && card.IsUpgraded) count++;
            return count;
        }

        /// <summary>Capture-harness only: opens the rest site without walking the map to one.</summary>
        public void DebugOpenRest()
        {
            if (_run == null) return;
            _mapView.Hide();
            OpenRest();
        }

        /// <summary>Capture-harness only: every encounter id in the pools, in authored order.</summary>
        public List<string> DebugEncounterIds()
        {
            var ids = new List<string>();
            foreach (var encounter in _config.Encounters)
                if (encounter != null) ids.Add(encounter.Id);
            return ids;
        }

        /// <summary>Capture-harness only: starts a fight against one named encounter.</summary>
        public void DebugFightEncounter(string id)
        {
            if (_run == null) return;
            var encounter = _config.Encounters.Find(e => e != null && e.Id == id);
            if (encounter == null) return;
            _mapView.Hide();
            _restView?.Hide();
            StartFight(encounter);
        }

        /// <summary>
        /// Capture-harness only: passes every piece of content text through translation, so Loc.Missing lists what
        /// the capture did not happen to show — every card, relic, potion, enemy, move, event and keyword.
        /// </summary>
        public void DebugSweepTranslations()
        {
            foreach (var card in _config.AllCards)
            {
                if (card == null) continue;
                Loc.T(card.DisplayName);
                Keywords.Highlight(card.BuildDescription());
            }

            var relics = new List<Content.Relics.RelicData>(_config.RelicPoolFor(_config.AllUnlockIds()));
            relics.AddRange(_config.Relics);
            foreach (var relic in relics)
            {
                if (relic == null) continue;
                Loc.T(relic.DisplayName);
                Keywords.Highlight(relic.Description);
            }

            foreach (var potion in _config.PotionPool)
            {
                if (potion == null) continue;
                Loc.T(potion.DisplayName);
                Keywords.Highlight(potion.BuildDescription());
            }

            foreach (var encounter in _config.Encounters)
                if (encounter != null)
                    foreach (var enemy in encounter.Enemies)
                    {
                        if (enemy == null) continue;
                        Loc.T(enemy.DisplayName);
                        foreach (var move in enemy.Moves)
                            if (move != null) Loc.T(move.Label);
                    }

            foreach (var evt in EventService.All)
            {
                Loc.T(evt.Title);
                Keywords.Highlight(evt.Body);
                foreach (var choice in evt.Choices)
                {
                    Loc.T(choice.Label);
                    Keywords.Highlight(choice.Effect);
                }
            }

            foreach (var keyword in Keywords.All)
            {
                Loc.T(keyword.Word);
                Keywords.Highlight(keyword.Body);
            }

            foreach (var unlock in _config.Unlocks)
            {
                if (unlock == null) continue;
                Loc.T(unlock.DisplayName);
                Loc.T(unlock.Describe());
            }

            for (int act = 1; act <= _config.Acts; act++) Loc.T(_config.ActName(act).ToUpperInvariant());
            for (int level = 1; level <= DifficultyRules.Max; level++) Loc.T(DifficultyRules.Name(level));
            foreach (var rule in DifficultyRules.RulesAt(DifficultyRules.Max)) Loc.T(rule);
        }

        /// <summary>Capture-harness only: moves on to the next act's map, as if its boss had just been beaten.</summary>
        public void DebugStartNextAct()
        {
            if (_run == null || _run.Act >= _config.Acts) return;
            _run.FightNumber++;
            _run.BeginNextAct();
            ShowMap();
        }

        /// <summary>Capture-harness only: no Energy left and Heat past the threshold, so the end-turn and Heat tips come up.</summary>
        public void DebugRaiseLateTips()
        {
            if (_session == null) return;
            State.Energy = 0;
            State.Heat = State.OverheatThreshold + 2;
            Redraw();
        }

        /// <summary>Capture-harness only: shows the end-of-run screen for the current run without ending it.</summary>
        public void DebugShowEndOfRun(bool won) => ShowEndOfRun(won);

        /// <summary>Capture-harness only: opens a shop with 160 gold — enough for some shelves and not others.</summary>
        public void DebugOpenShop()
        {
            if (_run == null) return;
            _mapView.Hide();
            _run.Gold = 160;
            OpenShop();
        }

        /// <summary>Capture-harness only: fills the potion belt from the pool.</summary>
        public void DebugGivePotions()
        {
            if (_run == null || _config == null) return;
            foreach (var potion in _config.PotionPool)
                if (!PotionService.TryAdd(_run, potion)) break;
            Redraw();
        }

        /// <summary>Capture-harness only: opens a named event at the current position.</summary>
        public void DebugOpenEvent(string id)
        {
            if (_run == null) return;
            var evt = EventService.Find(id);
            if (evt == null) return;
            _mapView.Hide();
            _eventView.Show(evt, EventService.Context(_run, _config));
        }


        /// <summary>
        /// Capture-harness only: resolves the current fight as a win.
        ///
        /// The harness exists to take screenshots, not to play well. Teaching it to actually
        /// win would mean duplicating the simulator's policy inside the shipped assembly —
        /// two copies of the same judgement, guaranteed to drift. This is honest about being
        /// a tool, and it is compiled out of a release build.
        /// </summary>
        public void DebugWinFight()
        {
            if (_session == null) return;

            // Revive first when the harness already lost. Resuming a save at 19 HP and then
            // playing one bad turn kills the player before the win can be forced, and the
            // tool's job is to reach the reward screen, not to be fair about it.
            if (State.IsOver)
            {
                if (State.PlayerWon) return;
                State.Player.Hp = Mathf.Max(1, State.Player.Hp);
                State.IsOver = false;
                State.PlayerWon = false;
            }

            foreach (var enemy in new System.Collections.Generic.List<Enemy>(State.LivingEnemies()))
                Engine.LoseHp(enemy, enemy.Hp);

            Engine.EndPlayerTurn();   // runs the end-of-combat check
            Redraw();
        }
#endif

        void OnEndTurnClicked()
        {
            if (_session == null || State.IsOver) return;
            _selectedCard = null;
            _selectedPotion = -1;
            Coach.Complete("endturn");
            Engine.EndPlayerTurn();
            Redraw();
        }

        // ── Rendering ────────────────────────────────────────────────────────────────

        void Redraw()
        {
            if (_session == null) return;

            SyncHand();

            RefreshPotions();
            bool targeting = _selectedCard != null || _selectedPotion >= 0 || _dragCard != null;
            foreach (var enemyView in _enemyViews)
                enemyView.Refresh(targeting);

            var player = State.Player;
            UiFactory.SetBarFill(_playerHealthFill, player.MaxHp > 0 ? (float)player.Hp / player.MaxHp : 0f);
            _playerHealthLabel.text = $"{player.Hp} / {player.MaxHp}";
            _playerBlockBadge.gameObject.SetActive(player.Block > 0);
            _playerBlockLabel.text = player.Block.ToString();
            _playerStatuses.Set(player);

            _energyLabel.text = $"{State.Energy}/{State.EnergyPerTurn}";

            int threshold = State.OverheatThreshold;
            bool overheating = State.Heat > threshold;
            // The bar fills to the threshold, then the whole thing turns red — the player
            // needs to read "I am being damaged" instantly, not compute it from two numbers.
            UiFactory.SetBarFill(_heatFill, threshold > 0 ? Mathf.Clamp01((float)State.Heat / threshold) : 0f);
            _heatFill.color = overheating ? Palette.Overheat : Palette.Energy;
            _heatTrack.color = overheating ? Palette.OverheatTrack : Palette.HeatTrack;
            _heatLabel.text = overheating
                ? $"{State.Heat} / {threshold}   OVERHEAT  −{State.Heat - threshold} HP"
                : $"{State.Heat} / {threshold}";
            _turnLabel.text = $"Turn {State.TurnNumber}";
            _drawLabel.text = $"Draw {State.DrawPile.Count}";
            _discardLabel.text = $"Discard {State.DiscardPile.Count}" +
                                 (State.ExhaustPile.Count > 0 ? $"    Exhaust {State.ExhaustPile.Count}" : "");
            _endTurnButton.interactable = !State.IsOver;

            bool anyPlayable = false;
            foreach (var cardView in _cardViews)
            {
                var card = cardView.Card;
                int cost = Engine.GetCardCost(card);
                bool playable = !State.IsOver && cost <= State.Energy;
                anyPlayable |= playable;
                cardView.Refresh(playable, cardView == _selectedCard, cost);
            }

            RaiseCombatTips(anyPlayable);
        }

        /// <summary>
        /// Rebuilds the hand only when its contents actually changed. Recreating every card
        /// object each redraw would throw away the selection and make any future animation
        /// impossible — the view has to keep identity across frames.
        /// </summary>
        void SyncHand()
        {
            // Views are matched to cards by identity. A card that stays in hand keeps its view and
            // glides to its new slot; a new card flies in from the draw pile; a card that left
            // flies to wherever it went. Rebuilding the hand from scratch — as this did before
            // there was motion — would make every card re-deal itself on every play.
            var next = new List<CardView>(State.Hand.Count);
            int dealt = 0;
            foreach (var card in State.Hand)
            {
                var view = _cardViews.Find(v => v != null && v.Card == card);
                if (view == null)
                {
                    view = CardView.Create(_handRow, card);
                    view.Clicked += OnCardClicked;
                    view.Draggable = true;
                    view.DragStarted += OnCardDragStarted;
                    view.Dropped += OnCardDropped;
                    NavHint.On(view).Priority = 10;   // a fight opens with focus in the hand
                    // Face down on the pile, then dealt in turn. The stagger matches the draw sound's,
                    // so each card lands on its own click.
                    view.DealIn(DrawPilePoint, 0.3f, -32f, 0.05f + dealt++ * 0.07f);
                }
                next.Add(view);
            }

            int discarded = 0;
            foreach (var view in _cardViews)
            {
                if (view == null || next.Contains(view)) continue;
                bool played = view.Card == _lastPlayedCard;
                if (played)
                {
                    // Thrown at whatever it was aimed at, so an attack visibly travels to the enemy it
                    // hits. A card with no target flies to the middle of the board instead.
                    var target = _lastPlayTarget != null && _lastPlayTarget.gameObject.activeInHierarchy
                        ? Motion.PointIn(_handRow, _lastPlayTarget)
                        : PlayedPoint;
                    view.FlyAway(target, 1.15f, 0.3f);
                }
                else
                {
                    // Swept to the discard pile in order, so a five-card discard reads as a sweep.
                    view.FlyAway(DiscardPilePoint, 0.3f, 0.35f, delay: discarded++ * 0.05f);
                }
            }

            _cardViews.Clear();
            _cardViews.AddRange(next);
            if (_selectedCard != null && !_cardViews.Contains(_selectedCard)) _selectedCard = null;

            LayoutHand();
        }

        void LayoutHand()
        {
            int count = _cardViews.Count;
            if (count == 0) return;

            // Cards overlap once the hand grows, and fan slightly. The fan is not decoration:
            // it keeps every card's top edge visible, so a ten-card hand stays readable.
            float step = Mathf.Min(CardView.Width + 14f, 1180f / Mathf.Max(1, count));
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
                _cardViews[i].SetRest(new Vector2(startX + i * step, lift), -centred * 3f);
            }

            // A card held in a finger stays above the rest of the hand.
            if (_dragCard != null) _dragCard.transform.SetAsLastSibling();
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
