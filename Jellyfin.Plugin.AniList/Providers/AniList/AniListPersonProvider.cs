using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;


namespace Jellyfin.Plugin.AniList.Providers.AniList
{
    public class AniListPersonProvider(AniListApi aniListApi, IHttpClientFactory httpClientFactory) : IRemoteMetadataProvider<Person, PersonLookupInfo>, IHasOrder
    {
        public int Order => -2;
        public string Name => "AniList";

        public async Task<MetadataResult<Person>> GetMetadata(PersonLookupInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Person>();

            if (!info.TryGetProviderId(ProviderNames.AniList, out string stringId)
                || !int.TryParse(stringId, out int anilistId))
            {
                return result;
            }

            Staff staff = await aniListApi.GetStaff(anilistId, cancellationToken).ConfigureAwait(false);

            if (staff is null)
            {
                return result;
            }

            result.Item = staff.ToPerson();
            result.HasMetadata = true;

            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(PersonLookupInfo searchInfo, CancellationToken cancellationToken)
        {
            return (await aniListApi.SearchStaff(searchInfo.Name, cancellationToken).ConfigureAwait(false))
                .Select(s => s.ToSearchResult());
        }

        public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            var httpClient = httpClientFactory.CreateClient();
            return await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        }
    }
}
