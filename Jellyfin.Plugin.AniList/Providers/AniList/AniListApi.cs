using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.Anime.Configuration;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.Anime.Providers.AniList
{
    /// <summary>
    /// Based on the new API from AniList
    /// 🛈 This code works with the API Interface (v2) from AniList
    /// 🛈 https://anilist.gitbooks.io/anilist-apiv2-docs
    /// 🛈 THIS IS AN UNOFFICAL API INTERFACE FOR JELLYFIN
    /// </summary>
    public class AniListApi
    {
        private const string SearchLink = @"https://graphql.anilist.co/api/v2?query=
query ($query: String, $type: MediaType) {
  Page {
    media(search: $query, type: $type) {
      id
      title {
        romaji
        english
        native
      }
      coverImage {
        medium
        large
        extraLarge
      }
      startDate {
        year
        month
        day
      }
    }
  }
}&variables={ ""query"":""{0}"",""type"":""ANIME""}";
        public string AnimeLink = @"https://graphql.anilist.co/api/v2?query=
query($id: Int!, $type: MediaType) {
  Media(id: $id, type: $type) {
    id
    title {
      romaji
      english
      native
      userPreferred
    }
    startDate {
      year
      month
      day
    }
    endDate {
      year
      month
      day
    }
    coverImage {
      medium
      large
      extraLarge
    }
    bannerImage
    format
    type
    status
    episodes
    chapters
    volumes
    season
    seasonYear
    description
    averageScore
    meanScore
    genres
    synonyms
    duration
    tags {
      id
      name
      category
    }
    nextAiringEpisode {
      airingAt
      timeUntilAiring
      episode
    }

    studios {
      nodes {
        id
        name
        isAnimationStudio
      }
    }
    characters(sort: [ROLE]) {
      edges {
        node {
          id
          name {
            first
            last
            full
          }
          image {
            medium
            large
          }
        }
        role
        voiceActors {
          id
          name {
            first
            last
            full
            native
          }
          image {
            medium
            large
          }
          language
        }
      }
    }
  }
}&variables={ ""id"":""{0}"",""type"":""ANIME""}";

        static AniListApi()
        {
        }

        /// <summary>
        /// API call to get the anime with the given id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<Media> GetAnime(string id)
        {
            return (await WebRequestAPI(AnimeLink.Replace("{0}", id))).data?.Media;
        }

        /// <summary>
        /// API call to search a title and return the first result
        /// </summary>
        /// <param name="title"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<MediaSearchResult> Search_GetSeries(string title, CancellationToken cancellationToken)
        {
            // Reimplemented instead of calling Search_GetSeries_list() for efficiency
            RootObject WebContent = await WebRequestAPI(SearchLink.Replace("{0}", title));
            foreach (MediaSearchResult media in WebContent.data.Page.media)
            {
                return media;
            }
            return null;
        }

        /// <summary>
        /// API call to search a title and return a list of results
        /// </summary>
        /// <param name="title"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<List<MediaSearchResult>> Search_GetSeries_list(string title, CancellationToken cancellationToken)
        {
            return (await WebRequestAPI(SearchLink.Replace("{0}", title))).data.Page.media;
        }

        /// <summary>
        /// Search for anime with the given title. Attempts to fuzzy search by removing special characters
        /// </summary>
        /// <param name="title"></param>
        /// <returns></returns>
        public async Task<string> FindSeries(string title, CancellationToken cancellationToken)
        {
            MediaSearchResult result = await Search_GetSeries(title, cancellationToken);
            if (result != null)
            {
                return result.id.ToString();
            }

            result = await Search_GetSeries(await Equals_check.Clear_name(title, cancellationToken), cancellationToken);
            if (result != null)
            {
                return result.id.ToString();
            }

            return null;
        }

        /// <summary>
        /// GET and parse JSON content from link, deserialize into a RootObject
        /// </summary>
        /// <param name="link"></param>
        /// <returns></returns>
        public async Task<RootObject> WebRequestAPI(string link)
        {
            var httpClient = Plugin.Instance.GetHttpClient();
            using (HttpContent content = new FormUrlEncodedContent(Enumerable.Empty<KeyValuePair<string, string>>()))
            using (var response = await httpClient.PostAsync(link, content).ConfigureAwait(false))
            using (var responseStream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
            {
                return await JsonSerializer.DeserializeAsync<RootObject>(responseStream).ConfigureAwait(false);
            }
        }
    }
}
