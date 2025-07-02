using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.AniList.Configuration
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

    public enum LanguageFilterType {
        Localized,
        Japanese,
        All
    }

    public enum StudioFilterType {
        MainOnly,
        AnimationStudioOnly,
        All
    }

    public class PluginConfiguration : BasePluginConfiguration
    {
        public PluginConfiguration()
        {
            TitlePreference = TitlePreferenceType.Localized;
            OriginalTitlePreference = TitlePreferenceType.JapaneseRomaji;
            PersonLanguageFilterPreference = LanguageFilterType.All;
            MaxPeople = 0;
            MaxGenres = 5;
            AnimeDefaultGenre = AnimeDefaultGenreType.Anime;
            StudioFilterPreference = StudioFilterType.All;
            ApiRateLimit = 30;
            AniDbReplaceGraves = true;
            AniListShowSpoilerTags = true;
            UseAnitomyLibrary = false;
        }

        public TitlePreferenceType TitlePreference { get; set; }

        public TitlePreferenceType OriginalTitlePreference { get; set; }

        public LanguageFilterType PersonLanguageFilterPreference { get; set; }

        public int MaxPeople { get; set; }

        public int MaxGenres { get; set; }

        public StudioFilterType StudioFilterPreference { get; set; }

        public AnimeDefaultGenreType AnimeDefaultGenre { get; set; }

        public int ApiRateLimit { get; set; }

        public bool AniDbReplaceGraves { get; set; }

        public bool AniListShowSpoilerTags { get; set; }

        public bool UseAnitomyLibrary { get; set; }
    }
}
