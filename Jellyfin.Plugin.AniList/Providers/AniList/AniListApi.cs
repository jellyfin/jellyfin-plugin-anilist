using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Jellyfin.Plugin.AniList.Providers.AniList
{
    /// <summary>
    /// Based on the new API from AniList
    /// 🛈 This code works with the API Interface (v2) from AniList
    /// 🛈 https://anilist.gitbook.io/anilist-apiv2-docs
    /// 🛈 THIS IS AN UNOFFICAL API INTERFACE FOR JELLYFIN
    /// </summary>
    public class AniListApi
    {
        private const string BaseApiUrl = "https://graphql.anilist.co/";

        private static readonly Uri RefererUri = new("https://jellyfin.org/");

        private readonly ILogger _logger;

        private const string SearchMediaGraphqlQuery = """
            query ($query: String, $type: MediaType) {
              Page {
                media(search: $query, type: $type) {
                  id
                  format
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
            }
        """;

        private const string GetMediaGraphqlQuery = """
            query($id: Int!) {
              Media(id: $id) {
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
                  rank
                  category
                  isMediaSpoiler
                }
                nextAiringEpisode {
                  airingAt
                  timeUntilAiring
                  episode
                }

                studios {
                  edges {
                    node {
                      id
                      name
                      isAnimationStudio
                    }
                    isMain
                  }
                }
                characters(sort: [ROLE, FAVOURITES_DESC]) {
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
                      language: languageV2
                    }
                  }
                }
              }
            }
        """;

        private const string SearchStaffGraphqlQuery = """
            query($query: String) {
              Page {
                staff(search: $query) {
                  id
                  name {
                    first
                    last
                    full
                    native
                  }
                  image {
                    large
                    medium
                  }
                }
              }
            }
        """;

        private const string GetStaffGraphqlQuery = """
            query($id: Int!) {
              Staff(id: $id) {
                id
                name {
                  first
                  last
                  full
                  native
                }
                image {
                  large
                  medium
                }
                description(asHtml: true)
                homeTown
                dateOfBirth {
                  year
                  month
                  day
                }
                dateOfDeath {
                  year
                  month
                  day
                }
              }
            }
        """;

        private class GraphQlRequest {
            [JsonPropertyName("query")]
            public string Query { get; set; }

            [JsonPropertyName("variables")]
            public Dictionary<string, string> Variables { get; set; }
        }

        public AniListApi(ILogger logger = null)
        {
            _logger = logger ?? NullLogger.Instance;
        }

        /// <summary>
        /// API call to get the anime with the given id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<Media> GetMedia(string id, CancellationToken cancellationToken)
        {
            RootObject result = await WebRequestAPI(
                new GraphQlRequest {
                    Query = GetMediaGraphqlQuery,
                    Variables = new Dictionary<string, string> {{"id", id}},
                },
                cancellationToken
            ).ConfigureAwait(false);

            return result?.data?.Media;
        }

        /// <summary>
        /// API call to search a title and return a list of results
        /// </summary>
        /// <param name="title"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<List<MediaSearchResult>> SearchMedia(string title, MediaType mediaType, CancellationToken cancellationToken)
        {
            string mediaTypeString = mediaType switch
            {
                MediaType.Anime => "ANIME",
                MediaType.Manga => "MANGA",
                _ => throw new ArgumentException("invalid media type value"),
            };

            _logger.LogInformation("Searching mediaType: {0} query: {1}", mediaTypeString, title);

            RootObject result = await WebRequestAPI(
                new GraphQlRequest {
                    Query = SearchMediaGraphqlQuery,
                    Variables = new Dictionary<string, string> {{"query", title}, {"type", mediaTypeString}},
                },
                cancellationToken
            ).ConfigureAwait(false);

            return result?.data?.Page?.media ?? [];
        }

        public async Task<Staff> GetStaff(int id, CancellationToken cancellationToken)
        {
            RootObject result = await WebRequestAPI(
                new GraphQlRequest {
                    Query = GetStaffGraphqlQuery,
                    Variables = new Dictionary<string, string> {{"id", id.ToString(CultureInfo.InvariantCulture)}},
                },
                cancellationToken
            ).ConfigureAwait(false);

            return result?.data?.Staff;
        }

        public async Task<List<Staff>> SearchStaff(string query, CancellationToken cancellationToken)
        {
            RootObject result = await WebRequestAPI(
                new GraphQlRequest {
                    Query = SearchStaffGraphqlQuery,
                    Variables = new Dictionary<string, string> {{"query", query}},
                },
                cancellationToken
            ).ConfigureAwait(false);

            return result?.data?.Page?.staff ?? [];
        }

        /// <summary>
        /// Send a GraphQL request, deserialize into a RootObject
        /// </summary>
        /// <param name="request">The GraphQl request payload</param>
        /// <returns></returns>
        private async Task<RootObject> WebRequestAPI(GraphQlRequest request, CancellationToken cancellationToken)
        {
            var httpClient = Plugin.Instance.GetHttpClient();
            var requestBody = JsonSerializer.Serialize(request);

            for (var attempt = 0; attempt < 2; attempt++)
            {
                await AniListRateLimiter.WaitForRequestSlot(_logger, cancellationToken).ConfigureAwait(false);

                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseApiUrl)
                {
                    Content = new StringContent(requestBody, Encoding.UTF8, "application/json"),
                };
                httpRequest.Headers.Referrer = RefererUri;

                using var response = await httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var retryDelay = AniListRateLimiter.RegisterRateLimited(response, _logger);
                    _logger.LogInformation("Rate limited by AniList API. Retrying after {RetryDelay} ms.", retryDelay.TotalMilliseconds);
                    await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                    continue; // Retry one more time after the HTTP 429 delay
                }

                AniListRateLimiter.RegisterResponse(response, _logger);

                if (!response.IsSuccessStatusCode)
                {
                    var errorBody = await ReadErrorBody(response, cancellationToken).ConfigureAwait(false);
                    _logger.LogError("AniList API request failed with HTTP {StatusCode}: {Error}", (int)response.StatusCode, errorBody);
                    return null;
                }

                RootObject result;
                try
                {
                    using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                    result = await JsonSerializer.DeserializeAsync<RootObject>(responseStream, cancellationToken: cancellationToken).ConfigureAwait(false);
                }
                catch (JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize the response from the AniList API.");
                    return null;
                }

                if (result?.errors?.Count > 0)
                {
                    _logger.LogError("AniList API returned errors: {Errors}", FormatErrors(result.errors));
                }

                return result;
            }

            _logger.LogWarning("Failed to make request to AniList API after retrying due to rate limits. Giving up.");
            return null;
        }

        private static async Task<string> ReadErrorBody(HttpResponseMessage response, CancellationToken cancellationToken)
        {
            string body;
            try
            {
                body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                return response.ReasonPhrase;
            }

            try
            {
                var errors = JsonSerializer.Deserialize<RootObject>(body)?.errors;
                if (errors?.Count > 0)
                {
                    return FormatErrors(errors);
                }
            }
            catch (JsonException)
            {
                // Not a GraphQL error document, fall through to the raw body
            }

            body = body?.Trim();
            if (string.IsNullOrEmpty(body))
            {
                return response.ReasonPhrase;
            }

            return body.Length > 500 ? body[..500] : body;
        }

        private static string FormatErrors(List<GraphQlError> errors)
        {
            return string.Join("; ", errors.Select(e => e.message));
        }

    }
}
