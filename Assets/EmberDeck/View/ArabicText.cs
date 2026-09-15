using System;
using System.Collections.Generic;
using System.Text;

namespace EmberDeck.View
{
    /// <summary>
    /// Arabic for the legacy Text component, which knows nothing of it: letters are joined into their
    /// contextual forms, lines are broken, and each line is put into visual right-to-left order.
    ///
    /// Legacy Text draws code points one after another, left to right, with no shaping. Arabic written
    /// that way comes out as isolated letters in reverse. So the text is converted to the Presentation
    /// Forms-B code points (every letter in its isolated, final, initial or medial form, plus the lam-alef
    /// ligatures) and reordered here, and Text is handed a string it can draw as-is. That is why the Arabic
    /// font must map those code points; many modern fonts shape through OpenType instead and do not
    /// (docs/localization.md).
    ///
    /// Lines are broken before reordering, because a string reversed first would wrap its last words onto
    /// the first line. Rich-text tags are lifted off the characters and put back afterwards, so a coloured
    /// keyword stays coloured after its letters have moved.
    /// </summary>
    public static class ArabicText
    {
        // Letters in the order their forms are laid out from U+FE80, with how many forms each has:
        // 4 joins on both sides (isolated, final, initial, medial), 2 only to the letter before it.
        static readonly (char Letter, int Count)[] FormLayout =
        {
            ('ء', 1), ('آ', 2), ('أ', 2), ('ؤ', 2), ('إ', 2), ('ئ', 4), ('ا', 2),
            ('ب', 4), ('ة', 2), ('ت', 4), ('ث', 4), ('ج', 4), ('ح', 4), ('خ', 4),
            ('د', 2), ('ذ', 2), ('ر', 2), ('ز', 2), ('س', 4), ('ش', 4), ('ص', 4),
            ('ض', 4), ('ط', 4), ('ظ', 4), ('ع', 4), ('غ', 4), ('ف', 4), ('ق', 4),
            ('ك', 4), ('ل', 4), ('م', 4), ('ن', 4), ('ه', 4), ('و', 2), ('ى', 2),
            ('ي', 4),
        };

        const char Tatweel = 'ـ';
        const char Lam = 'ل';

        struct Forms
        {
            public char Isolated, Final, Initial, Medial;
            public int Count;
        }

        static readonly Dictionary<char, Forms> Letters = new();

        // Lam followed by an alef becomes one glyph: (isolated, final).
        static readonly Dictionary<char, (char Isolated, char Final)> LamAlef = new()
        {
            ['آ'] = ('ﻵ', 'ﻶ'),
            ['أ'] = ('ﻷ', 'ﻸ'),
            ['إ'] = ('ﻹ', 'ﻺ'),
            ['ا'] = ('ﻻ', 'ﻼ'),
        };

        static readonly Dictionary<char, char> Mirrored = new()
        {
            ['('] = ')', [')'] = '(', ['['] = ']', [']'] = '[', ['{'] = '}', ['}'] = '{', ['«'] = '»', ['»'] = '«',
        };

        static ArabicText()
        {
            int code = 0xFE80;
            foreach (var (letter, count) in FormLayout)
            {
                Letters[letter] = new Forms
                {
                    Isolated = (char)code,
                    Final = count > 1 ? (char)(code + 1) : (char)code,
                    Initial = count == 4 ? (char)(code + 2) : '\0',
                    Medial = count == 4 ? (char)(code + 3) : '\0',
                    Count = count,
                };
                code += count;
            }
        }

        public static bool IsArabic(char c) =>
            (c >= '؀' && c <= 'ۿ') || (c >= 'ﭐ' && c <= '﷿') || (c >= 'ﹰ' && c <= '﻿');

        public static bool HasArabic(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (char c in text)
                if (IsArabic(c)) return true;
            return false;
        }

        // Harakat are dropped: legacy Text cannot place a mark over the letter before it, and a mark drawn
        // as its own glyph lands on the next letter. The translations are written without them.
        static bool IsMark(char c) => (c >= 'ً' && c <= 'ٟ') || c == 'ٰ';

        static bool JoinsToNext(char c) => c == Tatweel || (Letters.TryGetValue(c, out var f) && f.Count == 4);
        static bool JoinsToPrevious(char c) => c == Tatweel || (Letters.TryGetValue(c, out var f) && f.Count > 1);

