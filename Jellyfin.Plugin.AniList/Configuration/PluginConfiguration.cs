using Jellyfin.Plugin.AniList.Providers.AniList;
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

    public enum PersonRoleFilter
    {
        Main = 0,
        Supporting = 1,
        Background = 2,
    }

    public class PluginConfiguration : BasePluginConfiguration
    {
        public PluginConfiguration()
        {
            TitlePreference = TitlePreferenceType.Localized;
            OriginalTitlePreference = TitlePreferenceType.JapaneseRomaji;
            PersonLanguageFilterPreference = LanguageFilterType.All;
            PersonRoleFilterPreference = PersonRoleFilter.Background;
            AddOverview = true;
            MaxPeople = 0;
            MaxGenres = 5;
            MaxTags = 0;
            MinTagRank = 0;
            AnimeDefaultGenre = AnimeDefaultGenreType.Anime;
            StudioFilterPreference = StudioFilterType.All;
            AniDbRateLimit = AniListRateLimiter.DefaultRequestsPerMinute;
            AniDbReplaceGraves = true;
            AniListShowSpoilerTags = true;
            UseAnitomyLibrary = false;
        }

        public TitlePreferenceType TitlePreference { get; set; }

        public TitlePreferenceType OriginalTitlePreference { get; set; }

        public LanguageFilterType PersonLanguageFilterPreference { get; set; }

        public PersonRoleFilter PersonRoleFilterPreference { get; set; }

        public bool AddOverview { get; set; }

        public int MaxPeople { get; set; }

        public int MaxGenres { get; set; }

        public int MaxTags { get; set; }

        public int MinTagRank { get; set; }

        public StudioFilterType StudioFilterPreference { get; set; }

        public AnimeDefaultGenreType AnimeDefaultGenre { get; set; }

        /// <summary>
        /// How many requests per minute may be sent to the AniList API. AniList applies its own
        /// allowance on top of this and the plugin never exceeds the lower of the two, so this
        /// only serves to be more conservative than AniList asks for. Zero or less leaves the
        /// pacing entirely to the limit AniList reports.
        /// </summary>
        public int AniDbRateLimit { get; set; }

        public bool AniDbReplaceGraves { get; set; }

        public bool AniListShowSpoilerTags { get; set; }

        public bool UseAnitomyLibrary { get; set; }
    }
}
