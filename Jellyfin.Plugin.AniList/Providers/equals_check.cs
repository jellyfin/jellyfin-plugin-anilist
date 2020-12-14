using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Jellyfin.Plugin.Anime.Providers.AniDB.Identity;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Anime.Providers
{
    internal class Equals_check
    {
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
        public async static Task<string> Clear_name(string a, CancellationToken cancellationToken)
        {
            try
            {
                a = a.Trim().Replace(await One_line_regex(new Regex(@"(?s) \(.*?\)"), a.Trim(), cancellationToken, 0), "");
            }
            catch (Exception)
            { }
            a = a.Replace(".", " ");
            a = a.Replace("-", " ");
            a = a.Replace("`", "");
            a = a.Replace("'", "");
            a = a.Replace("&", "and");
            a = a.Replace("(", "");
            a = a.Replace(")", "");
            try
            {
                a = a.Replace(await One_line_regex(new Regex(@"(?s)(S[0-9]+)"), a.Trim(), cancellationToken), await One_line_regex(new Regex(@"(?s)S([0-9]+)"), a.Trim(), cancellationToken));
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
        public async static Task<string> Clear_name_step2(string a, CancellationToken cancellationToken)
        {
            if(a.Contains("Gekijyouban"))
               a= (a.Replace("Gekijyouban", "") + " Movie").Trim();
            if (a.Contains("gekijyouban"))
               a = (a.Replace("gekijyouban", "") + " Movie").Trim();
            try
            {
                a = a.Trim().Replace(await One_line_regex(new Regex(@"(?s) \(.*?\)"), a.Trim(), cancellationToken, 0), "");
            }
            catch (Exception)
            { }
            a = a.Replace(".", " ");
            a = a.Replace("-", " ");
            a = a.Replace("`", "");
            a = a.Replace("'", "");
            a = a.Replace("&", "and");
            a = a.Replace(":", "");
            a = a.Replace("␣", "");
            a = a.Replace("2wei", "zwei");
            a = a.Replace("3rei", "drei");
            a = a.Replace("4ier", "vier");
            return a;
        }

        /// <summary>
        /// If a and b match it return true
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        public async static Task<bool> Compare_strings(string a, string b, CancellationToken cancellationToken)
        {
            if (!string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b))
            {
                if (await Simple_compare(a, b, cancellationToken))
                    return true;
                if (await Fast_xml_search(a, b, cancellationToken))
                    return true;

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
        public async static Task<string> Half_string(string string_, CancellationToken cancellationToken, int min_lenght = 0, int p = 50)
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
        public async static Task<string> One_line_regex(Regex regex, string match, CancellationToken cancellationToken, int group = 1, int match_int = 0)
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
        ///Return true if a and b match return false if not
        ///It loads the titles.xml on exceptions
        /// </summary>
        private async static Task<bool> Fast_xml_search(string a, string b, CancellationToken cancellationToken, bool return_AniDBid = false, bool retry = false)
        {
            //Get AID aid=\"([s\S].*)\">
            try
            {
                List<string> pre_aid = new List<string>();
                string xml = File.ReadAllText(Get_anidb_xml_file());
                int x = 0;
                string s1 = "-";
                string s2 = "-";
                while (!string.IsNullOrEmpty(s1) && !string.IsNullOrEmpty(s2))
                {
                    s1 = await One_line_regex(new Regex("<anime aid=" + "\"" + @"(\d+)" + "\"" + @">(?>[^<>]+|<(?!\/anime>)[^<>]*>)*?" + Regex.Escape(await Half_string(a, cancellationToken,4))), xml, cancellationToken,1, x);
                    if (s1 != "")
                    {
                        pre_aid.Add(s1);
                    }
                    s2 = await One_line_regex(new Regex("<anime aid=" + "\"" + @"(\d+)" + "\"" + @">(?>[^<>]+|<(?!\/anime>)[^<>]*>)*?" + Regex.Escape(await Half_string(b, cancellationToken,4))), xml, cancellationToken, 1, x);
                    if (s1 != "")
                    {
                        if (s1 != s2)
                        {
                            pre_aid.Add(s2);
                        }
                    }
                    x++;
                }
                foreach (string _aid in pre_aid)
                {
                    XElement doc = await Task.Run(async () => XElement.Parse("<?xml version=\"1.0\" encoding=\"UTF - 8\"?>" + "<animetitles>" + await One_line_regex(await Task.Run(() => new Regex("<anime aid=\"" + _aid + "\">" + @"(?s)(.*?)<\/anime>"), cancellationToken), xml, cancellationToken, 0) + "</animetitles>"), cancellationToken);
                    var a_ = from page in doc.Elements("anime")
                             where _aid == page.Attribute("aid").Value
                             select page;
                    if (await Simple_compare( a_.Elements("title"), b, cancellationToken) && await Simple_compare(a_.Elements("title"), a, cancellationToken))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch (Exception)
            {
                if (retry)
                {
                    return false;
                }
                else
                {
                    await Task.Run(() => AniDbTitleDownloader.Load_static(cancellationToken), cancellationToken);
                    return await Fast_xml_search(a, b, cancellationToken, false, true);
                }
            }
        }

        /// <summary>
        /// Return the AniDB ID if a and b match
        /// </summary>
        public async static Task<string> Fast_xml_search(string a, string b, CancellationToken cancellationToken, bool return_AniDBid, int x_ = 0)
        {
            //Get AID aid=\"([s\S].*)\">
            try
            {
                List<string> pre_aid = new List<string>();
                string xml = File.ReadAllText(Get_anidb_xml_file());
                int x = 0;
                string s1 = "-";
                string s2 = "-";
                while (!string.IsNullOrEmpty(s1) && !string.IsNullOrEmpty(s2))
                {
                    s1 = await One_line_regex(new Regex("<anime aid=" + "\"" + @"(\d+)" + "\"" + @">(?>[^<>]+|<(?!\/anime>)[^<>]*>)*?" + Regex.Escape(await Half_string(a, cancellationToken, 4))), xml, cancellationToken, 1, x);
                    if (s1 != "")
                    {
                        pre_aid.Add(s1);
                    }
                    s2 = await One_line_regex(new Regex("<anime aid=" + "\"" + @"(\d+)" + "\"" + @">(?>[^<>]+|<(?!\/anime>)[^<>]*>)*?" + Regex.Escape(await Half_string(b, cancellationToken, 4))), xml, cancellationToken, 1, x);
                    if (s1 != "")
                    {
                        if (s1 != s2)
                        {
                            pre_aid.Add(s2);
                        }
                    }
                    x++;
                }
                if (pre_aid.Count == 1)
                {
                    if (!string.IsNullOrEmpty(pre_aid[0]))
                    {
                        return pre_aid[0];
                    }
                }
                int biggestcount = 0;
                string cache_aid="";
                if (a == b)
                {
                    foreach (string _aid in pre_aid)
                    {
                       string result= await One_line_regex(new Regex(@"<anime aid=" + "\"" + _aid + "\"" + @"((?s).*?)<\/anime>"), xml, cancellationToken);
                       int count = (result.Length - result.Replace(a, "").Length)/a.Length;
                       if(biggestcount< count)
                       {
                            biggestcount = count;
                            cache_aid =_aid;
                       }
                    }
                    if (!string.IsNullOrEmpty(cache_aid))
                    {
                        return cache_aid;
                    }
                }
                foreach (string _aid in pre_aid)
                {
                    XElement doc = XElement.Parse("<?xml version=\"1.0\" encoding=\"UTF - 8\"?>" + "<animetitles>" +await One_line_regex(new Regex("<anime aid=\"" + _aid + "\">" + @"(?s)(.*?)<\/anime>"), xml, cancellationToken,0, 0) + "</animetitles>");
                    var a_ = from page in doc.Elements("anime")
                             where _aid == page.Attribute("aid").Value
                             select page;
                    
                    if (await Simple_compare(a_.Elements("title"), b, cancellationToken) && await Simple_compare(a_.Elements("title"), a, cancellationToken))
                    {
                        return _aid;
                    }
                }
                return "";
            }
            catch (Exception)
            {
                if (x_ == 1)
                {
                    return "";
                }
                else
                {
                    await Task.Run(() => AniDbTitleDownloader.Load_static(cancellationToken), cancellationToken);
                    return await Fast_xml_search(a, b, cancellationToken, true, 1);
                }
            }
        }

        /// <summary>
        /// get file Path from anidb xml file
        /// </summary>
        /// <returns></returns>
        private static string Get_anidb_xml_file()
        {
            return AniDbTitleDownloader.TitlesFilePath_;
        }

        /// <summary>
        /// Compare 2 Strings, and it just works
        /// SeriesA S2 == SeriesA Second Season | True;
        /// </summary>
        private async static Task<bool> Simple_compare(string a, string b, CancellationToken cancellationToken, bool fastmode = false)
        {
            if (fastmode)
            {
                if (a[0] == b[0])
                {
                }
                else
                {
                    return false;
                }
            }

            if (await Core_compare(a, b, cancellationToken))
                return true;
            if (await Core_compare(b, a, cancellationToken))
                return true;

            return false;
        }

        /// <summary>
        /// Compare 2 Strings, and it just works
        /// </summary>
        private async static Task<bool> Core_compare(string a, string b, CancellationToken cancellationToken)
        {
            if (a == b)
                return true;

            a = a.ToLower().Replace(" ", "").Trim().Replace(".", "");
            b = b.ToLower().Replace(" ", "").Trim().Replace(".", "");

            if (await Clear_name(a, cancellationToken) == await Clear_name(b, cancellationToken))
                return true;
            if (await Clear_name_step2(a, cancellationToken) == await Clear_name_step2(b, cancellationToken))
                return true;
            if (a.Replace("-", " ") == b.Replace("-", " "))
                return true;
            if (a.Replace(" 2", ":secondseason") == b.Replace(" 2", ":secondseason"))
                return true;
            if (a.Replace("2", "secondseason") == b.Replace("2", "secondseason"))
                return true;
            if (await Convert_symbols_too_numbers(a, "I", cancellationToken) == await Convert_symbols_too_numbers(b, "I", cancellationToken))
                return true;
            if (await Convert_symbols_too_numbers(a, "!", cancellationToken) == await Convert_symbols_too_numbers(b, "!", cancellationToken))
                return true;
            if (a.Replace("ndseason", "") == b.Replace("ndseason", ""))
                return true;
            if (a.Replace("ndseason", "") == b)
                return true;
            if (await One_line_regex(new Regex(@"((.*)s([0 - 9]))"), a, cancellationToken, 2) + await One_line_regex(new Regex(@"((.*)s([0 - 9]))"), a, cancellationToken, 3) == await One_line_regex(new Regex(@"((.*)s([0 - 9]))"), b, cancellationToken, 2) + await One_line_regex(new Regex(@"((.*)s([0 - 9]))"), b, cancellationToken, 3))
                if (!string.IsNullOrEmpty(await One_line_regex(new Regex(@"((.*)s([0 - 9]))"), a, cancellationToken, 2) + await One_line_regex(new Regex(@"((.*)s([0 - 9]))"), a, cancellationToken, 3)))
                    return true;
            if (await One_line_regex(new Regex(@"((.*)s([0 - 9]))"), a, cancellationToken, 2) + await One_line_regex(new Regex(@"((.*)s([0 - 9]))"), a, cancellationToken, 3) == b)
                if (!string.IsNullOrEmpty(await One_line_regex(new Regex(@"((.*)s([0 - 9]))"), a, cancellationToken, 2) + await One_line_regex(new Regex(@"((.*)s([0 - 9]))"), a, cancellationToken, 3)))
                    return true;
            if (a.Replace("rdseason", "") == b.Replace("rdseason", ""))
                return true;
            if (a.Replace("rdseason", "") == b)
                return true;
            try
            {
                if (a.Replace("2", "secondseason").Replace(await One_line_regex(new Regex(@"(?s)\(.*?\)"), a, cancellationToken, 0), "") == b.Replace("2", "secondseason").Replace(await One_line_regex(new Regex(@"(?s)\(.*?\)"), b, cancellationToken, 0), ""))
                    return true;
            }
            catch (Exception)
            {
            }
            try
            {
                if (a.Replace("2", "secondseason").Replace(await One_line_regex(new Regex(@"(?s)\(.*?\)"), a, cancellationToken, 0), "") == b)
                    return true;
            }
            catch (Exception)
            {
            }
            try
            {
                if (a.Replace(" 2", ":secondseason").Replace(await One_line_regex(new Regex(@"(?s)\(.*?\)"), a, cancellationToken, 0), "") == b.Replace(" 2", ":secondseason").Replace(await One_line_regex(new Regex(@"(?s)\(.*?\)"), b, cancellationToken, 0), ""))
                    return true;
            }
            catch (Exception)
            {
            }
            try
            {
                if (a.Replace(" 2", ":secondseason").Replace(await One_line_regex(new Regex(@"(?s)\(.*?\)"), a, cancellationToken, 0), "") == b)
                    return true;
            }
            catch (Exception)
            {
            }
            try
            {
                if (a.Replace(await One_line_regex(new Regex(@"(?s)\(.*?\)"), a, cancellationToken, 0), "") == b.Replace(await One_line_regex(new Regex(@"(?s)\(.*?\)"), b, cancellationToken, 0), ""))
                    return true;
            }
            catch (Exception)
            {
            }
            try
            {
                if (a.Replace(await One_line_regex(new Regex(@"(?s)\(.*?\)"), a, cancellationToken, 0), "") == b)
                    return true;
            }
            catch (Exception)
            {
            }
            try
            {
                if (b.Replace(await One_line_regex(new Regex(@"(?s)\(.*?\)"), b, cancellationToken, 0), "").Replace("  2", ": second Season") == a)
                    return true;
            }
            catch (Exception)
            {
            }
            try
            {
                if (a.Replace(" 2ndseason", ":secondseason") + " vs " + b == a)
                    return true;
            }
            catch (Exception)
            {
            }
            try
            {
                if (a.Replace(await One_line_regex(new Regex(@"(?s)\(.*?\)"), a, cancellationToken, 0), "").Replace("  2", ":secondseason") == b)
                    return true;
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
        private async static Task<string> Convert_symbols_too_numbers(string input, string symbol, CancellationToken cancellationToken)
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
                output = input.Replace(output, highest_number.ToString());
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

        /// <summary>
        /// Simple Compare a XElemtent with a string
        /// </summary>
        /// <param name="a_"></param>
        /// <param name="b"></param>
        /// <returns></returns>
        private async static Task<bool> Simple_compare(IEnumerable<XElement> a_, string b, CancellationToken cancellationToken)
        {
            bool ignore_date = true;
            string a_date = "";
            string b_date = "";

            string b_date_ = await One_line_regex(new Regex(@"([0-9][0-9][0-9][0-9])"), b, cancellationToken);
            if (!string.IsNullOrEmpty(b_date_))
            {
                b_date = b_date_;
            }
            if (!string.IsNullOrEmpty(b_date))
            {
                foreach (XElement a in a_)
                {
                    if (ignore_date)
                    {
                        string a_date_ = await One_line_regex(new Regex(@"([0-9][0-9][0-9][0-9])"), a.Value, cancellationToken);
                        if (!string.IsNullOrEmpty(a_date_))
                        {
                            a_date = a_date_;
                            ignore_date = false;
                        }
                    }
                }
            }
            if (!ignore_date)
            {
                if (a_date.Trim()==b_date.Trim())
                {
                    foreach (XElement a in a_)
                    {
                            if (await Simple_compare(a.Value, b, cancellationToken, true))
                                return true;
                    }
                }
                else
                {
                    return false;
                }
                return false;
            }
            else
            {
                foreach (XElement a in a_)
                {
                    if (ignore_date)
                    {
                        if (await Simple_compare(a.Value, b, cancellationToken, true))
                            return true;
                    }
                }
                return false;
            }
        }
    }
}
