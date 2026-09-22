using System.IO;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.AniList.Configuration;
using System.Globalization;

//API v2
namespace Jellyfin.Plugin.AniList.Providers.AniList
{
    public class AniListMovieProvider : IRemoteMetadataProvider<Movie, MovieInfo>, IHasOrder
    {
        private readonly ILogger _log;
        private readonly AniListApi _aniListApi;
        public int Order => -2;
        public string Name => "AniList";

        public AniListMovieProvider(ILogger<AniListMovieProvider> logger)
        {
            _log = logger;
            _aniListApi = new AniListApi(logger);
        }

        public async Task<MetadataResult<Movie>> GetMetadata(MovieInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Movie>();
            Media media = null;
            PluginConfiguration config = Plugin.Instance.Configuration;

            var aid = info.ProviderIds.GetOrDefault(ProviderNames.AniList);
            if (!string.IsNullOrEmpty(aid))
            {
                media = await _aniListApi.GetAnime(aid, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var candidates = new List<(string Title, int? Year)>();

                int? animeYear = null;
                if (config.UseAnitomyLibrary)
                {
                    var yearStr = Anitomy.AnitomyHelper.ExtractAnimeYear(Path.GetFileName(info.Path));
                    if (int.TryParse(yearStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
                        animeYear = y;
                }

                if (config.UseOriginalTitle && !string.IsNullOrWhiteSpace(info.OriginalTitle))
                {
                    if (config.UseAnitomyLibrary)
                    {
                        candidates.Add((Anitomy.AnitomyHelper.ExtractAnimeTitle(info.OriginalTitle), animeYear));
                    }
                    candidates.Add((info.OriginalTitle, null));
                }
                if (config.UseAnitomyLibrary)
                {
                    candidates.Add((Anitomy.AnitomyHelper.ExtractAnimeTitle(info.Name), animeYear));
                }
                candidates.Add((info.Name, null));

                foreach (var (title, year) in candidates)
                {
                    media = await SearchAniListAsync(title, year, cancellationToken).ConfigureAwait(false);
                    if (media is not null)
                        break;
                }
            }

            if (media is not null)
            {
                result.HasMetadata = true;
                result.Item = media.ToMovie();
                result.People = media.GetPeopleInfo();
                result.Provider = ProviderNames.AniList;
            }

            return result;
        }


        private async Task<Media> SearchAniListAsync(string searchName, int? animeYear, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(searchName))
                return null;

            searchName = AnilistSearchHelper.PreprocessTitle(searchName);
            _log.LogInformation("Start AniList... Searching({Name})", searchName);

            var msr = await _aniListApi.Search_GetSeries(searchName, animeYear, cancellationToken).ConfigureAwait(false);

            if (msr is null)
                return null;

            return await _aniListApi.GetAnime(
                msr.id.ToString(CultureInfo.InvariantCulture),
                cancellationToken).ConfigureAwait(false);
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
        {
            var results = new List<RemoteSearchResult>();

            var aid = searchInfo.ProviderIds.GetOrDefault(ProviderNames.AniList);
            if (!string.IsNullOrEmpty(aid))
            {
                Media aid_result = await _aniListApi.GetAnime(aid, cancellationToken).ConfigureAwait(false);
                if (aid_result is not null)
                {
                    results.Add(aid_result.ToSearchResult());
                }
            }

            if (!string.IsNullOrEmpty(searchInfo.Name))
            {
                List<MediaSearchResult> name_results = await _aniListApi.Search_GetSeries_list(searchInfo.Name, cancellationToken).ConfigureAwait(false);
                foreach (var media in name_results)
                {
                    results.Add(media.ToSearchResult());
                }
            }

            return results;
        }

        public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            var httpClient = Plugin.Instance.GetHttpClient();
            return await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        }
    }
}
