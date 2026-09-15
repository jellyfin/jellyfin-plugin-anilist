using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AniList.Providers.AniList
{
    public class AnilistSeriesMetadataProvider : AniListBaseMetadataProvider<Series, SeriesInfo>, IRemoteMetadataProvider<Series, SeriesInfo>
    {
        public string Name => "Anilist";

        protected override MediaType Type => MediaType.Anime;

        public AnilistSeriesMetadataProvider(ILogger<AnilistSeriesMetadataProvider> logger) : base(logger) { }

        protected override Series ToJellyfinItem(Media media)
        {
            Series result = base.ToJellyfinItem(media);

            result.Status = media.status switch
            {
                "FINISHED" or "CANCELLED" => SeriesStatus.Ended,
                "RELEASING" => SeriesStatus.Continuing,
                "NOT_YET_RELEASED" => SeriesStatus.Unreleased,
                _ => null,
            };

            return result;
        }
    }
}
