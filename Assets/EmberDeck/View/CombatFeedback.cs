using System;
using EmberDeck.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// Turns combat events into motion and sound.
    ///
    /// The engine resolves a whole enemy turn inside one call, so every hit of that turn arrives
    /// in the same frame. Played as it arrives, three enemies' attacks are one blurred thud under
    /// a pile of overlapping numbers. Events from the same frame are therefore staggered: the
    /// rules have already finished, and the view replays what happened at a pace a person can
    /// read.
    ///
    /// It subscribes to the bus the way a relic does, and only ever reads. The session clears the
    /// bus when the fight ends, which is what detaches it.
    /// </summary>
    public sealed class CombatFeedback
    {
        const float Stagger = 0.16f;
        const float MaxStagger = 1.6f;
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

        int _frame = -1;
        int _eventsThisFrame;
        int _drawsThisFrame;

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
            bus.Subscribe<CardPlayedEvent>(_ => AudioDirector.Play(Sfx.CardPlay));
            bus.Subscribe<CardDrawnEvent>(OnCardDrawn);
            bus.Subscribe<StatusAppliedEvent>(OnStatus);
            bus.Subscribe<HeatGainedEvent>(OnHeat);
            bus.Subscribe<TurnStartedEvent>(OnTurnStarted);
            bus.Subscribe<CombatEndedEvent>(OnCombatEnded);
            bus.Subscribe<PotionUsedEvent>(OnPotion);
        }

        void NewFrameCheck()
        {
            if (Time.frameCount == _frame) return;
            _frame = Time.frameCount;
            _eventsThisFrame = 0;
            _drawsThisFrame = 0;
        }

        float NextDelay()
        {
            NewFrameCheck();
            return Mathf.Min(_eventsThisFrame++ * Stagger, MaxStagger);
        }

        Vector2 PointAbove(RectTransform anchor, float height) => Motion.PointIn(_layer, anchor, new Vector2(0f, height));

        void OnDamage(DamageAppliedEvent e)
        {
            var anchor = _anchorFor(e.Target);
            if (anchor == null || (e.HpLost <= 0 && e.BlockAbsorbed <= 0)) return;

            float delay = NextDelay();

            if (e.HpLost > 0)
            {
                bool heavy = e.HpLost >= HeavyHit;
                bool fromAttack = e.Source != null;

                Motion.FloatText(_layer, PointAbove(anchor, 30f), $"-{e.HpLost}",
                                 fromAttack ? (e.Target.IsPlayer ? Palette.IntentAttack : Palette.Ink) : BurnColor,
                                 heavy ? 60 : 46, delay);
                Motion.Shake(anchor, Mathf.Clamp(e.HpLost * 1.1f, 6f, 30f), heavy ? 0.42f : 0.3f, delay);
                Motion.Flash(_flashFor(e.Target), fromAttack ? HitFlash : new Color(BurnColor.r, BurnColor.g, BurnColor.b, 0.45f),
                             0.32f, delay);

                // Where the hit lands, drawn on top of it: a flare of light inside an expanding ring.
                // Sized by the damage, so a 20-point blow does not look like a 3-point one.
                var impact = fromAttack ? (e.Target.IsPlayer ? ImpactOnPlayer : ImpactOnEnemy) : BurnColor;
                var centre = Motion.PointIn(_layer, anchor);
                float size = Mathf.Lerp(150f, 320f, Mathf.Clamp01(e.HpLost / 24f));
                Fx.Flare(_layer, centre, new Color(impact.r, impact.g, impact.b, 0.75f), size * 0.8f, delay);
                Fx.Burst(_layer, centre, new Color(impact.r, impact.g, impact.b, 0.9f), size, delay);

                // The board itself moves for a blow that matters. Small, and only for heavy hits or a
                // hit on the player: a screen that shakes at every 3-point jab is exhausting to read.
                if (heavy || (e.Target.IsPlayer && fromAttack))
                    Motion.Shake(_board, heavy ? 9f : 5f, 0.34f, delay);

                var sound = !fromAttack ? Sfx.Burn : heavy ? Sfx.HeavyHit : Sfx.Hit;
                bool hurtPlayer = e.Target.IsPlayer && fromAttack;
                float recoil = Mathf.Clamp(e.HpLost * 1.3f, 5f, 26f);
                var hitView = _viewFor?.Invoke(e.Target);
                Motion.After(delay, () =>
                {
                    AudioDirector.Play(sound, fromAttack ? 1f : 0.6f);
                    if (hurtPlayer) AudioDirector.Play(Sfx.PlayerHurt, 0.65f);
                    if (hitView != null) hitView.Recoil(recoil);
                });
            }
            else
            {
                Motion.FloatText(_layer, PointAbove(anchor, 30f), "Blocked", Palette.Block, 30, delay);
                Motion.Shake(anchor, 4f, 0.18f, delay);
                Fx.Burst(_layer, Motion.PointIn(_layer, anchor), new Color(Palette.Block.r, Palette.Block.g, Palette.Block.b, 0.8f),
                         170f, delay, 0.34f);
                Motion.After(delay, () => AudioDirector.Play(Sfx.BlockedHit));
            }
        }

        void OnBlock(BlockGainedEvent e)
        {
            var anchor = _anchorFor(e.Target);
            if (anchor == null || e.Amount <= 0) return;

            float delay = NextDelay();
            Motion.FloatText(_layer, PointAbove(anchor, 70f), $"+{e.Amount} Block", Palette.Block, 32, delay, rise: 50f);
            Motion.After(delay, () => AudioDirector.Play(Sfx.BlockGain, 0.8f));
        }

        void OnDied(ActorDiedEvent e)
        {
            if (e.Actor.IsPlayer) return;   // the defeat sting covers it
            var anchor = _anchorFor(e.Actor);
            if (anchor == null) return;

            float delay = NextDelay() + 0.1f;
            Motion.Run(anchor, "death", 0.55f,
                       t => anchor.localScale = Vector3.one * (1f - 0.14f * t),
                       Motion.OutCubic, delay);
            _viewFor?.Invoke(e.Actor)?.Die(delay);
            Fx.Burst(_layer, Motion.PointIn(_layer, anchor), new Color(1f, 0.72f, 0.4f, 0.75f), 300f, delay, 0.6f);
            Motion.After(delay, () => AudioDirector.Play(Sfx.EnemyDeath));
        }

        void OnCardDrawn(CardDrawnEvent _)
        {
            NewFrameCheck();
            int index = _drawsThisFrame++;
            // Each card in a draw is a little higher, so a five-card hand reads as a riffle.
            Motion.After(0.05f + index * 0.07f, () => AudioDirector.Play(Sfx.CardDraw, 0.7f, 1f + index * 0.035f, 0f));
        }

        void OnStatus(StatusAppliedEvent e)
        {
            if (e.Amount <= 0) return;
            var anchor = _anchorFor(e.Target);
            if (anchor == null) return;

            bool harmful = e.Status is StatusType.Burn or StatusType.Vulnerable or StatusType.Weak;
            var color = e.Status == StatusType.Burn ? BurnColor : harmful ? DebuffColor : Palette.IntentBuff;
            var sound = e.Status == StatusType.Burn ? Sfx.Burn : harmful ? Sfx.Debuff : Sfx.Buff;

            float delay = NextDelay();
            Motion.FloatText(_layer, PointAbove(anchor, 100f), $"+{e.Amount} {e.Status.DisplayName()}", color, 28, delay, rise: 44f);
            Motion.After(delay, () => AudioDirector.Play(sound, 0.7f));
        }

        void OnHeat(HeatGainedEvent e)
        {
            if (e.Amount <= 0) return;
            int threshold = _state.OverheatThreshold;
            bool crossed = e.Total > threshold && e.Total - e.Amount <= threshold;
            AudioDirector.Play(crossed ? Sfx.Overheat : Sfx.Heat, crossed ? 0.9f : 0.55f);
        }

        void OnTurnStarted(TurnStartedEvent e)
        {
            if (!e.IsPlayerTurn) return;
            // After the enemy turn's staggered hits, not on top of them.
            Motion.After(NextDelay() + 0.15f, () => AudioDirector.Play(Sfx.TurnStart, 0.8f));
        }

        void OnPotion(PotionUsedEvent e)
        {
            // A bright, rising whoosh — the heat sound pitched up reads as a cork and a swallow.
            AudioDirector.Play(Sfx.Heat, 0.9f, 1.35f, 0f);
            var anchor = _anchorFor(_state.Player);
            if (anchor != null)
                Motion.FloatText(_layer, PointAbove(anchor, 110f), e.Potion.DisplayName, Palette.IntentBlock, 30, 0f, rise: 40f);
        }

        void OnCombatEnded(CombatEndedEvent e)
        {
            bool won = e.PlayerWon;
            Motion.After(NextDelay() + 0.3f, () => AudioDirector.Play(won ? Sfx.Victory : Sfx.Defeat));
        }
    }
}
