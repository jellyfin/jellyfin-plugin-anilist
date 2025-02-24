#pragma warning disable CA1851 // Possible multiple enumerations of 'IEnumerable' collection

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AniList.Providers
{
    internal partial class Equals_check
    {
        [GeneratedRegex(@"(?s)(S[0-9]+)")]
        private static partial Regex CleanSeasonRegex();

        [GeneratedRegex(@"(?s)S([0-9]+)")]
        private static partial Regex CleanSeasonRegex2();

        [GeneratedRegex(@"((.*)s([0 - 9]))")]
        private static partial Regex CleanSeasonRegex3();

        [GeneratedRegex(@"(?s) \(.*?\)")]
        private static partial Regex CleanRegex();

        [GeneratedRegex(@"(?s)\(.*?\)")]
        private static partial Regex CleanRegex2();

        public readonly ILogger<Equals_check> _logger;

        public Equals_check(ILogger<Equals_check> logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// Clear name
        /// </summary>
        /// <param name="a"></param>
        /// <returns></returns>
        public static async Task<string> Clear_name(string a, CancellationToken cancellationToken)
        {
            try
            {
                a = a.Trim().Replace(await One_line_regex(CleanRegex(), a.Trim(), cancellationToken, 0), "", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            { }
            a = a.Replace(".", " ", StringComparison.OrdinalIgnoreCase);
            a = a.Replace("-", " ", StringComparison.OrdinalIgnoreCase);
            a = a.Replace("`", "", StringComparison.OrdinalIgnoreCase);
            a = a.Replace("'", "", StringComparison.OrdinalIgnoreCase);
            a = a.Replace("&", "and", StringComparison.OrdinalIgnoreCase);
            a = a.Replace("(", "", StringComparison.OrdinalIgnoreCase);
            a = a.Replace(")", "", StringComparison.OrdinalIgnoreCase);
            try
            {
                a = a.Replace(await One_line_regex(CleanSeasonRegex(), a.Trim(), cancellationToken), await One_line_regex(CleanSeasonRegex2(), a.Trim(), cancellationToken), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
            }
            return a;
        }

        /// <summary>
        /// Clear name heavy.
        /// Example: Text & Text to Text and Text
        /// </summary>
        /// <param name="a"></param>
        /// <returns></returns>
        public static async Task<string> Clear_name_step2(string a, CancellationToken cancellationToken)
        {
            if (a.Contains("Gekijyouban", StringComparison.OrdinalIgnoreCase))
            {
                a = (a.Replace("Gekijyouban", "", StringComparison.OrdinalIgnoreCase) + " Movie").Trim();
            }

            if (a.Contains("gekijyouban", StringComparison.OrdinalIgnoreCase))
            {
                a = (a.Replace("gekijyouban", "", StringComparison.OrdinalIgnoreCase) + " Movie").Trim();
            }

            try
            {
                a = a.Trim().Replace(await One_line_regex(CleanRegex(), a.Trim(), cancellationToken, 0), "", StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            { }
            a = a.Replace(".", " ", StringComparison.OrdinalIgnoreCase);
            a = a.Replace("-", " ", StringComparison.OrdinalIgnoreCase);
            a = a.Replace("`", "", StringComparison.OrdinalIgnoreCase);
            a = a.Replace("'", "", StringComparison.OrdinalIgnoreCase);
            a = a.Replace("&", "and", StringComparison.OrdinalIgnoreCase);
            a = a.Replace(":", "", StringComparison.OrdinalIgnoreCase);
            a = a.Replace("␣", "", StringComparison.OrdinalIgnoreCase);
            a = a.Replace("2wei", "zwei", StringComparison.OrdinalIgnoreCase);
            a = a.Replace("3rei", "drei", StringComparison.OrdinalIgnoreCase);
            a = a.Replace("4ier", "vier", StringComparison.OrdinalIgnoreCase);
            return a;
        }

        /// <summary>
        /// If a and b match it return true
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public static async Task<bool> Compare_strings(string a, string b, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b))
            {
                if (await Simple_compare(a, b, cancellationToken))
                {
                    return true;
                }

                return false;
            }

            return false;
        }

        /// <summary>
        /// Cut p(%) away from the string
        /// </summary>
        /// <param name="string_"></param>
        /// <param name="min_lenght"></param>
        /// <param name="p"></param>
        /// <returns></returns>
        public static async Task<string> Half_string(string string_, CancellationToken cancellationToken, int min_lenght = 0, int p = 50)
        {
            decimal length = 0;
            if (await Task.Run(() => ((int)((decimal)string_.Length - (((decimal)string_.Length / 100m) * (decimal)p)) > min_lenght), cancellationToken))
            {
                length = (decimal)string_.Length - (((decimal)string_.Length / 100m) * (decimal)p);
            }
            else
            {
                if (string_.Length < min_lenght)
                {
                    length = string_.Length;
                }
                else
                {
                    length = min_lenght;
                }
            }

            return string_.Substring(0, (int)length);
        }

        /// <summary>
        /// simple regex
        /// </summary>
        /// <param name="regex"></param>
        /// <param name="match"></param>
        /// <param name="group"></param>
        /// <param name="match_int"></param>
        /// <returns></returns>
        public static async Task<string> One_line_regex(Regex regex, string match, CancellationToken cancellationToken, int group = 1, int match_int = 0)
        {
            Regex _regex = regex;
            int x = 0;
            foreach (Match _match in regex.Matches(match))
            {
                if (x == match_int)
                {
                    return await Task.Run(() => _match.Groups[group].Value.ToString(), cancellationToken);
                }

                x++;
            }

            return "";
        }

        /// <summary>
        /// Compare 2 Strings, and it just works
        /// SeriesA S2 == SeriesA Second Season | True;
        /// </summary>
        private static async Task<bool> Simple_compare(string a, string b, CancellationToken cancellationToken, bool fastmode = false)
        {
            if (fastmode)
            {
                if (a[0] != b[0])
                {
                    return false;
                }
            }

            if (await Core_compare(a, b, cancellationToken))
            {
                return true;
            }

            if (await Core_compare(b, a, cancellationToken))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Compare 2 Strings, and it just works
        /// </summary>
        private static async Task<bool> Core_compare(string a, string b, CancellationToken cancellationToken)
        {
            if (a == b)
            {
                return true;
            }

            a = a.ToLower().Replace(" ", "", StringComparison.OrdinalIgnoreCase).Trim().Replace(".", "", StringComparison.OrdinalIgnoreCase);
            b = b.ToLower().Replace(" ", "", StringComparison.OrdinalIgnoreCase).Trim().Replace(".", "", StringComparison.OrdinalIgnoreCase);

            if (await Clear_name(a, cancellationToken) == await Clear_name(b, cancellationToken))
            {
                return true;
            }

            if (await Clear_name_step2(a, cancellationToken) == await Clear_name_step2(b, cancellationToken))
            {
                return true;
            }

            if (a.Replace("-", " ", StringComparison.OrdinalIgnoreCase) == b.Replace("-", " ", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (a.Replace(" 2", ":secondseason", StringComparison.OrdinalIgnoreCase) == b.Replace(" 2", ":secondseason", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (a.Replace("2", "secondseason", StringComparison.OrdinalIgnoreCase) == b.Replace("2", "secondseason", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (await Convert_symbols_too_numbers(a, "I", cancellationToken) == await Convert_symbols_too_numbers(b, "I", cancellationToken))
            {
                return true;
            }

            if (await Convert_symbols_too_numbers(a, "!", cancellationToken) == await Convert_symbols_too_numbers(b, "!", cancellationToken))
            {
                return true;
            }

            if (a.Replace("ndseason", "", StringComparison.OrdinalIgnoreCase) == b.Replace("ndseason", "", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (a.Replace("ndseason", "", StringComparison.OrdinalIgnoreCase) == b)
            {
                return true;
            }

            if (await One_line_regex(CleanSeasonRegex3(), a, cancellationToken, 2) + await One_line_regex(CleanSeasonRegex3(), a, cancellationToken, 3) == await One_line_regex(CleanSeasonRegex3(), b, cancellationToken, 2) + await One_line_regex(CleanSeasonRegex3(), b, cancellationToken, 3))
            {
                if (!string.IsNullOrEmpty(await One_line_regex(CleanSeasonRegex3(), a, cancellationToken, 2) + await One_line_regex(CleanSeasonRegex3(), a, cancellationToken, 3)))
                {
                    return true;
                }
            }

            if (await One_line_regex(CleanSeasonRegex3(), a, cancellationToken, 2) + await One_line_regex(CleanSeasonRegex3(), a, cancellationToken, 3) == b)
            {
                if (!string.IsNullOrEmpty(await One_line_regex(CleanSeasonRegex3(), a, cancellationToken, 2) + await One_line_regex(CleanSeasonRegex3(), a, cancellationToken, 3)))
                {
                    return true;
                }
            }

            if (a.Replace("rdseason", "", StringComparison.OrdinalIgnoreCase) == b.Replace("rdseason", "", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (a.Replace("rdseason", "", StringComparison.OrdinalIgnoreCase) == b)
            {
                return true;
            }

            try
            {
                if (a.Replace("2", "secondseason", StringComparison.OrdinalIgnoreCase).Replace(await One_line_regex(CleanRegex2(), a, cancellationToken, 0), "", StringComparison.OrdinalIgnoreCase) == b.Replace("2", "secondseason", StringComparison.OrdinalIgnoreCase).Replace(await One_line_regex(CleanRegex2(), b, cancellationToken, 0), "", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }
            try
            {
                if (a.Replace("2", "secondseason", StringComparison.OrdinalIgnoreCase).Replace(await One_line_regex(CleanRegex2(), a, cancellationToken, 0), "", StringComparison.OrdinalIgnoreCase) == b)
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }
            try
            {
                if (a.Replace(" 2", ":secondseason", StringComparison.OrdinalIgnoreCase).Replace(await One_line_regex(CleanRegex2(), a, cancellationToken, 0), "", StringComparison.OrdinalIgnoreCase) == b.Replace(" 2", ":secondseason", StringComparison.OrdinalIgnoreCase).Replace(await One_line_regex(CleanRegex2(), b, cancellationToken, 0), "", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }
            try
            {
                if (a.Replace(" 2", ":secondseason", StringComparison.OrdinalIgnoreCase).Replace(await One_line_regex(CleanRegex2(), a, cancellationToken, 0), "", StringComparison.OrdinalIgnoreCase) == b)
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }
            try
            {
                if (a.Replace(await One_line_regex(CleanRegex2(), a, cancellationToken, 0), "", StringComparison.OrdinalIgnoreCase) == b.Replace(await One_line_regex(CleanRegex2(), b, cancellationToken, 0), "", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }
            try
            {
                if (a.Replace(await One_line_regex(CleanRegex2(), a, cancellationToken, 0), "", StringComparison.OrdinalIgnoreCase) == b)
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }
            try
            {
                if (b.Replace(await One_line_regex(CleanRegex2(), b, cancellationToken, 0), "", StringComparison.OrdinalIgnoreCase).Replace("  2", ": second Season", StringComparison.OrdinalIgnoreCase) == a)
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }
            try
            {
                if (a.Replace(" 2ndseason", ":secondseason", StringComparison.OrdinalIgnoreCase) + " vs " + b == a)
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }
            try
            {
                if (a.Replace(await One_line_regex(CleanRegex2(), a, cancellationToken, 0), "", StringComparison.OrdinalIgnoreCase).Replace("  2", ":secondseason", StringComparison.OrdinalIgnoreCase) == b)
                {
                    return true;
                }
            }
            catch (Exception)
            {
            }
            return false;
        }

        /// <summary>
        /// Example: Convert II to 2
        /// </summary>
        /// <param name="input"></param>
        /// <param name="symbol"></param>
        /// <returns></returns>
        private static async Task<string> Convert_symbols_too_numbers(string input, string symbol, CancellationToken cancellationToken)
        {
            try
            {
                string regex_c = "_";
                int x = 0;
                int highest_number = 0;
                while (!string.IsNullOrEmpty(regex_c))
                {
                    regex_c = (await One_line_regex(new Regex(@"(" + symbol + @"+)"), input.ToLower().Trim(), cancellationToken, 1, x)).Trim();
                    if (highest_number < regex_c.Count())
                        highest_number = regex_c.Count();
                    x++;
                }

                x = 0;
                string output = "";
                while (x != highest_number)
                {
                    output = output + symbol;
                    x++;
                }

                output = input.Replace(output, highest_number.ToString(CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
                if (string.IsNullOrEmpty(output))
                {
                    output = input;
                }

                return output;
            }
            catch (Exception)
            {
                return input;
            }
        }
    }
}