        struct Glyph
        {
            public char C;
            public string Tags;   // the rich-text tags open around this character, "" when none
        }

        /// <summary>
        /// The string legacy Text should draw for <paramref name="text"/>: shaped, broken into lines no wider
        /// than <paramref name="maxWidth"/> when <paramref name="wrap"/> is set, and in visual order.
        /// </summary>
        public static string Layout(string text, Func<string, float> measure, float maxWidth, bool wrap)
        {
            var glyphs = Parse(text);
            var output = new StringBuilder(text.Length + 16);

            int start = 0;
            for (int i = 0; i <= glyphs.Count; i++)
            {
                if (i < glyphs.Count && glyphs[i].C != '\n') continue;
                var paragraph = glyphs.GetRange(start, i - start);
                foreach (var line in wrap ? Break(paragraph, measure, maxWidth) : new List<List<Glyph>> { paragraph })
                {
                    if (output.Length > 0 || start > 0) output.Append('\n');
                    Emit(Reorder(Shape(line)), output);
                }
                start = i + 1;
            }
            return output.ToString();
        }

        /// <summary>The characters with their tags lifted off. Anything that is not a known tag is text.</summary>
        static List<Glyph> Parse(string text)
        {
            var glyphs = new List<Glyph>(text.Length);
            var open = new List<string>();
            string key = "";

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '<')
                {
                    int close = text.IndexOf('>', i + 1);
                    if (close > i)
                    {
                        string inner = text.Substring(i + 1, close - i - 1);
                        string name = inner.TrimStart('/');
                        int equals = name.IndexOf('=');
                        if (equals >= 0) name = name.Substring(0, equals);
                        if (name == "color" || name == "b" || name == "i" || name == "size")
                        {
                            if (inner.StartsWith("/"))
                            {
                                for (int k = open.Count - 1; k >= 0; k--)
                                    if (TagName(open[k]) == name) { open.RemoveAt(k); break; }
                            }
                            else open.Add(inner);
                            key = string.Join("\u0001", open);
                            i = close;
                            continue;
                        }
                    }
                }
                glyphs.Add(new Glyph { C = c, Tags = key });
            }
            return glyphs;
        }

        static string TagName(string tag)
        {
            int equals = tag.IndexOf('=');
            return equals >= 0 ? tag.Substring(0, equals) : tag;
        }

        /// <summary>Greedy line breaking at spaces, measured on shaped words (a shaped letter can be narrower).</summary>
        static List<List<Glyph>> Break(List<Glyph> paragraph, Func<string, float> measure, float maxWidth)
        {
            var lines = new List<List<Glyph>>();
            var line = new List<Glyph>();
            float lineWidth = 0f;
            float space = measure(" ");

            int i = 0;
            while (i < paragraph.Count)
            {
                int end = i;
                while (end < paragraph.Count && paragraph[end].C != ' ') end++;
                var word = paragraph.GetRange(i, end - i);
                float width = measure(Plain(Shape(word)));

                float needed = line.Count == 0 ? width : lineWidth + space + width;
                if (line.Count > 0 && needed > maxWidth)
                {
                    lines.Add(line);
                    line = new List<Glyph>();
                    lineWidth = 0f;
                    needed = width;
                }
                if (line.Count > 0) line.Add(paragraph[i - 1]);   // the space before this word
                line.AddRange(word);
                lineWidth = needed;
                i = end + 1;
            }
            lines.Add(line);
            return lines;
        }

        static string Plain(List<Glyph> glyphs)
        {
            var builder = new StringBuilder(glyphs.Count);
            foreach (var glyph in glyphs) builder.Append(glyph.C);
            return builder.ToString();
        }

        /// <summary>Each letter in the form its neighbours call for, and lam-alef as one glyph.</summary>
        static List<Glyph> Shape(List<Glyph> line)
        {
            var source = new List<Glyph>(line.Count);
            foreach (var glyph in line)
                if (!IsMark(glyph.C)) source.Add(glyph);

            var shaped = new List<Glyph>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                var glyph = source[i];
                if (!Letters.TryGetValue(glyph.C, out var forms))
                {
                    shaped.Add(glyph);
                    continue;
                }

                char previous = i > 0 ? source[i - 1].C : '\0';
                char next = i + 1 < source.Count ? source[i + 1].C : '\0';
                bool joinsBefore = previous != '\0' && JoinsToNext(previous);

                if (glyph.C == Lam && LamAlef.TryGetValue(next, out var ligature))
                {
                    glyph.C = joinsBefore ? ligature.Final : ligature.Isolated;
                    shaped.Add(glyph);
                    i++;   // the alef is part of the ligature
                    continue;
                }

                bool joinsAfter = forms.Count == 4 && next != '\0' && JoinsToPrevious(next);
                glyph.C = forms.Count == 1 ? forms.Isolated
                        : joinsBefore && joinsAfter ? forms.Medial
                        : joinsBefore ? forms.Final
                        : joinsAfter ? forms.Initial
                        : forms.Isolated;
                shaped.Add(glyph);
            }
            return shaped;
        }

        enum Direction { Right, Left, Neutral }

        /// <summary>
        /// A small bidi pass for a right-to-left line: runs of Latin letters and digits keep their own order,
        /// everything else reads right to left, and neutrals between two left-to-right runs ("46 / 63") stay
        /// with them. Full UAX #9 is far more than game text needs.
        /// </summary>
        static List<Glyph> Reorder(List<Glyph> line)
        {
            int n = line.Count;
            var directions = new Direction[n];
            for (int i = 0; i < n; i++)
            {
                char c = line[i].C;
                if (IsArabic(c)) directions[i] = Direction.Right;
                else if (char.IsLetterOrDigit(c)) directions[i] = Direction.Left;
                else if ((c == '+' || c == '-' || c == '−') && i + 1 < n && char.IsDigit(line[i + 1].C)) directions[i] = Direction.Left;
                else directions[i] = Direction.Neutral;
            }

            for (int i = 0; i < n; i++)
            {
                if (directions[i] != Direction.Neutral) continue;
                int end = i;
                while (end < n && directions[end] == Direction.Neutral) end++;
                bool leftBefore = i > 0 && directions[i - 1] == Direction.Left;
                bool leftAfter = end < n && directions[end] == Direction.Left;
                // Between Latin letters ("Enter  Select") neutrals stay with the Latin. Between numbers they do only
                // as one separator inside a number ("46/63"); "Fight 2    46/63 HP" is two numbers, and joining them
                // into one left-to-right run printed the 2 on the wrong side.
                bool lettersBothSides = leftBefore && leftAfter && char.IsLetter(line[i - 1].C) && char.IsLetter(line[end].C);
                bool separatorInNumber = leftBefore && leftAfter && end - i == 1 && ".,/:×".IndexOf(line[i].C) >= 0;
                var resolved = lettersBothSides || separatorInNumber ? Direction.Left : Direction.Right;
                for (int k = i; k < end; k++) directions[k] = resolved;
                i = end - 1;
            }

            var runs = new List<(int Start, int End, Direction Direction)>();
            for (int i = 0; i < n;)
            {
                int end = i;
                while (end < n && directions[end] == directions[i]) end++;
                runs.Add((i, end, directions[i]));
                i = end;
            }

            var visual = new List<Glyph>(n);
            for (int r = runs.Count - 1; r >= 0; r--)
            {
                var (start, end, direction) = runs[r];
                if (direction == Direction.Left)
                {
                    for (int i = start; i < end; i++) visual.Add(line[i]);
                    continue;
                }
                for (int i = end - 1; i >= start; i--)
                {
                    var glyph = line[i];
                    if (Mirrored.TryGetValue(glyph.C, out var mirror)) glyph.C = mirror;
                    visual.Add(glyph);
                }
            }
            return visual;
        }

        /// <summary>Writes the characters back out, reopening tags around each stretch that shares them.</summary>
        static void Emit(List<Glyph> glyphs, StringBuilder output)
        {
            string open = "";
            foreach (var glyph in glyphs)
            {
                if (glyph.Tags != open)
                {
                    Close(open, output);
                    if (glyph.Tags.Length > 0)
                        foreach (var tag in glyph.Tags.Split('\u0001')) output.Append('<').Append(tag).Append('>');
                    open = glyph.Tags;
                }
                output.Append(glyph.C);
            }
            Close(open, output);
        }

        static void Close(string open, StringBuilder output)
        {
            if (open.Length == 0) return;
            var tags = open.Split('\u0001');
            for (int i = tags.Length - 1; i >= 0; i--) output.Append("</").Append(TagName(tags[i])).Append('>');
        }
    }
}
