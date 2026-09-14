using System;
using System.Collections.Generic;
using EmberDeck.Content;
using EmberDeck.Run;
using UnityEngine;
using UnityEngine.UI;

namespace EmberDeck.View
{
    /// <summary>
    /// The summary at the end of a run, won or lost.
    ///
    /// A run used to end on one word over the board. That wastes the one moment a roguelike has the
    /// player's full attention: the run is over, nothing is at stake, and they want to know what
    /// happened. What ended it and where, what the deck became, and a few numbers to beat next time
    /// are what turn a loss into the reason for one more run.
    /// </summary>
    public sealed class EndOfRunView : MonoBehaviour
    {
        // 55% and seven across: at 50% the rules text on a card could not be read, and seven columns
        // keep three rows of the wider cards inside the space the deck has.
        const int DeckColumns = 7;
        const int MaxDeckTiles = 21;
        const float DeckScale = 0.55f;
        const float TileStepX = CardView.Width + 24f;
        const float TileStepY = CardView.Height + 28f;
        const float StatRowHeight = 38f;

        public event Action NewRunChosen;
        public event Action MainMenuChosen;

        Text _title;
        Text _subtitle;
        RectTransform _stats;
        Text _deckTitle;
        RectTransform _deckGrid;
        Text _relics;
        readonly List<GameObject> _spawned = new();

        public bool IsOpen => gameObject.activeSelf;

        public static EndOfRunView Create(Transform parent)
        {
            var root = UiFactory.Panel(parent, "EndOfRun", Palette.Background);
            UiFactory.Stretch(root);
            var view = root.gameObject.AddComponent<EndOfRunView>();
            view.Build(root);
            return view;
        }

        void Build(RectTransform root)
        {
            _title = UiFactory.Label(root, "EndTitle", "", 96, Palette.Ink);
            _title.fontStyle = FontStyle.Bold;
            UiFactory.Place(_title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -60f), new Vector2(1200f, 120f));

            _subtitle = UiFactory.Label(root, "EndSubtitle", "", 28, Palette.InkMuted);
            UiFactory.Place(_subtitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                            new Vector2(0f, -180f), new Vector2(1400f, 40f));

            // Left: the numbers.
            var statsTitle = UiFactory.Label(root, "StatsTitle", "THE RUN", 22, Palette.Energy, TextAnchor.MiddleLeft);
            UiFactory.Place(statsTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f),
                            new Vector2(-800f, -260f), new Vector2(600f, 32f));

            _stats = (RectTransform)new GameObject("Stats", typeof(RectTransform)).transform;
            _stats.SetParent(root, false);
            UiFactory.Place(_stats, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(-800f, -300f),
                            new Vector2(600f, 560f));

