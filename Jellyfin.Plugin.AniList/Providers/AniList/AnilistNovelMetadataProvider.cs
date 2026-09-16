using System.Collections.Generic;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AniList.Providers.AniList
{
    public class AnilistNovelMetadataProvider : AniListBaseMetadataProvider<Book, BookInfo>, IRemoteMetadataProvider<Book, BookInfo>
    {
        public string Name => "Anilist (Light Novel)";

        protected override MediaType Type => MediaType.Manga;
        protected override HashSet<MediaFormat> Formats { get; } = [MediaFormat.Novel];

        public AnilistNovelMetadataProvider(ILogger<AnilistNovelMetadataProvider> logger) : base(logger) { }
    }
}
