using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.AniList.Configuration;
using System.Globalization;
using System;
using MediaBrowser.Model.Entities;
using System.Linq;

//API v2
namespace Jellyfin.Plugin.AniList.Providers.AniList
{
    public abstract class AniListBaseMetadataProvider<TItem, TLookupInfo> : IHasOrder
        where TItem : BaseItem, new()
        where TLookupInfo : ItemLookupInfo
    {
        public int Order => -2;
        protected readonly ILogger _log;
        private readonly AniListApi _aniListApi;

        protected AniListBaseMetadataProvider(ILogger logger)
        {
            _log = logger;
            _aniListApi = new AniListApi(logger);
        }

        public async Task<MetadataResult<TItem>> GetMetadata(TLookupInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<TItem>();
            Media media = null;
            PluginConfiguration config = Plugin.Instance.Configuration;

            var aid = info.ProviderIds.GetOrDefault(ProviderNames.AniList);
            if (!string.IsNullOrEmpty(aid))
            {
                media = await _aniListApi.GetMedia(aid, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var searchName = info.Name;
                MediaSearchResult msr = null;
                if (config.UseAnitomyLibrary)
                {
                    // Use Anitomy to extract the title
                    searchName = Anitomy.AnitomyHelper.ExtractAnimeTitle(searchName);
                    searchName = AnilistSearchHelper.PreprocessTitle(searchName);
                    _log.LogInformation("Start AniList... Searching({Name})", searchName);
                    msr = (await SearchMedia(searchName, cancellationToken)).FirstOrDefault();
                }

                if (!config.UseAnitomyLibrary || msr is null)
                {
                    searchName = info.Name;
                    searchName = AnilistSearchHelper.PreprocessTitle(searchName);
                    _log.LogInformation("Start AniList... Searching({Name})", searchName);
                    msr = (await SearchMedia(searchName, cancellationToken)).FirstOrDefault();
                }

                if (msr is not null)
                {
                    media = await _aniListApi.GetMedia(msr.id.ToString(CultureInfo.InvariantCulture), cancellationToken).ConfigureAwait(false);
                }
            }

            if (media is not null)
            {
                result.HasMetadata = true;
                result.Item = ToJellyfinItem(media);
                result.People = media.GetPeopleInfo();
                result.Provider = ProviderNames.AniList;
            }

            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(TLookupInfo searchInfo, CancellationToken cancellationToken)
        {
            var results = new List<RemoteSearchResult>();

            var aid = searchInfo.ProviderIds.GetOrDefault(ProviderNames.AniList);
            if (!string.IsNullOrEmpty(aid))
            {
                Media aid_result = await _aniListApi.GetMedia(aid, cancellationToken).ConfigureAwait(false);
                if (aid_result is not null)
                {
                    results.Add(aid_result.ToSearchResult());
                }
            }

            if (!string.IsNullOrEmpty(searchInfo.Name))
            {
                results.AddRange(
                    (await SearchMedia(searchInfo.Name, cancellationToken))
                    .Select(m => m.ToSearchResult())
                );
            }

            return results;
        }

        private async Task<IEnumerable<MediaSearchResult>> SearchMedia(string query, CancellationToken cancellationToken)
        {
            IEnumerable<MediaSearchResult> results = await _aniListApi.SearchMedia(query, Type, cancellationToken).ConfigureAwait(false);

            if (Formats != null)
                results = results.Where(m => Formats.Contains(m.format));

            return results;
        }

        public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            var httpClient = Plugin.Instance.GetHttpClient();
            return await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        }

        protected abstract MediaType Type { get; }
        protected virtual HashSet<MediaFormat> Formats { get => null; }

        protected virtual TItem ToJellyfinItem(Media media)
        {
            // Populate common media item fields
            PluginConfiguration config = Plugin.Instance.Configuration;
            var result = new TItem
            {
                Name = media.GetPreferredTitle(config.TitlePreference, "en"),
                OriginalTitle = media.GetPreferredTitle(config.OriginalTitlePreference, "en"),
                Overview = config.AddOverview ? media.description : null,
                ProductionYear = media.startDate?.year,
                PremiereDate = media.startDate?.ToDateTime(),
                EndDate = media.endDate?.ToDateTime(),
                CommunityRating = media.GetRating(),
                RunTimeTicks = media.duration.HasValue ? TimeSpan.FromMinutes(media.duration.Value).Ticks : null,
                Genres = media.GetGenres().ToArray(),
                Tags = media.GetTagNames().ToArray(),
                Studios = media.GetStudioNames().ToArray(),
                ProviderIds = new Dictionary<string, string>() { { ProviderNames.AniList, media.id.ToString(CultureInfo.InvariantCulture) } }
            };

            return result;
        }
    }
}
