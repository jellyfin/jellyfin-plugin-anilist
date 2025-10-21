using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.AniList.Providers.AniList
{
    public class AniListEpisodeProvider : IRemoteMetadataProvider<Episode, EpisodeInfo>
    {
        public string Name => "AniList";

        public async Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            var provider = new AniListAnimeImageProvider();
            return await provider.GetImageResponse(url, cancellationToken);
        }

        public Task<MetadataResult<Episode>> GetMetadata(EpisodeInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Episode>();
            result.HasMetadata = true;
            result.Item = new Episode
            {
                IndexNumber = info.IndexNumber ?? 1,
                ParentIndexNumber = info.ParentIndexNumber ?? 1,
            };
            return Task.FromResult(result);
        }

        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(EpisodeInfo searchInfo, CancellationToken cancellationToken)
        {
            return Task.FromResult(Enumerable.Empty<RemoteSearchResult>());
        }
    }
}
