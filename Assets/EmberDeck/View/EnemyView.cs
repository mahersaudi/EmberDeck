using System;
using System.Text;
using EmberDeck.Combat;
using EmberDeck.Content;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    public sealed class EnemyView : MonoBehaviour
    {
        public Enemy Enemy { get; private set; }

        Image _body;
        Image _healthFill;
        Text _nameLabel;
        Text _healthLabel;
        Text _intentLabel;
        Text _statusLabel;
        Text _blockLabel;
        Image _blockBadge;
        Button _button;

        public event Action<EnemyView> Clicked;

        public static EnemyView Create(Transform parent, Enemy enemy)
        {
            var root = UiFactory.Panel(parent, $"Enemy_{enemy.Data.Id}", Palette.PanelDark);
            root.sizeDelta = new Vector2(260f, 300f);

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
            _intentLabel = UiFactory.Label(rect, "Intent", "", 30, Palette.IntentAttack);
            UiFactory.Place(_intentLabel.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 8f), new Vector2(260f, 44f));

            var body = UiFactory.Panel(rect, "Body", enemy.Data.TintColor);
            UiFactory.Place(body, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -12f), new Vector2(150f, 170f));
            _body = body.GetComponent<Image>();

            _nameLabel = UiFactory.Label(rect, "Name", enemy.Name, 22, Palette.Ink);
            UiFactory.Place(_nameLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 72f), new Vector2(250f, 28f));

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

            _statusLabel = UiFactory.Label(rect, "Statuses", "", 17, Palette.InkMuted);
            UiFactory.Place(_statusLabel.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(0f, 8f), new Vector2(250f, 24f));

            _button = gameObject.AddComponent<Button>();
            _button.targetGraphic = GetComponent<Image>();
            _button.onClick.AddListener(() => Clicked?.Invoke(this));
        }

        public void Refresh(bool targetable)
        {
            bool alive = Enemy.IsAlive;
            gameObject.SetActive(true);

            _body.color = alive ? Enemy.Data.TintColor : Enemy.Data.TintColor * 0.3f;
            GetComponent<Image>().color = targetable && alive ? Palette.PanelRaised : Palette.PanelDark;

            UiFactory.SetBarFill(_healthFill, Enemy.MaxHp > 0 ? (float)Enemy.Hp / Enemy.MaxHp : 0f);
            _healthLabel.text = $"{Enemy.Hp} / {Enemy.MaxHp}";

            _blockBadge.gameObject.SetActive(Enemy.Block > 0);
            _blockLabel.text = Enemy.Block.ToString();

            _statusLabel.text = DescribeStatuses(Enemy);

            if (!alive)
            {
                _intentLabel.text = "";
                _nameLabel.color = Palette.InkMuted;
                return;
            }

            var intent = Enemy.CurrentIntent;
            _intentLabel.color = intent.Kind switch
            {
                IntentKind.Attack => Palette.IntentAttack,
                IntentKind.Block  => Palette.IntentBlock,
                _                 => Palette.IntentBuff
            };
            _intentLabel.text = intent.Kind switch
            {
                IntentKind.Attack when intent.Hits > 1 => $"{intent.Value} x{intent.Hits}",
                IntentKind.Attack                      => intent.Value.ToString(),
                IntentKind.Block                       => $"[ {intent.Value} ]",
                _                                      => intent.Label
            };
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
