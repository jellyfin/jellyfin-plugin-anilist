using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;


namespace Jellyfin.Plugin.AniList.Providers.AniList
{
    public class AnilistSearchHelper
    {
        public static String PreprocessTitle(String path)
        { //Remove items that will always cause anilist to fail
            String input = path;

            //Season designation
            input = Regex.Replace(
                input,
                @"(\s|\.)S[0-9]{1,2}", String.Empty);
            // ~ ALT NAME ~
            input = Regex.Replace(
                input,
                @"\s*~(\w|[0-9]|\s)+~", String.Empty);

            // Native Name (English Name)
            // Only replaces if the name ends with a parenthesis to hopefully avoid mangling titles with parens (e.g. Evangelion 1.11 You Are (Not) Alone)
            input = Regex.Replace(
                input.Trim(),
                @"\((\w|[0-9]|\s)+\)$", String.Empty);

            // Replace & with "and" to avoid lookup failures
            input = Regex.Replace(input, @"\s?&\s?", " and ");

            // Replace the following characters with a space, to avoid failed lookups
            input = Regex.Replace(input, @"\#", " ");

            // Truncate suggested Jellyfin folder format for the anilist search. Example: The Melancholy of Haruhi Suzumiya (2006) [tvdbid-79414]
            input = Regex.Replace(input.Trim(), @"\(\d{4}\)\s*\[(\w|\d|-)+\]$", String.Empty);


            return input;
        }
    }
}
