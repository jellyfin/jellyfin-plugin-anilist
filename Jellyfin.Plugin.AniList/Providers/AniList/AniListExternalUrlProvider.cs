using System.Collections.Generic;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;

namespace Jellyfin.Plugin.AniList.Providers.AniList;

/// <summary>
/// External url provider for AniList.
/// </summary>
public class AniListExternalUrlProvider : IExternalUrlProvider
{
    public string Name => "AniList";

    public IEnumerable<string> GetExternalUrls(BaseItem item)
    {
        if (item.TryGetProviderId(ProviderNames.AniList, out var externalId))
        {
            switch (item)
            {
                case Series:
                case Movie:
                    yield return $"https://anilist.co/anime/{externalId}/";
                    break;
                case Person:
                    yield return $"https://anilist.co/staff/{externalId}/";
                    break;
            }
        }
    }
}
