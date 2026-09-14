using System;
using System.Collections.Generic;
using System.Text;
using EmberDeck.Combat;
using EmberDeck.Content;
using EmberDeck.Content.Effects;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    public sealed class EnemyView : MonoBehaviour
    {
        public Enemy Enemy { get; private set; }

        Image _body;
        bool _hasArt;
        Image _flash;
        Vector2 _bodyRest;
        float _breathPhase;

        /// <summary>The overlay a hit flashes on.</summary>
        public Graphic FlashGraphic => _flash;
        Image _healthFill;
        Text _nameLabel;
        Text _healthLabel;
        Text _intentLabel;
        RectTransform _intentRow;
        Image _intentIcon;
        readonly List<Image> _intentExtras = new();
        string _intentSignature;
        StatusStrip _statuses;
        Text _blockLabel;
        Image _blockBadge;
        Button _button;

        public event Action<EnemyView> Clicked;

        public static EnemyView Create(Transform parent, Enemy enemy)
        {
            var root = UiFactory.Panel(parent, $"Enemy_{enemy.Data.Id}", Palette.PanelDark);
            root.sizeDelta = new Vector2(260f, 300f);
            UiFactory.Frame(root.GetComponent<Image>(), "frame_panel", 4f);

            var view = root.gameObject.AddComponent<EnemyView>();
            view.Build(enemy);
            return view;
        }

        void Build(Enemy enemy)
        {
            Enemy = enemy;
            var rect = (RectTransform)transform;

            // Intent sits ABOVE the enemy, where the player looks first. Announcing the next
            // attack is what makes each turn a solvable problem rather than a coin flip, so
            // it gets the most legible position on the board.
            // An icon for the kind of move, the number, then a small icon for anything else the
            // move does — so an attack that also applies Burn says so before it lands.
            var intentRow = new GameObject("Intent", typeof(RectTransform));
            intentRow.transform.SetParent(rect, false);
            _intentRow = (RectTransform)intentRow.transform;
            UiFactory.Place(_intentRow, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 8f), new Vector2(260f, 44f));
            var intentLayout = intentRow.AddComponent<HorizontalLayoutGroup>();
            intentLayout.childAlignment = TextAnchor.MiddleCenter;
            intentLayout.spacing = 5f;
            intentLayout.childControlWidth = true;
            intentLayout.childControlHeight = true;
            intentLayout.childForceExpandWidth = false;
            intentLayout.childForceExpandHeight = false;

            _intentIcon = Icons.Create(_intentRow, "Kind", null, 40f);
            _intentLabel = UiFactory.Label(_intentRow, "Value", "", 30, Palette.IntentAttack);
            _intentLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            _intentLabel.gameObject.AddComponent<LayoutElement>().preferredHeight = 44f;

            var body = UiFactory.Panel(rect, "Body", enemy.Data.TintColor);
            UiFactory.Place(body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -8f), new Vector2(240f, 184f));
            _body = body.GetComponent<Image>();

            if (enemy.Data.Art != null)
            {
                _body.sprite = enemy.Data.Art;
                _body.preserveAspect = true;
                _hasArt = true;
            }

            // The flash sits over the portrait, so a hit reads on the thing that was hit rather
            // than on the panel around it.
            var flash = UiFactory.Panel(body, "HitFlash", new Color(1f, 1f, 1f, 0f));
            UiFactory.Stretch(flash);
            _flash = flash.GetComponent<Image>();
            _flash.raycastTarget = false;
            _bodyRest = body.anchoredPosition;
            _breathPhase = UnityEngine.Random.value * Mathf.PI * 2f;

            _nameLabel = UiFactory.Label(rect, "Name", enemy.Name, 22, Palette.Ink);
            UiFactory.Place(_nameLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 76f), new Vector2(250f, 28f));

            _healthFill = UiFactory.Bar(rect, "Health", Palette.HealthTrack, Palette.Health,
                                        new Vector2(210f, 22f), new Vector2(0f, -108f));

            _healthLabel = UiFactory.Label(rect, "HealthText", "", 18, Palette.Ink);
            UiFactory.Place(_healthLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 36f), new Vector2(250f, 24f));

            var blockBadge = UiFactory.Panel(rect, "BlockBadge", Palette.Block);
            UiFactory.Place(blockBadge, new Vector2(0f, 0f), new Vector2(0f, 0f),
                            new Vector2(16f, 30f), new Vector2(46f, 34f));
            _blockBadge = blockBadge.GetComponent<Image>();
            _blockLabel = UiFactory.Label(blockBadge, "BlockText", "", 20, Palette.Background);
            UiFactory.Stretch(_blockLabel.rectTransform);

            _statuses = StatusStrip.Create(rect, "Statuses", TextAnchor.MiddleCenter);
            UiFactory.Place((RectTransform)_statuses.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 4f), new Vector2(250f, 30f));

            _button = gameObject.AddComponent<Button>();
            _button.targetGraphic = GetComponent<Image>();
            _button.onClick.AddListener(() => Clicked?.Invoke(this));

            TooltipTrigger.Attach(gameObject, DescribeForTooltip);
        }

        /// <summary>
        /// A slow idle breath, offset per enemy so a group never moves in lockstep. A portrait that
        /// never moves reads as a picture of a monster; three pixels of motion make it the monster.
        /// </summary>
        void Update()
        {
            if (Enemy == null || !Enemy.IsAlive || _body == null) return;
            float t = Time.time * 1.7f + _breathPhase;
            var rect = _body.rectTransform;
            rect.anchoredPosition = _bodyRest + new Vector2(0f, Mathf.Sin(t) * 3f);
            float s = 1f + Mathf.Sin(t + 0.8f) * 0.012f;
            rect.localScale = new Vector3(s, s, 1f);
        }

        public void Refresh(bool targetable)
        {
            bool alive = Enemy.IsAlive;
            gameObject.SetActive(true);

            // A sprite is tinted white so its own colours show; only the placeholder
            // rectangle takes the enemy's tint.
            _body.color = _hasArt
                ? (alive ? Color.white : new Color(0.45f, 0.45f, 0.5f, 1f))
                : (alive ? Enemy.Data.TintColor : Enemy.Data.TintColor * 0.3f);
            GetComponent<Image>().color = targetable && alive ? Palette.PanelRaised : Palette.PanelDark;

            UiFactory.SetBarFill(_healthFill, Enemy.MaxHp > 0 ? (float)Enemy.Hp / Enemy.MaxHp : 0f);
            _healthLabel.text = $"{Enemy.Hp} / {Enemy.MaxHp}";

            _blockBadge.gameObject.SetActive(Enemy.Block > 0);
            _blockLabel.text = Enemy.Block.ToString();

            _statuses.Set(Enemy);

            if (!alive)
            {
                _intentRow.gameObject.SetActive(false);
                _nameLabel.color = Palette.InkMuted;
                return;
            }

            var intent = Enemy.CurrentIntent;
            _intentRow.gameObject.SetActive(true);
            Icons.SetSprite(_intentIcon, Icons.For(intent.Kind));
            _intentLabel.color = intent.Kind switch
            {
                IntentKind.Attack => Palette.IntentAttack,
                IntentKind.Block  => Palette.IntentBlock,
                IntentKind.Debuff => Keywords.VulnerableColor,
                _                 => Palette.IntentBuff
            };
            // A move with no number shows its name, so "Chant" still says something before the
            // tooltip is opened.
            string intentText = intent.Kind switch
            {
                IntentKind.Attack when intent.Hits > 1 => $"{intent.Value} x{intent.Hits}",
                IntentKind.Attack                      => intent.Value.ToString(),
                IntentKind.Block                       => intent.Value.ToString(),
                _                                      => intent.Label
            };
            _intentLabel.text = intentText;

            var extras = IntentExtras(intent);
            SetExtras(extras);

            // A changed intent pops, so a new threat is noticed rather than found.
            string signature = $"{intent.Kind}|{intentText}|{extras.Count}";
            if (signature != _intentSignature)
            {
                bool first = _intentSignature == null;
                _intentSignature = signature;
                if (!first) Motion.Punch(_intentRow, 0.28f, 0.32f);
            }
        }

        /// <summary>
        /// Everything a move does besides its headline, as small icons after the number. Scald used
        /// to read "4": the 3 Burn it also applies stayed invisible until it had already landed.
        /// </summary>
        static List<Sprite> IntentExtras(Intent intent)
        {
            var extras = new List<Sprite>();
            if (intent.Move == null) return extras;
            foreach (var effect in intent.Move.Effects)
            {
                Sprite icon = effect switch
                {
                    ApplyStatusEffect status                             => Icons.For(status.Status),
                    DealDamageEffect hit when hit.PerHitStatusAmount > 0 => Icons.For(hit.PerHitStatus),
                    GainBlockEffect when intent.Kind != IntentKind.Block => Icons.Get("res_block"),
                    _                                                    => null
                };
                if (icon != null) extras.Add(icon);
            }
            return extras;
        }

        void SetExtras(List<Sprite> extras)
        {
            while (_intentExtras.Count < extras.Count)
                _intentExtras.Add(Icons.Create(_intentRow, "Extra", null, 30f));
            for (int i = 0; i < _intentExtras.Count; i++)
            {
                bool used = i < extras.Count;
                _intentExtras[i].gameObject.SetActive(used);
                if (used) Icons.SetSprite(_intentExtras[i], extras[i]);
            }
        }

        static string IntentIconId(IntentKind kind) => kind switch
        {
            IntentKind.Attack => "intent_attack",
            IntentKind.Block  => "intent_block",
            IntentKind.Buff   => "intent_buff",
            IntentKind.Debuff => "intent_debuff",
            _                 => "intent_unknown",
        };

        /// <summary>The intent spelled out, then every status on this enemy and every status it is about to apply.</summary>
        IReadOnlyList<Tooltip.Entry> DescribeForTooltip()
        {
            var entries = new List<Tooltip.Entry>();
            if (Enemy == null) return entries;

            bool acting = Enemy.IsAlive && Enemy.CurrentIntent.Move != null;
            if (acting)
            {
                var intent = Enemy.CurrentIntent;
                entries.Add(new Tooltip.Entry($"Intent: {intent.Label}", DescribeMove(intent),
                                              IntentIconId(intent.Kind), _intentLabel.color));
            }

            if (Enemy.Block > 0)
                entries.Add(Tooltip.Entry.From(Keywords.Find("Block"), $"Block {Enemy.Block}"));

            var explained = new List<StatusType>();
            foreach (StatusType status in Enum.GetValues(typeof(StatusType)))
            {
                int value = Enemy.GetStatus(status);
                var keyword = Keywords.For(status);
                if (value == 0 || keyword == null) continue;
                entries.Add(Tooltip.Entry.From(keyword, $"{keyword.Word} {value}"));
                explained.Add(status);
            }

            // Statuses the move is about to apply, defined before they land.
            if (acting)
            {
                foreach (var effect in Enemy.CurrentIntent.Move.Effects)
                {
                    StatusType? status = effect switch
                    {
                        ApplyStatusEffect apply                              => apply.Status,
                        DealDamageEffect hit when hit.PerHitStatusAmount > 0 => hit.PerHitStatus,
                        _                                                    => null
                    };
                    if (status == null || explained.Contains(status.Value)) continue;
                    explained.Add(status.Value);
                    var keyword = Keywords.For(status.Value);
                    if (keyword != null) entries.Add(Tooltip.Entry.From(keyword));
                }
            }

            return entries;
        }

        /// <summary>
        /// A move in the player's words: "Deals 4 damage. Applies 3 Burn to you." The damage is the
        /// engine's resolved number — after Strength, Weak and Vulnerable — the same one on the badge.
        /// </summary>
        string DescribeMove(Intent intent)
        {
            var parts = new List<string>();
            foreach (var effect in intent.Move.Effects)
            {
                switch (effect)
                {
                    case DealDamageEffect hit:
                        parts.Add(intent.Hits > 1 ? $"Deals {intent.Value} damage {intent.Hits} times." : $"Deals {intent.Value} damage.");
                        if (hit.PerHitStatusAmount > 0)
                            parts.Add($"Each hit applies {hit.PerHitStatusAmount} {hit.PerHitStatus.DisplayName()}.");
                        break;
                    case GainBlockEffect block:
                        parts.Add($"Gains {block.Amount + Enemy.GetStatus(StatusType.Dexterity)} Block.");
                        break;
                    case ApplyStatusEffect status:
                        parts.Add(status.ApplyToSelf
                            ? $"Gains {status.Amount} {status.Status.DisplayName()}."
                            : $"Applies {status.Amount} {status.Status.DisplayName()} to you.");
                        break;
                    default:
                        var text = effect != null ? effect.Describe() : null;
                        if (!string.IsNullOrEmpty(text)) parts.Add(text);
                        break;
                }
            }
            return string.Join(" ", parts);
        }

        public static string DescribeStatuses(Actor actor)
        {
            if (actor.Statuses.Count == 0) return "";

            var builder = new StringBuilder();
            foreach (var pair in actor.Statuses)
            {
                if (pair.Value == 0) continue;
                if (builder.Length > 0) builder.Append("   ");
                builder.Append(pair.Key.DisplayName()).Append(' ').Append(pair.Value);
            }
            return builder.ToString();
        }
    }
}
