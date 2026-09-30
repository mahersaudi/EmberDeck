using System;
using System.Collections.Generic;
using EmberDeck.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// Turns combat events into motion and sound, on a timeline of its own.
    ///
    /// The engine resolves a card, or a whole enemy turn, inside one call: every hit of it arrives in the
    /// same frame, already decided. Shown as it arrives, a fight is a blur — a card's damage appears the
    /// instant it leaves the hand, three enemies' attacks are one thud, and the next hand is dealt on top
    /// of all of it. The first playtest said exactly that: the fight felt weak.
    ///
    /// So the view keeps a timeline and replays the fight on it, in beats:
    ///
    ///   a card        lifts, is thrown, and its effects land when it arrives (Pace.CardImpact)
    ///   an enemy      draws back and lunges, and its blow lands at the bottom of the lunge;
    ///                 the next enemy waits its turn (Pace.EnemyActionGap)
    ///   a new hand    is dealt only after the last enemy has finished
    ///
    /// The rules have already finished; nothing here changes them, and the simulator runs the same game
    /// without any of it. What the view has to be careful about is showing numbers the replay has not
    /// reached yet, so health waits too: ShownHp is the model's health plus every blow still in the air,
    /// and each blow releases its share when it lands.
    ///
    /// It subscribes to the bus the way a relic does, and only ever reads. The session clears the bus
    /// when the fight ends, which is what detaches it.
    /// </summary>
    public sealed class CombatFeedback
    {
        const int HeavyHit = 12;

        static readonly Color BurnColor = new(1f, 0.56f, 0.2f);
        static readonly Color ImpactOnEnemy = new(1f, 0.86f, 0.55f);
        static readonly Color ImpactOnPlayer = new(1f, 0.4f, 0.32f);
        static readonly Color DebuffColor = new(0.72f, 0.55f, 1f);
        static readonly Color HitFlash = new(1f, 0.22f, 0.18f, 0.6f);

        readonly CombatState _state;
        readonly RectTransform _layer;
        readonly RectTransform _board;
        readonly Func<Actor, RectTransform> _anchorFor;
        readonly Func<Actor, Graphic> _flashFor;
        readonly Func<Actor, EnemyView> _viewFor;

        // ── The timeline ──
        float _cursor;           // the next free moment for an enemy action
        int _beatFrame = -1;     // the frame the open beat belongs to; a beat never outlives its frame
        float _beatImpact;       // when the open beat's first blow lands
        int _beatHits;           // blows already placed in the open beat
        readonly Dictionary<Actor, int> _pendingHp = new();
        readonly Dictionary<Actor, int> _pendingBlockGain = new();
        readonly Dictionary<Actor, float> _lastImpact = new();

        /// <summary>When everything queued so far has finished playing, in unscaled time.</summary>
        public float BusyUntil { get; private set; }

        /// <summary>Seconds until the replay has caught up with the model.</summary>
        public float Remaining => Mathf.Max(0f, BusyUntil - Now);

        /// <summary>The moment a hand drawn now may begin to be dealt: after the enemy turn it follows.</summary>
        public float DealAt { get; private set; }

        /// <summary>A blow landed (or a shield went up): health and Block on screen have moved.</summary>
        public event Action Landed;

        /// <summary>Block that absorbed a blow, at the moment the blow lands.</summary>
        public event Action<Actor, int> BlockSpent;

        static float Now => Time.unscaledTime;

        public CombatFeedback(CombatState state, RectTransform layer, RectTransform board,
                              Func<Actor, RectTransform> anchorFor, Func<Actor, Graphic> flashFor,
                              Func<Actor, EnemyView> viewFor)
        {
            _state = state;
            _layer = layer;
            _board = board;
            _anchorFor = anchorFor;
            _flashFor = flashFor;
            _viewFor = viewFor;

            var bus = state.Bus;
            bus.Subscribe<DamageAppliedEvent>(OnDamage);
            bus.Subscribe<BlockGainedEvent>(OnBlock);
            bus.Subscribe<ActorDiedEvent>(OnDied);
            bus.Subscribe<StatusAppliedEvent>(OnStatus);
            bus.Subscribe<HeatGainedEvent>(OnHeat);
            bus.Subscribe<TurnEndedEvent>(OnTurnEnded);
            bus.Subscribe<EnemyActionEvent>(OnEnemyAction);
            bus.Subscribe<TurnStartedEvent>(OnTurnStarted);
            bus.Subscribe<CombatEndedEvent>(OnCombatEnded);
            bus.Subscribe<PotionUsedEvent>(OnPotion);
            bus.Subscribe<HeartFireEvent>(OnHeartFire);
        }

        // ── What the view shows ──────────────────────────────────────────────────────

        /// <summary>Health as far as the replay has got: the model's, plus every blow still in the air.</summary>
        public int ShownHp(Actor actor) =>
            actor == null ? 0 : actor.Hp + (_pendingHp.TryGetValue(actor, out int hp) ? hp : 0);

        /// <summary>Block as far as the replay has got: shields not yet raised are not shown.</summary>
        public int ShownBlock(Actor actor) =>
            actor == null ? 0 : Mathf.Max(0, actor.Block - (_pendingBlockGain.TryGetValue(actor, out int gain) ? gain : 0));

        // ── Opening beats ────────────────────────────────────────────────────────────

        /// <summary>
        /// A card is about to be played. Its effects will arrive in this frame, and they land when the card
        /// does, so everything they cause is placed at Pace.CardImpact from now.
        /// </summary>
        public void BeginCard()
        {
            OpenBeat(Now + Pace.CardImpact);
            AudioDirector.Play(Sfx.CardPlay, 0.8f);                           // lifted out of the hand
            Motion.After(Pace.CardWindup, () => AudioDirector.Play(Sfx.Throw, 0.7f));  // and thrown
        }

        void OpenBeat(float impact)
        {
            _beatFrame = Time.frameCount;
            _beatImpact = impact;
            _beatHits = 0;
        }

        /// <summary>An event that arrived with no beat announced — a relic, a potion: its own short beat.</summary>
        void EnsureBeat()
        {
            if (_beatFrame == Time.frameCount) return;
            OpenBeat(Mathf.Max(Now, _cursor) + Pace.S(0.08f));
        }

        /// <summary>When the next blow of the open beat lands. Hits of one beat follow each other closely.</summary>
        float NextBlow()
        {
            EnsureBeat();
            float at = _beatImpact + _beatHits * Pace.HitGap;
            _beatHits++;
            Extend(at);
            return at;
        }

        /// <summary>When something that comes with the beat — a shield, a status — appears: after its blows.</summary>
        float WithBeat()
        {
            EnsureBeat();
            float at = _beatImpact + Mathf.Max(0, _beatHits - 1) * Pace.HitGap + (_beatHits > 0 ? Pace.S(0.12f) : 0f);
            Extend(at);
            return at;
        }

        void Extend(float at) => BusyUntil = Mathf.Max(BusyUntil, at + Pace.S(0.45f));

        static float DelayTo(float at) => Mathf.Max(0f, at - Now);

        Vector2 PointAbove(RectTransform anchor, float height) => Motion.PointIn(_layer, anchor, new Vector2(0f, height));

        /// <summary>Runs <paramref name="action"/> at an absolute time on the timeline.</summary>
        static void At(float time, Action action) => Motion.After(DelayTo(time), action);

        // ── Turn structure ───────────────────────────────────────────────────────────

        /// <summary>The player ended their turn: the enemy turn starts after anything still in the air.</summary>
        void OnTurnEnded(TurnEndedEvent e)
        {
            if (!e.IsPlayerTurn) return;
            float start = Mathf.Max(Now, _cursor, BusyUntil) + Pace.S(0.15f);
            OpenBeat(start);
            _cursor = start + Pace.S(0.3f);
        }

        /// <summary>One enemy acts: it lunges if it attacks, rears up if it does anything else.</summary>
        void OnEnemyAction(EnemyActionEvent e)
        {
            float start = Mathf.Max(Now, _cursor);
            bool attacks = e.Enemy.CurrentIntent.Kind == Content.IntentKind.Attack;
            float impact = attacks ? Pace.EnemyLunge : Pace.S(0.18f);
            OpenBeat(start + impact);
            _cursor = start + Pace.EnemyActionGap;
            Extend(start + impact);

            var view = _viewFor?.Invoke(e.Enemy);
            if (view == null) return;
            if (attacks)
            {
                // Toward the player, in the space the enemy cards are laid out in: the effects layer and the
                // enemy row are both unscaled children of the stage, so a direction in one is one in the other.
                var self = _anchorFor(e.Enemy);
                var player = _anchorFor(_state.Player);
                Vector2 toward = self != null && player != null
                    ? Motion.PointIn(_layer, player) - Motion.PointIn(_layer, self)
                    : Vector2.down;
                view.Lunge(DelayTo(start), impact, toward);
            }
            else Motion.Punch(view.transform, 0.08f, Pace.S(0.35f), DelayTo(start));
        }

        /// <summary>A new player turn: its hand is dealt after the enemy turn's last blow, never on top of it.</summary>
        void OnTurnStarted(TurnStartedEvent e)
        {
            if (!e.IsPlayerTurn) return;
            DealAt = Mathf.Max(Now, _cursor, BusyUntil) + Pace.S(0.1f);
            OpenBeat(DealAt);
            At(DealAt, () => AudioDirector.Play(Sfx.TurnStart, 0.7f));
            _cursor = DealAt;
        }

        // ── Blows ────────────────────────────────────────────────────────────────────

        void OnDamage(DamageAppliedEvent e)
        {
            var anchor = _anchorFor(e.Target);
            if (anchor == null || (e.HpLost <= 0 && e.BlockAbsorbed <= 0)) return;

            float at = NextBlow();
            float delay = DelayTo(at);
            var target = e.Target;
            _lastImpact[target] = at;

            // Health waits for the blow. It is released when the blow lands, and the view redraws then.
            if (e.HpLost > 0) _pendingHp[target] = (_pendingHp.TryGetValue(target, out int p) ? p : 0) + e.HpLost;
            int lost = e.HpLost, absorbed = e.BlockAbsorbed;
            At(at, () =>
            {
                if (lost > 0) _pendingHp[target] = Mathf.Max(0, _pendingHp[target] - lost);
                if (absorbed > 0) BlockSpent?.Invoke(target, absorbed);
                Landed?.Invoke();
            });

            var centre = Motion.PointIn(_layer, anchor);

            // Some of it hit a shield: the shield takes the blow first, visibly.
            if (e.BlockAbsorbed > 0)
            {
                Fx.Shield(_layer, centre, target.IsPlayer ? 170f : 140f, delay, shatter: true);
                Motion.FloatText(_layer, PointAbove(anchor, 80f), $"Blocked {e.BlockAbsorbed}", Palette.Block, 30, delay, rise: 40f);
                At(at, () => AudioDirector.Play(Sfx.BlockedHit, e.HpLost > 0 ? 0.6f : 0.9f));
            }

            if (e.HpLost <= 0)
            {
                Motion.Shake(anchor, 5f, Pace.S(0.2f), delay);
                return;
            }

            bool heavy = e.HpLost >= HeavyHit;
            bool fromAttack = e.Source != null;

            Motion.FloatText(_layer, PointAbove(anchor, 30f), $"-{e.HpLost}",
                             fromAttack ? (target.IsPlayer ? Palette.IntentAttack : Palette.Ink) : BurnColor,
                             heavy ? 64 : 50, delay);
            Motion.Shake(anchor, Mathf.Clamp(e.HpLost * 1.2f, 7f, 32f), Pace.S(heavy ? 0.5f : 0.36f), delay);
            Motion.Flash(_flashFor(target), fromAttack ? HitFlash : new Color(BurnColor.r, BurnColor.g, BurnColor.b, 0.45f),
                         Pace.S(0.4f), delay);

            // Where it lands: a flare inside an expanding ring, sized by the damage.
            var impact = fromAttack ? (target.IsPlayer ? ImpactOnPlayer : ImpactOnEnemy) : BurnColor;
            float size = Mathf.Lerp(160f, 340f, Mathf.Clamp01(e.HpLost / 24f));
            Fx.Flare(_layer, centre, new Color(impact.r, impact.g, impact.b, 0.8f), size * 0.8f, delay);
            Fx.Burst(_layer, centre, new Color(impact.r, impact.g, impact.b, 0.9f), size, delay);

            // The board itself moves for a blow that matters.
            if (heavy || (target.IsPlayer && fromAttack))
                Motion.Shake(_board, heavy ? 10f : 6f, Pace.S(0.4f), delay);

            var sound = !fromAttack ? Sfx.Burn : heavy ? Sfx.HeavyHit : Sfx.Hit;
            bool hurtPlayer = target.IsPlayer && fromAttack;
            float recoil = Mathf.Clamp(e.HpLost * 1.4f, 6f, 28f);
            var hitView = _viewFor?.Invoke(target);
            At(at, () =>
            {
                AudioDirector.Play(sound, fromAttack ? 0.9f : 0.5f);
                if (hurtPlayer) AudioDirector.Play(Sfx.PlayerHurt, 0.5f);
                if (hitView != null) hitView.Recoil(recoil);
            });
        }

        /// <summary>A shield goes up over whoever gained Block, and the badge waits for it.</summary>
        void OnBlock(BlockGainedEvent e)
        {
            var anchor = _anchorFor(e.Target);
            if (anchor == null || e.Amount <= 0) return;

            float at = WithBeat();
            float delay = DelayTo(at);
            var target = e.Target;
            int amount = e.Amount;

            _pendingBlockGain[target] = (_pendingBlockGain.TryGetValue(target, out int p) ? p : 0) + amount;
            At(at + Pace.S(0.2f), () =>
            {
                _pendingBlockGain[target] = Mathf.Max(0, _pendingBlockGain[target] - amount);
                Landed?.Invoke();
            });

            Fx.Shield(_layer, Motion.PointIn(_layer, anchor), target.IsPlayer ? 200f : 150f, delay);
            Motion.FloatText(_layer, PointAbove(anchor, 90f), $"+{e.Amount} Block", Palette.Block, 34, delay + Pace.S(0.15f), rise: 50f);
            At(at, () => AudioDirector.Play(Sfx.BlockGain, 0.8f));
        }

        void OnDied(ActorDiedEvent e)
        {
            if (e.Actor.IsPlayer) return;   // the defeat sting covers it
            var anchor = _anchorFor(e.Actor);
            if (anchor == null) return;

            float at = (_lastImpact.TryGetValue(e.Actor, out float last) ? last : WithBeat()) + Pace.S(0.2f);
            Extend(at + Pace.S(0.6f));
            float delay = DelayTo(at);

            Motion.Run(anchor, "death", Pace.S(0.7f), t => anchor.localScale = Vector3.one * (1f - 0.14f * t),
                       Motion.OutCubic, delay);
            _viewFor?.Invoke(e.Actor)?.Die(delay);
            Fx.Burst(_layer, Motion.PointIn(_layer, anchor), new Color(1f, 0.72f, 0.4f, 0.75f), 320f, delay, Pace.S(0.7f));
            At(at, () => AudioDirector.Play(Sfx.EnemyDeath, 0.8f));
        }

        void OnStatus(StatusAppliedEvent e)
        {
            if (e.Amount <= 0) return;
            var anchor = _anchorFor(e.Target);
            if (anchor == null) return;

            bool harmful = e.Status is StatusType.Burn or StatusType.Vulnerable or StatusType.Weak;
            var color = e.Status == StatusType.Burn ? BurnColor : harmful ? DebuffColor : Palette.IntentBuff;
            var sound = e.Status == StatusType.Burn ? Sfx.Burn : harmful ? Sfx.Debuff : Sfx.Buff;

            float at = WithBeat();
            Motion.FloatText(_layer, PointAbove(anchor, 110f), $"+{e.Amount} {e.Status.DisplayName()}", color, 30,
                             DelayTo(at), rise: 44f);
            At(at, () => AudioDirector.Play(sound, 0.45f));
        }

        /// <summary>
        /// Heat is heard only when it crosses the line. It used to sound on every gain, and a Heat deck gains
        /// it three times a turn — the sound the first playtest singled out as the one that grated.
        /// </summary>
        void OnHeat(HeatGainedEvent e)
        {
            if (e.Amount <= 0) return;
            int threshold = _state.OverheatThreshold;
            bool crossed = e.Total > threshold && e.Total - e.Amount <= threshold;
            if (!crossed) return;
            At(WithBeat(), () => AudioDirector.Play(Sfx.Overheat, 0.6f));
        }

        /// <summary>The Emberheart paid out: said over the player, as the new turn begins.</summary>
        void OnHeartFire(HeartFireEvent e)
        {
            var anchor = _anchorFor(_state.Player);
            if (anchor == null) return;
            float at = WithBeat() + Pace.S(0.2f);
            float delay = DelayTo(at);
            Motion.FloatText(_layer, PointAbove(anchor, 175f), $"+{e.Energy} Energy", Palette.Energy, 34, delay, rise: 46f);
            Fx.Flare(_layer, Motion.PointIn(_layer, anchor), new Color(1f, 0.78f, 0.3f, 0.7f), 260f, delay);
            At(at, () => AudioDirector.Play(Sfx.Buff, 0.6f, 1.2f, 0f));
        }

        void OnPotion(PotionUsedEvent e)
        {
            AudioDirector.Play(Sfx.Potion, 0.8f);
            var anchor = _anchorFor(_state.Player);
            if (anchor != null)
                Motion.FloatText(_layer, PointAbove(anchor, 110f), e.Potion.DisplayName, Palette.IntentBlock, 30, 0f, rise: 40f);
        }

        void OnCombatEnded(CombatEndedEvent e)
        {
            bool won = e.PlayerWon;
            At(BusyUntil + Pace.S(0.2f), () => AudioDirector.Play(won ? Sfx.Victory : Sfx.Defeat, 0.8f));
        }
    }
}
