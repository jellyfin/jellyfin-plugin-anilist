using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Anime.Providers.AniList
{
    public class AniListExternalId : IExternalId
    {
        public bool Supports(IHasProviderIds item)
            => item is Series || item is Movie;

        public string ProviderName
            => "AniList";

        public string Key
            => ProviderNames.AniList;

        public ExternalIdMediaType? Type
            => ExternalIdMediaType.Series;

        public string UrlFormatString
            => "https://anilist.co/anime/{0}/";
    }
}