            // Right: what the deck became.
            _deckTitle = UiFactory.Label(root, "DeckTitle", "", 22, Palette.Energy, TextAnchor.MiddleLeft);
            UiFactory.Place(_deckTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f),
                            new Vector2(-80f, -260f), new Vector2(880f, 32f));

            // Cards are laid out at full size inside a scaled-down grid. CardView eases its own scale
            // back to 1, so scaling the cards themselves would be undone a frame later.
            _deckGrid = (RectTransform)new GameObject("DeckGrid", typeof(RectTransform)).transform;
            _deckGrid.SetParent(root, false);
            UiFactory.Place(_deckGrid, new Vector2(0.5f, 1f), new Vector2(0f, 1f), new Vector2(-80f, -300f),
                            new Vector2(10f, 10f));
            _deckGrid.localScale = Vector3.one * DeckScale;

            _relics = UiFactory.Label(root, "Relics", "", 20, Palette.InkMuted, TextAnchor.UpperLeft);
            UiFactory.Place(_relics.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, 1f),
                            new Vector2(-80f, -800f), new Vector2(880f, 60f));

            var newRun = UiFactory.TextButton(root, "EndNewRun", "New Run", Palette.PanelRaised, Palette.Ink, 28);
            UiFactory.Place((RectTransform)newRun.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(-150f, 60f), new Vector2(260f, 72f));
            newRun.onClick.AddListener(() => NewRunChosen?.Invoke());

            var menu = UiFactory.TextButton(root, "EndMainMenu", "Main Menu", Palette.PanelRaised, Palette.Ink, 28);
            UiFactory.Place((RectTransform)menu.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                            new Vector2(150f, 60f), new Vector2(260f, 72f));
            menu.onClick.AddListener(() => MainMenuChosen?.Invoke());

            gameObject.SetActive(false);
        }

        public void Show(RunState run, bool won, int floor, int floors)
        {
            foreach (var spawned in _spawned)
            {
                if (spawned == null) continue;
                spawned.transform.SetParent(null, false);
                Destroy(spawned);
            }
            _spawned.Clear();

            var stats = run.Stats;
            string foe = string.IsNullOrEmpty(stats.FinalEncounter) ? "the forge" : stats.FinalEncounter;
            _title.text = won ? "VICTORY" : "DEFEAT";
            _title.color = won ? Palette.Victory : Palette.Defeat;
            _subtitle.text = won
                ? $"The {foe} lies in the ashes. The forge is yours."
                : $"Fell to {foe} on floor {floor} of {floors}.";

            BuildStats(run, floor, floors);
            BuildDeck(run);

            var relicNames = new List<string>();
            foreach (var relic in run.Relics)
                if (relic != null) relicNames.Add(relic.DisplayName);
            _relics.text = relicNames.Count > 0 ? "Relics:   " + string.Join(",   ", relicNames) : "No relics";

            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            Motion.Punch(_title.transform, 0.18f, 0.5f);
        }

        public void Hide() => gameObject.SetActive(false);

        void BuildStats(RunState run, int floor, int floors)
        {
            var stats = run.Stats;
            var rows = new List<(string label, string value)>
            {
                ("Floor reached", $"{floor} / {floors}"),
                ("Fights won", stats.ElitesWon > 0 ? $"{stats.FightsWon}   ({stats.ElitesWon} elite)" : stats.FightsWon.ToString()),
                ("Enemies defeated", stats.EnemiesDefeated.ToString()),
                ("Damage dealt", stats.DamageDealt.ToString()),
                ("Damage taken", stats.DamageTaken.ToString()),
                ("Biggest hit", stats.BiggestHit.ToString()),
                ("Cards played", stats.CardsPlayed.ToString()),
                ("Turns", stats.Turns.ToString()),
                ("Cards added", stats.CardsAdded.ToString()),
                ("Cards upgraded", stats.CardsUpgraded.ToString()),
                ("Cards removed", stats.CardsRemoved.ToString()),
                ("Gold earned", stats.GoldEarned.ToString()),
                ("Relics", run.Relics.Count.ToString()),
                ("Time", FormatTime(stats.Seconds)),
                ("Seed", run.Seed.ToString()),
            };

            for (int i = 0; i < rows.Count; i++)
            {
                var row = (RectTransform)new GameObject("Row", typeof(RectTransform)).transform;
                row.SetParent(_stats, false);
                UiFactory.Place(row, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -i * StatRowHeight),
                                new Vector2(600f, StatRowHeight - 2f));

                var label = UiFactory.Label(row, "Label", rows[i].label, 22, Palette.InkMuted, TextAnchor.MiddleLeft);
                UiFactory.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero,
                                new Vector2(380f, StatRowHeight));
                var value = UiFactory.Label(row, "Value", rows[i].value, 24, Palette.Ink, TextAnchor.MiddleRight);
                UiFactory.Place(value.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), Vector2.zero,
                                new Vector2(220f, StatRowHeight));

                // Rows arrive one after another, so the numbers are read as a story rather than a table.
                var group = row.gameObject.AddComponent<CanvasGroup>();
                group.alpha = 0f;
                Motion.Run(group, "reveal", 0.35f, t => group.alpha = t, Motion.OutCubic, 0.25f + i * 0.05f);

                _spawned.Add(row.gameObject);
            }
        }

        void BuildDeck(RunState run)
        {
            var counts = new List<(CardData card, int count)>();
            foreach (var card in run.Deck)
            {
                if (card == null) continue;
                int index = counts.FindIndex(entry => entry.card == card);
                if (index >= 0) counts[index] = (card, counts[index].count + 1);
                else counts.Add((card, 1));
            }
            // Rarest first: the cards that made this deck this deck, ahead of the starter cards.
            counts.Sort((a, b) =>
            {
                int byRarity = b.card.Rarity.CompareTo(a.card.Rarity);
                return byRarity != 0 ? byRarity : string.CompareOrdinal(a.card.DisplayName, b.card.DisplayName);
            });

            int shown = Mathf.Min(counts.Count, MaxDeckTiles);
            _deckTitle.text = $"FINAL DECK   ·   {run.Deck.Count} cards" +
                              (counts.Count > shown ? $"   (+{counts.Count - shown} more)" : "");

            for (int i = 0; i < shown; i++)
            {
                var (card, count) = counts[i];
                var view = CardView.Create(_deckGrid, new CardInstance(card));
                UiFactory.Place((RectTransform)view.transform, new Vector2(0f, 1f), new Vector2(0f, 1f), Vector2.zero,
                                new Vector2(CardView.Width, CardView.Height));
                // Anchored and pivoted at the top-left corner, so the position is the corner itself.
                view.SetRestPosition(new Vector2((i % DeckColumns) * TileStepX, -(i / DeckColumns) * TileStepY));
                view.Refresh(playable: true, selected: false, displayedCost: card.Cost);

                if (count > 1)
                {
                    var badge = UiFactory.Label(view.transform, "Count", $"x{count}", 46, Palette.Energy, TextAnchor.UpperRight);
                    badge.fontStyle = FontStyle.Bold;
                    var outline = badge.gameObject.AddComponent<Outline>();
                    outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                    outline.effectDistance = new Vector2(3f, -3f);
                    UiFactory.Place(badge.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-10f, -6f),
                                    new Vector2(140f, 60f));
                }

                _spawned.Add(view.gameObject);
            }

            // Relics sit under the last row of cards, however many rows there are.
            int rowsUsed = Mathf.Max(1, Mathf.CeilToInt(shown / (float)DeckColumns));
            var relicsRect = _relics.rectTransform;
            relicsRect.anchoredPosition = new Vector2(relicsRect.anchoredPosition.x,
                                                      -300f - rowsUsed * TileStepY * DeckScale - 16f);
        }

        static string FormatTime(float seconds)
        {
            var span = TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
            return span.TotalHours >= 1
                ? $"{(int)span.TotalHours}:{span.Minutes:00}:{span.Seconds:00}"
                : $"{span.Minutes}:{span.Seconds:00}";
        }
    }
}
