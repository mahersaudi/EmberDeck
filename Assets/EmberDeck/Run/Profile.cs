using System;
using System.Collections.Generic;
using System.IO;
using EmberDeck.Content;
using UnityEngine;

namespace EmberDeck.Run
{
    /// <summary>What the player has earned across every run. Saved as profile.json.</summary>
    [Serializable]
    public sealed class ProfileData
    {
        public int version = 1;
        public int embers;
        public int runs;
        public int wins;
        public int bestFloor;
        /// <summary>The highest difficulty that can be chosen. 0 until the first win.</summary>
        public int maxDifficulty;
        /// <summary>The difficulty the next new run starts at.</summary>
        public int difficulty;
        /// <summary>
        /// Card ids of the last deck built, so the builder opens on the deck that was just played
        /// rather than on an empty one. Cards no longer in the pool are dropped when it is read.
        /// </summary>
        public List<string> deck = new();
        /// <summary>Three slots the player can save a deck into and load it back from.</summary>
        public List<SavedDeck> savedDecks = new();
    }

    /// <summary>One saved deck. A class rather than a nested list because JsonUtility cannot serialise those.</summary>
    [Serializable]
    public sealed class SavedDeck
    {
        public List<string> cards = new();
    }

    /// <summary>What one finished run added to the profile, for the end-of-run screen.</summary>
    public sealed class RunResult
    {
        public int Earned;
        public int Before;
        public int After;
        public readonly List<UnlockData> Unlocked = new();
        /// <summary>The difficulty this run opened, or -1.</summary>
        public int DifficultyUnlocked = -1;
        public bool NewBestFloor;
    }

    /// <summary>
    /// The profile, in its own file beside the run save.
    ///
    /// Separate from run.json on purpose: the run save is deleted when a run ends or is abandoned, and
    /// the profile must survive both. A profile that cannot be read is set aside as profile.bad.json
    /// rather than overwritten, so a corrupted file costs a fresh start, not the earned progress with it.
    /// </summary>
    public static class Profile
    {
        const string FileName = "profile.json";
        const string BadFileName = "profile.bad.json";

        static ProfileData _data;
        static bool _persist = true;

        static string PathOf(string name) => System.IO.Path.Combine(Application.persistentDataPath, name);

        public static ProfileData Data
        {
            get
            {
                if (_data == null) Load();
                return _data;
            }
        }

        /// <summary>
        /// Capture-harness use: a profile held in memory and never written, so a capture run neither
        /// reads nor changes the progress of the person at this machine.
        /// </summary>
        public static void UseMemoryOnly(int embers = 0, int maxDifficulty = 0)
        {
            _persist = false;
            _data = new ProfileData { embers = embers, maxDifficulty = maxDifficulty };
        }

        /// <summary>Remembers the deck a run was started with.</summary>
        public static void SetDeck(List<string> ids)
        {
            Data.deck = ids ?? new List<string>();
            Save();
        }

        public const int DeckSlots = 3;

        /// <summary>The card ids in a slot, or an empty list if nothing has been saved there.</summary>
        public static List<string> SavedDeck(int slot)
        {
            var decks = Data.savedDecks;
            return slot >= 0 && slot < decks.Count && decks[slot]?.cards != null ? decks[slot].cards : new List<string>();
        }

        public static void SaveDeck(int slot, List<string> ids)
        {
            if (slot < 0 || slot >= DeckSlots) return;
            var decks = Data.savedDecks;
            while (decks.Count <= slot) decks.Add(new SavedDeck());
            decks[slot] = new SavedDeck { cards = new List<string>(ids ?? new List<string>()) };
            Save();
        }

        public static void SetDifficulty(int level)
        {
            Data.difficulty = Mathf.Clamp(level, 0, Data.maxDifficulty);
            Save();
        }

        /// <summary>Adds a finished run to the profile. Call once per run.</summary>
        public static RunResult RecordRun(RunState run, RunConfig config, bool won, int floor)
        {
            var data = Data;
            var result = new RunResult { Before = data.embers, Earned = UnlockService.Score(run, won, floor) };

            data.embers += result.Earned;
            result.After = data.embers;
            data.runs++;
            if (won) data.wins++;
            if (floor > data.bestFloor)
            {
                data.bestFloor = floor;
                result.NewBestFloor = true;
            }

            result.Unlocked.AddRange(UnlockService.Crossed(config, result.Before, result.After));

            // Winning at the highest level reached opens the next one. Winning below it opens nothing, so a
            // level has to be beaten to see the one after.
            if (won && run.Difficulty >= data.maxDifficulty && data.maxDifficulty < DifficultyRules.Max)
            {
                data.maxDifficulty = run.Difficulty + 1;
                result.DifficultyUnlocked = data.maxDifficulty;
            }

            Save();
            return result;
        }

        static void Load()
        {
            _data = null;
            string path = PathOf(FileName);
            if (_persist && File.Exists(path))
            {
                try
                {
                    _data = JsonUtility.FromJson<ProfileData>(File.ReadAllText(path));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[EmberDeck] Profile unreadable, set aside as {BadFileName}: {e.Message}");
                    try { File.Copy(path, PathOf(BadFileName), overwrite: true); } catch (Exception) { /* nothing more to save */ }
                }
            }

            _data ??= new ProfileData();
            _data.deck ??= new List<string>();
            _data.savedDecks ??= new List<SavedDeck>();
            _data.embers = Mathf.Max(0, _data.embers);
            _data.maxDifficulty = Mathf.Clamp(_data.maxDifficulty, 0, DifficultyRules.Max);
            _data.difficulty = Mathf.Clamp(_data.difficulty, 0, _data.maxDifficulty);
        }

        static void Save()
        {
            if (!_persist || _data == null) return;
            try
            {
                File.WriteAllText(PathOf(FileName), JsonUtility.ToJson(_data, prettyPrint: true));
            }
            catch (Exception e)
            {
                Debug.LogError($"[EmberDeck] Could not write profile: {e.Message}");
            }
        }
    }
}
