using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.AniList.Providers.AniList
{
    public class AniListPersonExternalId : IExternalId
    {
        public bool Supports(IHasProviderIds item)
            => item is Person;

        public string ProviderName
            => "AniList";

        public string Key
            => ProviderNames.AniList;

        public ExternalIdMediaType? Type
            => ExternalIdMediaType.Person;
    }
}
