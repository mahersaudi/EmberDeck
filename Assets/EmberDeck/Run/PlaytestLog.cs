using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace EmberDeck.Run
{
    /// <summary>
    /// One line per finished run, appended to playtest-log.txt beside the save.
    ///
    /// The balance numbers so far come from a bot that plays like a beginner on purpose. What a playtest has
    /// to answer is what real players do: how far they get, how long a run takes them, what kills them. Asking
    /// each tester to remember that is unreliable; a file they send back is not. The main menu's playtest-log
    /// button opens its folder, because on macOS it sits inside the hidden Library folder.
    ///
    /// Plain text rather than JSON so a tester can open it and see there is nothing private in it: dates,
    /// numbers, encounter names and the seed.
    /// </summary>
    public static class PlaytestLog
    {
        const string FileName = "playtest-log.txt";

        /// <summary>Capture-harness use: write into the capture folder, never beside the real save.</summary>
        public static string DirectoryOverride;

        public static string Folder => DirectoryOverride ?? Application.persistentDataPath;

        public static string FilePath => Path.Combine(Folder, FileName);

        public static void RecordRun(RunState run, bool won, int floor, int floors, string language)
        {
            var stats = run.Stats;
            string line = string.Join(" | ",
                DateTime.Now.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
                $"v{Application.version}",
                won ? "WON" : "LOST",
                $"act {run.Act}",
                $"floor {floor}/{floors}",
                $"difficulty {run.Difficulty}",
                $"time {Mathf.RoundToInt(stats.Seconds / 60f)} min",
                $"fights won {stats.FightsWon}",
                $"elites won {stats.ElitesWon}",
                $"deck {run.Deck.Count}",
                $"relics {run.Relics.Count}",
                $"damage taken {stats.DamageTaken}",
                $"potions used {stats.PotionsUsed}",
                $"last fight {(string.IsNullOrEmpty(stats.FinalEncounter) ? "-" : stats.FinalEncounter)}",
                $"language {language}",
                $"seed {run.Seed}");

            try
            {
                Directory.CreateDirectory(Folder);
                if (!File.Exists(FilePath))
                    File.WriteAllText(FilePath, "# EmberDeck playtest log: one line per finished run. Please send this file back." + Environment.NewLine);
                File.AppendAllText(FilePath, line + Environment.NewLine);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[EmberDeck] Could not write the playtest log: {e.Message}");
            }
        }
    }
}
