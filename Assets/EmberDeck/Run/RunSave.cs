using System;
using System.Collections.Generic;
using System.IO;
using EmberDeck.Content;
using UnityEngine;

namespace EmberDeck.Run
{
    /// <summary>
    /// Saves and restores a run.
    ///
    /// The map is not saved. It is generated deterministically from the run seed, so the seed
    /// alone reproduces it exactly — what actually has to persist is where the player stands
    /// and which nodes they have already burned. This is the direct payoff of building the
    /// RNG as seeded streams on day one: the alternative is serialising a graph of nodes and
    /// edges and keeping that format in step with the generator forever.
    ///
    /// Saving happens at the map, not mid-combat. Quitting inside a fight loses that fight.
    /// Mid-combat saving means serialising the hand, all four piles, every status, the heat
    /// and each enemy's intent — worth doing, but it is its own piece of work and pretending
    /// otherwise would ship a save that silently corrupts.
    /// </summary>
    public static class RunSave
    {
        const string FileName = "run.json";

        static string Path => System.IO.Path.Combine(Application.persistentDataPath, FileName);

        [Serializable]
        sealed class Data
        {
            public int version = 4;
            public int seed;
            public int hp;
            public int maxHp;
            public int fightNumber;
            public List<string> deck = new();
            public List<string> relics = new();
            public List<int> visited = new();   // flattened row, column pairs
            public int currentRow = -1;
            public int currentColumn = -1;
            public RunStats stats = new();   // version 3
            public int gold = -1;            // version 4
            public int cardsRemoved;         // version 4
        }

        public static bool Exists() => File.Exists(Path);

        public static void Delete()
        {
            if (File.Exists(Path)) File.Delete(Path);
        }

        public static void Write(RunState run)
        {
            var data = new Data
            {
                seed = run.Seed,
                hp = run.Hp,
                maxHp = run.MaxHp,
                fightNumber = run.FightNumber,
                currentRow = run.Map?.Current?.Row ?? -1,
                currentColumn = run.Map?.Current?.Column ?? -1,
                stats = run.Stats,
                gold = run.Gold,
                cardsRemoved = run.CardsRemoved,
            };

            foreach (var card in run.Deck)
                if (card != null) data.deck.Add(card.Id);

            foreach (var relic in run.Relics)
                if (relic != null) data.relics.Add(relic.Id);

            if (run.Map != null)
                foreach (var row in run.Map.Grid)
                    foreach (var node in row)
                        if (node.Visited) { data.visited.Add(node.Row); data.visited.Add(node.Column); }

            try
            {
                File.WriteAllText(Path, JsonUtility.ToJson(data, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[EmberDeck] Could not write save: {e.Message}");
            }
        }

        /// <summary>Returns null when there is no save, or when the file cannot be trusted.</summary>
        public static RunState Read(RunConfig config)
        {
            if (!File.Exists(Path)) return null;

            Data data;
            try
            {
                data = JsonUtility.FromJson<Data>(File.ReadAllText(Path));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EmberDeck] Save unreadable, starting fresh: {e.Message}");
                Delete();
                return null;
            }

            if (data == null || (data.version < 1 || data.version > 4))
            {
                Debug.LogWarning("[EmberDeck] Save is from a different version, starting fresh.");
                Delete();
                return null;
            }

            // The map comes back from the seed, not from the file.
            var run = new RunState(data.seed, data.maxHp);
            run.Map = RunMap.Generate(run.Rng.Map);
            run.Hp = Mathf.Clamp(data.hp, 0, data.maxHp);
            run.FightNumber = Mathf.Max(1, data.fightNumber);
            // Saves before version 3 carry no stats; the run continues with a fresh count.
            run.Stats = data.stats ?? new RunStats();
            // Saves before version 4 predate gold; they continue with the starting purse.
            run.Gold = data.gold >= 0 ? data.gold : config.StartingGold;
            run.CardsRemoved = Mathf.Max(0, data.cardsRemoved);

            foreach (var id in data.deck)
            {
                var card = config.FindCard(id);
                if (card != null) run.AddCard(card);
                else Debug.LogWarning($"[EmberDeck] Save names a card that no longer exists: {id}");
            }

            foreach (var id in data.relics)
            {
                var relic = config.FindRelic(id);
                if (relic != null) run.Relics.Add(relic);
                else Debug.LogWarning($"[EmberDeck] Save names a relic that no longer exists: {id}");
            }

            // Version-1 saves predate relics and carry none; they get the starting relics.
            if (data.relics.Count == 0)
                foreach (var relic in config.Relics)
                    if (relic != null) run.Relics.Add(relic);

            // A deck that lost every card to a content change is not a run worth resuming.
            if (run.Deck.Count == 0)
            {
                Debug.LogWarning("[EmberDeck] Save resolved to an empty deck, starting fresh.");
                Delete();
                return null;
            }

            for (int i = 0; i + 1 < data.visited.Count; i += 2)
            {
                var node = FindNode(run.Map, data.visited[i], data.visited[i + 1]);
                if (node != null) node.Visited = true;
            }

            run.Map.Current = FindNode(run.Map, data.currentRow, data.currentColumn);
            return run;
        }

        static MapNode FindNode(RunMap map, int row, int column)
        {
            if (row < 0 || row >= map.Grid.Count) return null;
            foreach (var node in map.Grid[row])
                if (node.Column == column) return node;
            return null;
        }
    }
}
