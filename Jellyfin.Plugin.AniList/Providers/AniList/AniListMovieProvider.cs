using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Serialization;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.AniList.Configuration;


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
            _aniListApi = new AniListApi();
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
                var searchName = info.Name;
                MediaSearchResult msr;
                if(config.UseAnitomyLibrary)
                {//Use Anitomy to extract the title
                    searchName = Anitomy.AnitomyHelper.ExtractAnimeTitle(searchName);
                    searchName = AnilistSearchHelper.PreprocessTitle(searchName);
                    _log.LogInformation("Start AniList... Searching({Name})", searchName);
                    msr = await _aniListApi.Search_GetSeries(searchName, cancellationToken).ConfigureAwait(false);
                    if (msr != null)
                    {
                        media = await _aniListApi.GetAnime(msr.id.ToString(), cancellationToken).ConfigureAwait(false);
                    }
                }
                if(!config.UseAnitomyLibrary || media == null)
                {
                    searchName = info.Name;
                    searchName = AnilistSearchHelper.PreprocessTitle(searchName);
                    _log.LogInformation("Start AniList... Searching({Name})", searchName);
                    msr = await _aniListApi.Search_GetSeries(searchName, cancellationToken).ConfigureAwait(false);
                    if (msr != null)
                    {
                        media = await _aniListApi.GetAnime(msr.id.ToString(), cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            if (media != null)
            {
                result.HasMetadata = true;
                result.Item = media.ToMovie();
                result.People = media.GetPeopleInfo();
                result.Provider = ProviderNames.AniList;
            }

            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(MovieInfo searchInfo, CancellationToken cancellationToken)
        {
            var results = new List<RemoteSearchResult>();

            var aid = searchInfo.ProviderIds.GetOrDefault(ProviderNames.AniList);
            if (!string.IsNullOrEmpty(aid))
            {
                Media aid_result = await _aniListApi.GetAnime(aid, cancellationToken).ConfigureAwait(false);
                if (aid_result != null)
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
            using var httpClient = Plugin.Instance.GetHttpClient();
            return await httpClient.GetAsync(url).ConfigureAwait(false);
        }
    }
}
