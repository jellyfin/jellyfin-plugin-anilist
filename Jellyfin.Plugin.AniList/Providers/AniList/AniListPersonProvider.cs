using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Microsoft.Extensions.Logging;


//API v2
namespace Jellyfin.Plugin.AniList.Providers.AniList
{
    public class AniListPersonProvider : IRemoteMetadataProvider<Person, PersonLookupInfo>, IHasOrder
    {
        private readonly IApplicationPaths _paths;
        private readonly ILogger _log;
        private readonly AniListApi _aniListApi;
        public int Order => -2;
        public string Name => "AniList";

        public AniListPersonProvider(IApplicationPaths appPaths, ILogger<AniListPersonProvider> logger)
        {
            _log = logger;
            _aniListApi = new AniListApi();
            _paths = appPaths;
        }

        public async Task<MetadataResult<Person>> GetMetadata(PersonLookupInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Person>();

            int anilistId = int.Parse(info.GetProviderId(ProviderNames.AniList));
            Staff staff = await _aniListApi.GetStaff(anilistId, cancellationToken);

            if (staff == null) {
                return result;
            }

            result.Item = staff.ToPerson();
            result.HasMetadata = true;
            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(PersonLookupInfo searchInfo, CancellationToken cancellationToken)
        {
            return (await _aniListApi.SearchStaff(searchInfo.Name, cancellationToken))
                .Select(s => s.ToSearchResult());
        }

        public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            var httpClient = Plugin.Instance.GetHttpClient();
            return await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        }
    }
}
