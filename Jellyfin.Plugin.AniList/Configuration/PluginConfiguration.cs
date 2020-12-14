using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Anime.Configuration
{
    public enum TitlePreferenceType
    {
        /// <summary>
        /// Use titles in the local metadata language.
        /// </summary>
        Localized,

        /// <summary>
        /// Use titles in Japanese.
        /// </summary>
        Japanese,

        /// <summary>
        /// Use titles in Japanese romaji.
        /// </summary>
        JapaneseRomaji
    }

    public enum AnimeDefaultGenreType
    {
        None, Anime, Animation
    }

    public class PluginConfiguration : BasePluginConfiguration
    {
        public PluginConfiguration()
        {
            TitlePreference = TitlePreferenceType.Localized;
            MaxGenres = 5;
            TidyGenreList = true;
            TitleCaseGenres = false;
            AnimeDefaultGenre = AnimeDefaultGenreType.Anime;
            AniDbRateLimit = 2000;
            AniDbReplaceGraves = true;
        }

        public TitlePreferenceType TitlePreference { get; set; }

        public int MaxGenres { get; set; }

        public bool TidyGenreList { get; set; }

        public bool TitleCaseGenres { get; set; }

        public AnimeDefaultGenreType AnimeDefaultGenre { get; set; }

        public int AniDbRateLimit { get; set; }

        public bool AniDbReplaceGraves { get; set; }
    }
}
