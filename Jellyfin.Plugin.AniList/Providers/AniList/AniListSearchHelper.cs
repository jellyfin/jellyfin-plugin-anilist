using System.Text.RegularExpressions;


namespace Jellyfin.Plugin.AniList.Providers.AniList
{
    public partial class AnilistSearchHelper
    {

        [GeneratedRegex(@"(\s|\.)S[0-9]{1,2}")]
        private static partial Regex SeasonRegex();

        [GeneratedRegex(@"\s*~(\w|[0-9]|\s)+~")]
        private static partial Regex AltNameRegex();

        [GeneratedRegex(@"\((\w|[0-9]|\s)+\)$")]
        private static partial Regex NativeNameRegex();

        [GeneratedRegex(@"\s?&\s?")]
        private static partial Regex AndReplacementRegex();

        [GeneratedRegex(@"\([0-9]{4}\)\s*\[(\w|[0-9]|-)+\]$")]
        private static partial Regex FolderTruncationRegex();

        [GeneratedRegex(@"\#")]
        private static partial Regex SpaceReplacementRegex();

        public static string PreprocessTitle(string path)
        { //Remove items that will always cause anilist to fail
            string input = path;

            //Season designation
            input = SeasonRegex().Replace(input, string.Empty);

            // ~ ALT NAME ~
            input = AltNameRegex().Replace(input, string.Empty);

            // Native Name (English Name)
            // Only replaces if the name ends with a parenthesis to hopefully avoid mangling titles with parens (e.g. Evangelion 1.11 You Are (Not) Alone)
            input = NativeNameRegex().Replace(input.Trim(), string.Empty);

            // Replace & with "and" to avoid lookup failures
            input = AndReplacementRegex().Replace(input, " and ");

            // Replace the following characters with a space, to avoid failed lookups
            input = SpaceReplacementRegex().Replace(input, " ");

            // Truncate suggested Jellyfin folder format for the anilist search. Example: The Melancholy of Haruhi Suzumiya (2006) [tvdbid-79414]
            input = FolderTruncationRegex().Replace(input.Trim(), string.Empty);

            return input;
        }
    }
}
