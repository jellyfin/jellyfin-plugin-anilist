using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AniList.Providers.AniList
{
    public class AnilistMovieMetadataProvider : AniListBaseMetadataProvider<Movie, MovieInfo>, IRemoteMetadataProvider<Movie, MovieInfo>
    {
        public string Name => "Anilist";

        protected override MediaType Type => MediaType.Anime;

        public AnilistMovieMetadataProvider(ILogger<AnilistMovieMetadataProvider> logger) : base(logger) { }
    }
}
