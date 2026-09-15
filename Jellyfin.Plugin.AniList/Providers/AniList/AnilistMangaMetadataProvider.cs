using System.Collections.Generic;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.AniList.Providers.AniList
{
    public class AnilistMangaMetadataProvider : AniListBaseMetadataProvider<Book, BookInfo>, IRemoteMetadataProvider<Book, BookInfo>
    {
        public string Name => "Anilist (Manga)";

        protected override MediaType Type => MediaType.Manga;
        protected override HashSet<MediaFormat> Formats { get; } = [MediaFormat.Manga, MediaFormat.OneShot];

        public AnilistMangaMetadataProvider(ILogger<AnilistMangaMetadataProvider> logger) : base(logger) { }
    }
}
