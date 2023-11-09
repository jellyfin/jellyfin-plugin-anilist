using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using AnitomySharp;

namespace Jellyfin.Plugin.AniList.Anitomy
{
    public class AnitomyHelper
    {
        public static String ExtractAnimeTitle(string path)
        {
            String input = path;
            var elements = AnitomySharp.AnitomySharp.Parse(input);
            return elements.FirstOrDefault(p => p.Category == Element.ElementCategory.ElementAnimeTitle).Value;
        }
        public static String ExtractEpisodeTitle(string path)
        {
            var elements = AnitomySharp.AnitomySharp.Parse(path);
            return elements.FirstOrDefault(p => p.Category == Element.ElementCategory.ElementEpisodeTitle).Value;
        }
        public static String ExtractEpisodeNumber(string path)
        {
            var elements = AnitomySharp.AnitomySharp.Parse(path);
            return elements.FirstOrDefault(p => p.Category == Element.ElementCategory.ElementEpisodeNumber).Value;
        }
        public static String ExtractSeasonNumber(string path)
        {
            var elements = AnitomySharp.AnitomySharp.Parse(path);
            return elements.FirstOrDefault(p => p.Category == Element.ElementCategory.ElementAnimeSeason).Value;
        }
    }
}