using System;
using Jellyfin.Plugin.AniList.Providers.AniList;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Configuration;
using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;

namespace Jellyfin.Plugin.AniList;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    /// <inheritdoc />
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost applicationHost)
    {
        serviceCollection.AddTransient<AniListApi>();
        serviceCollection
            .AddHttpClient(AniListApi.AnilistHttpClient)
            .AddHttpMessageHandler(BuildHttpHandler);
    }

    RateLimiterHandler BuildHttpHandler(IServiceProvider serviceProvider)
    {
        return new RateLimiterHandler(Plugin.Instance.Configuration.ApiRateLimit);
    }
}
