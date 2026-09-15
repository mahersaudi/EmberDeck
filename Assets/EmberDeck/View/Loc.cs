using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace EmberDeck.View
{
    public enum Language { English, Arabic }

    /// <summary>
    /// Translation, done where text meets the screen.
    ///
    /// The game's strings stay English in code, and every label translates what it is given as it is given
    /// it (UiText). Wrapping each of several hundred strings at its call site would touch every screen and
    /// every effect's description, and English would then exist twice: once for the rules code that reads
    /// it (Keywords.ForCard reads card text) and once for display. Here the English is the key.
    ///
    /// The table is Resources/Localization/ar.txt: an English line, then its Arabic on a line beginning
    /// "= ". A key with {0}, {1}… is a template: "Deal {0} damage." matches "Deal 6 damage." and its Arabic
    /// receives the 6. A placeholder that captured words is translated in turn, so "Apply 2 Burn to the
    /// target." fills its status name in Arabic too. A string no key matches whole is tried a sentence at a
    /// time, which is how a card's description, built sentence by sentence from its effects, translates.
    ///
    /// Anything still unmatched is shown in English and collected in Missing, which the capture harness
    /// prints, so an untranslated string is found by running the game, not by reading code.
    /// </summary>
    public static class Loc
    {
        const string PrefKey = "emberdeck.settings.language";

        sealed class Template
        {
            public string Prefix;
            public Regex Pattern;
            public string Arabic;
            public int Literal;   // non-placeholder characters: more specific templates are tried first
        }

        static Language? _language;
        static bool _persist = true;
        static Dictionary<string, string> _exact;
        static List<Template> _templates;
        static readonly Dictionary<string, string> Memo = new();

        /// <summary>English strings shown in Arabic mode with no translation, for the capture harness.</summary>
        public static readonly SortedSet<string> Missing = new(StringComparer.Ordinal);

        static readonly Regex SentenceBreak = new(@"(?<=[.!?])\s+", RegexOptions.Compiled);
        static readonly Regex TagSplit = new(@"(<[^>]+>)", RegexOptions.Compiled);
        static readonly Regex Placeholder = new(@"\\\{(\d)}", RegexOptions.Compiled);

        public static Language Language
        {
            get
            {
                _language ??= (Language)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, 1);
                return _language.Value;
            }
        }

        /// <summary>True for a right-to-left language: text is shaped and the layout mirrored.</summary>
        public static bool IsRtl => Language == Language.Arabic;

        public static string DisplayName(Language language) => language == Language.Arabic ? "العربية" : "English";

        /// <summary>Chooses the language. It takes effect when the interface is next built (the main menu reloads it).</summary>
        public static void SetLanguage(Language language)
        {
            if (Language == language) return;
            _language = language;
            Memo.Clear();
            if (_persist)
            {
                PlayerPrefs.SetInt(PrefKey, (int)language);
                PlayerPrefs.Save();
            }
        }

        /// <summary>Capture-harness use: a language for this run only, never written to PlayerPrefs.</summary>
        public static void UseMemoryOnly(Language language)
        {
            _persist = false;
            _language = language;
            Memo.Clear();
        }

        public static string T(string english) => Translate(english);

        public static string Translate(string text)
        {
            if (!IsRtl || string.IsNullOrEmpty(text) || !HasLatinLetter(text)) return text;
            if (Memo.TryGetValue(text, out var cached)) return cached;
            EnsureLoaded();

            string result = text.IndexOf('<') >= 0 ? TranslateTagged(text) : TranslateSpan(text);
            if (Memo.Count > 5000) Memo.Clear();
            Memo[text] = result;
            return result;
        }

        /// <summary>"Emberling and Cinder Rat", each name translated and joined the language's way.</summary>
        public static string Names(string english)
        {
            if (!IsRtl || string.IsNullOrEmpty(english)) return english;
            var names = english.Split(new[] { " and " }, StringSplitOptions.None);
            for (int i = 0; i < names.Length; i++) names[i] = Translate(names[i]);
            return string.Join(" و", names);
        }

        static bool HasLatinLetter(string text)
        {
            foreach (char c in text)
                if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z')) return true;
            return false;
        }

        /// <summary>Text between tags is translated piece by piece; the tags stay where they are.</summary>
        static string TranslateTagged(string text)
        {
            var builder = new StringBuilder(text.Length);
            foreach (var part in TagSplit.Split(text))
                builder.Append(part.StartsWith("<") && part.EndsWith(">") ? part : TranslateSpan(part));
            return builder.ToString();
        }

        /// <summary>One span of plain text, its surrounding whitespace kept, line by line.</summary>
        static string TranslateSpan(string text)
        {
            if (!HasLatinLetter(text)) return text;
            int start = 0, end = text.Length;
            while (start < end && char.IsWhiteSpace(text[start])) start++;
            while (end > start && char.IsWhiteSpace(text[end - 1])) end--;
            string core = text.Substring(start, end - start);

            string translated;
            if (core.IndexOf('\n') >= 0)
            {
                var lines = core.Split('\n');
                for (int i = 0; i < lines.Length; i++) lines[i] = TranslateSpan(lines[i]);
                translated = string.Join("\n", lines);
            }
            else translated = Whole(core) ?? Sentences(core) ?? Record(core);

            return text.Substring(0, start) + translated + text.Substring(end);
        }

        static string Record(string english)
        {
            Missing.Add(english);
            return english;
        }

        /// <summary>
        /// A sentence at a time, when the whole has no key. Null when there is only one sentence; otherwise the
        /// sentences that translate are translated, and the ones that do not are recorded and left in English.
        /// </summary>
        static string Sentences(string text)
        {
            var sentences = SentenceBreak.Split(text);
            if (sentences.Length < 2) return null;

            for (int i = 0; i < sentences.Length; i++)
                sentences[i] = Whole(sentences[i]) ?? Record(sentences[i]);
            return string.Join(" ", sentences);
        }

        /// <summary>
        /// An exact key or a template for the whole string; null when neither matches. A template matches only if
        /// every placeholder's words translate too. Otherwise it was the wrong template: "Heat {0}" would claim the
        /// card "Heat Shield+", and "Gain {0} Block." would swallow "Gain 4 Heat. Gain 7 Block." whole.
        /// </summary>
        static string Whole(string text)
        {
            if (!HasLatinLetter(text)) return text;
            if (_exact.TryGetValue(text, out var exact)) return exact;

            foreach (var template in _templates)
            {
                if (template.Prefix.Length > 0 && !text.StartsWith(template.Prefix, StringComparison.Ordinal)) continue;
                var match = template.Pattern.Match(text);
                if (!match.Success) continue;

                var builder = new StringBuilder(template.Arabic);
                bool complete = true;
                for (int g = 1; g < match.Groups.Count && complete; g++)
                {
                    string value = match.Groups[g].Value;
                    string part = HasLatinLetter(value) ? Whole(value) : value;
                    if (part == null) complete = false;
                    else builder.Replace("{" + (g - 1) + "}", part);
                }
                if (complete) return builder.ToString();
            }
            return null;
        }

        static void EnsureLoaded()
        {
            if (_exact != null) return;
            _exact = new Dictionary<string, string>(StringComparer.Ordinal);
            _templates = new List<Template>();

            var asset = Resources.Load<TextAsset>("Localization/ar");
            if (asset == null)
            {
                Debug.LogWarning("[EmberDeck] No Arabic translation table at Resources/Localization/ar.txt.");
                return;
            }

            string pending = null;
            foreach (var raw in asset.text.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0 || line.StartsWith("#"))
                {
                    pending = null;
                    continue;
                }
                if (line.StartsWith("= "))
                {
                    if (pending != null) Add(pending, Unescape(line.Substring(2)));
                    pending = null;
                    continue;
                }
                pending = Unescape(line.Trim());
            }

            // Longest literal text first, so "Deal {0} damage {1} times." is tried before "Deal {0} damage.".
            _templates.Sort((a, b) => b.Literal.CompareTo(a.Literal));
        }

        static void Add(string english, string arabic)
        {
            if (english.IndexOf("{0}", StringComparison.Ordinal) < 0)
            {
                _exact[english] = arabic;
                return;
            }

            string pattern = Placeholder.Replace(Regex.Escape(english), "(.+?)");
            _templates.Add(new Template
            {
                Prefix = english.Substring(0, english.IndexOf('{')),
                Pattern = new Regex("^" + pattern + "$", RegexOptions.Singleline),
                Arabic = arabic,
                Literal = Regex.Replace(english, @"\{\d}", "").Length,
            });
        }

        static string Unescape(string value) => value.Replace("\\n", "\n");
    }
}
