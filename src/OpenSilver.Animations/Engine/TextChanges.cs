/*===================================================================================
* 
*   Copyright (c) Userware (OpenSilver.net/XAML.io)
*      
*   This file is part of the OpenSilver.Animations library by Userware
*   (https://github.com/OpenSilver/OpenSilver.Animations),
*   which is licensed under the MIT license: https://opensource.org/licenses/MIT
*   
*   As stated in the MIT license, "the above copyright notice and this permission
*   notice shall be included in all copies or substantial portions of the Software."
*  
\*====================================================================================*/

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace OpenSilver.Animations.Engine
{
    /// <summary>
    /// Watches the text of the TextBlocks that have a TextChange, and works out how it changes.
    /// </summary>
    internal static class TextChanges
    {
        private static readonly ConditionalWeakTable<TextBlock, object> Watched = new ConditionalWeakTable<TextBlock, object>();

        // Bound to the TextBlock's own Text: it tells every change, from code, a binding or the
        // inlines, after the TextBlock has written its new text in the page.
        private static readonly DependencyProperty WatchedTextProperty =
            DependencyProperty.RegisterAttached(
                "WatchedText",
                typeof(string),
                typeof(TextChanges),
                new PropertyMetadata(null, OnWatchedTextChanged));

        public static void Watch(UIElement element)
        {
            if (element is not TextBlock textBlock || Watched.TryGetValue(textBlock, out _))
            {
                return;
            }

            Watched.Add(textBlock, null);
            BindingOperations.SetBinding(textBlock, WatchedTextProperty, new Binding { Path = new PropertyPath("Text"), Source = textBlock });
        }

        private static void OnWatchedTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // The first value is the text it had when it started being watched.
            if (e.OldValue is string oldText && d is TextBlock textBlock)
            {
                LayoutAnimator.OnTextChanged(textBlock, oldText);
            }
        }

        /// <summary>
        /// The number of letters of a text, counting an emoji or a letter with its accents as one.
        /// </summary>
        public static int CountLetters(string text) => string.IsNullOrEmpty(text) ? 0 : new StringInfo(text).LengthInTextElements;

        /// <summary>
        /// The number of letters at the beginning that two texts share.
        /// </summary>
        public static int CountSharedLetters(string a, string b)
        {
            string[] x = LettersOf(a), y = LettersOf(b);
            int count = 0;
            while (count < x.Length && count < y.Length && x[count] == y[count])
            {
                count++;
            }

            return count;
        }

        /// <summary>
        /// Pairs the letters of the old text with those of the new one, for Roll. When both texts
        /// are numbers with the same text around them, the numbers are aligned on their last digit,
        /// like on a counter; otherwise, the texts are aligned on their first letter. A column with
        /// one string stays as it is; a column with two rolls from the first to the second.
        /// </summary>
        public static List<string[]> GetRollColumns(string oldText, string newText, TextNumber oldNumber, TextNumber newNumber)
        {
            var columns = new List<string[]>();
            var same = new StringBuilder();
            if (AreComparable(oldNumber, newNumber))
            {
                AddColumns(columns, same, oldNumber.Prefix, newNumber.Prefix, alignRight: false);
                AddColumns(columns, same, oldNumber.Number, newNumber.Number, alignRight: true);
                AddColumns(columns, same, oldNumber.Suffix, newNumber.Suffix, alignRight: false);
            }
            else
            {
                AddColumns(columns, same, oldText, newText, alignRight: false);
            }

            if (same.Length > 0)
            {
                columns.Add(new[] { same.ToString() });
            }

            return columns;
        }

        /// <summary>
        /// Whether two texts are the same number format with different values, like "9 items" and
        /// "10 items".
        /// </summary>
        public static bool AreComparable(TextNumber a, TextNumber b)
            => a != null && b != null && a.Prefix == b.Prefix && a.Suffix == b.Suffix;

        private static void AddColumns(List<string[]> columns, StringBuilder same, string oldText, string newText, bool alignRight)
        {
            string[] a = LettersOf(oldText), b = LettersOf(newText);
            int count = Math.Max(a.Length, b.Length);
            for (int i = 0; i < count; i++)
            {
                int ia = alignRight ? i - (count - a.Length) : i;
                int ib = alignRight ? i - (count - b.Length) : i;
                string x = ia >= 0 && ia < a.Length ? a[ia] : string.Empty;
                string y = ib >= 0 && ib < b.Length ? b[ib] : string.Empty;
                if (x == y)
                {
                    same.Append(x);
                    continue;
                }

                if (same.Length > 0)
                {
                    columns.Add(new[] { same.ToString() });
                    same.Clear();
                }

                columns.Add(new[] { x, y });
            }
        }

        private static string[] LettersOf(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Array.Empty<string>();
            }

            var letters = new List<string>(text.Length);
            TextElementEnumerator e = StringInfo.GetTextElementEnumerator(text);
            while (e.MoveNext())
            {
                letters.Add(e.GetTextElement());
            }

            return letters.ToArray();
        }
    }

    /// <summary>
    /// The first number written in a text, with the text around it: "$1,250.50" is 1250.5, after
    /// "$", with 2 decimals, "," between the thousands and "." before the decimals.
    /// </summary>
    internal sealed class TextNumber
    {
        private static readonly Regex Pattern = new Regex(@"[-\u2212]?[0-9]+(?:[.,'\u00A0\u202F ][0-9]+)*", RegexOptions.CultureInvariant);

        private TextNumber() { }

        public string Prefix { get; private set; }

        /// <summary>
        /// The number as it is written, with its sign and separators.
        /// </summary>
        public string Number { get; private set; }

        public string Suffix { get; private set; }

        public double Value { get; private set; }

        public int Decimals { get; private set; }

        /// <summary>
        /// What separates the thousands, or an empty string.
        /// </summary>
        public string GroupSeparator { get; private set; }

        public string DecimalSeparator { get; private set; }

        public static bool TryParse(string text, out TextNumber number)
        {
            number = null;
            if (string.IsNullOrEmpty(text))
            {
                return false;
            }

            Match match = Pattern.Match(text);
            if (!match.Success)
            {
                return false;
            }

            string s = match.Value;
            bool negative = s[0] == '-' || s[0] == '\u2212';
            int start = negative ? 1 : 0;

            // The digits, and the separators between them.
            var runs = new List<string>();
            var separators = new List<char>();
            int runStart = start;
            for (int i = start; i < s.Length; i++)
            {
                if (s[i] < '0' || s[i] > '9')
                {
                    runs.Add(s.Substring(runStart, i - runStart));
                    separators.Add(s[i]);
                    runStart = i + 1;
                }
            }
            runs.Add(s.Substring(runStart));

            char decimalSeparator = '\0';
            char groupSeparator = '\0';
            if (separators.Count > 0)
            {
                char last = separators[separators.Count - 1];
                bool lastDiffers = separators.Count > 1 && separators.FindIndex(c => c == last) == separators.Count - 1;
                if (lastDiffers && (last == '.' || last == ','))
                {
                    decimalSeparator = last;
                    groupSeparator = separators[0];
                }
                else if (separators.Count == 1 && (last == '.' || last == ',') && IsDecimal(last, runs[0], runs[1]))
                {
                    decimalSeparator = last;
                }
                else
                {
                    groupSeparator = separators[0];
                }
            }

            // The thousands come in groups of three digits: what doesn't is not part of the number,
            // like the year in "Oct 28 2026".
            int groups = decimalSeparator != '\0' ? runs.Count - 1 : runs.Count;
            int used = 1;
            bool valid = runs[0].Length <= 3 || groupSeparator == '\0';
            for (int i = 1; i < groups && valid; i++)
            {
                if (separators[i - 1] != groupSeparator || runs[i].Length != 3)
                {
                    valid = false;
                    break;
                }
                used = i + 1;
            }

            if (!valid)
            {
                groupSeparator = '\0';
                decimalSeparator = '\0';
                used = 1;
            }
            else if (decimalSeparator != '\0')
            {
                used = runs.Count;
            }

            var integer = new StringBuilder();
            for (int i = 0; i < (decimalSeparator != '\0' ? used - 1 : used); i++)
            {
                integer.Append(runs[i]);
            }

            string decimals = decimalSeparator != '\0' ? runs[used - 1] : string.Empty;
            int length = start;
            for (int i = 0; i < used; i++)
            {
                length += runs[i].Length + (i > 0 ? 1 : 0);
            }

            string digits = decimals.Length > 0 ? integer + "." + decimals : integer.ToString();
            if (!double.TryParse(digits, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out double value))
            {
                return false;
            }

            number = new TextNumber
            {
                Prefix = text.Substring(0, match.Index),
                Number = s.Substring(0, length),
                Suffix = text.Substring(match.Index + length),
                Value = negative ? -value : value,
                Decimals = decimals.Length,
                GroupSeparator = groupSeparator == '\0' ? string.Empty : groupSeparator.ToString(),
                DecimalSeparator = decimalSeparator != '\0' ? decimalSeparator.ToString() : CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator,
            };
            return true;
        }

        /// <summary>
        /// Whether the only separator of a number is before its decimals: "4.2" and "0,500" are,
        /// "1,000" is when the culture writes the decimals after a comma.
        /// </summary>
        private static bool IsDecimal(char separator, string before, string after)
        {
            if (after.Length != 3 || before == "0" || before.Length > 3)
            {
                return true;
            }

            return CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator == separator.ToString();
        }
    }
}
