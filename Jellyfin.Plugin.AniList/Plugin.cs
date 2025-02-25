using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using Jellyfin.Plugin.AniList.Configuration;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Common.Net;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using System.Globalization;

namespace Jellyfin.Plugin.AniList
{
    public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
    {
        IHttpClientFactory _httpClientFactory;
        public Plugin(
            IApplicationPaths applicationPaths,
            IXmlSerializer xmlSerializer,
            IHttpClientFactory httpClientFactory)
            : base(applicationPaths, xmlSerializer)
        {
            Instance = this;
            _httpClientFactory = httpClientFactory;
        }

        public HttpClient GetHttpClient() {
            var httpClient = _httpClientFactory.CreateClient(NamedClient.Default);
            httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(Name, Version.ToString()));

            return httpClient;
        }

        /// <inheritdoc />
        public override string Name => Constants.PluginName;

        /// <inheritdoc />
        public override Guid Id => Guid.Parse(Constants.PluginGuid);

        public static Plugin Instance { get; private set; }

        /// <inheritdoc />
        public IEnumerable<PluginPageInfo> GetPages()
        {
            return
            [
                new PluginPageInfo
                {
                    Name = Name,
                    EmbeddedResourcePath = string.Format(CultureInfo.InvariantCulture, "{0}.Configuration.configPage.html", GetType().Namespace)
                }
            ];
        }
    }
}
