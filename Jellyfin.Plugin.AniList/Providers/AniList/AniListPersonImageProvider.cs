using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.AniList.Providers.AniList
{
    public class AniListPersonImageProvider(AniListApi aniListApi, IHttpClientFactory httpClientFactory) : IRemoteImageProvider
    {
        private readonly ImageType[] supportedTypes = [ImageType.Primary];

        public string Name => "AniList";

        public bool Supports(BaseItem item) => item is Person;

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item) => supportedTypes;

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var results = new List<RemoteImageInfo>();

            if (!item.TryGetProviderId(ProviderNames.AniList, out string stringId)
                || !int.TryParse(stringId, out int id))
            {
                return results;
            }

            Staff staff = await aniListApi.GetStaff(id, cancellationToken).ConfigureAwait(false);
            if (staff is null)
            {
                return results;
            }

            string imageUrl = staff.image.GetBestImage();
            if (string.IsNullOrEmpty(imageUrl))
            {
                return results;
            }

            results.Add(new RemoteImageInfo {
                ProviderName = Name,
                Type = ImageType.Primary,
                Url = imageUrl,
            });

            return results;
        }

        public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            var httpClient = httpClientFactory.CreateClient();
            return await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
        }
    }
}
