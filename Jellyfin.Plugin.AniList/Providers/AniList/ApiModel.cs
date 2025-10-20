﻿using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Entities.Movies;
using Jellyfin.Plugin.AniList.Configuration;
using System.Globalization;

namespace Jellyfin.Plugin.AniList.Providers.AniList
{
    public class Title
    {
        public string romaji { get; set; }
        public string english { get; set; }
        public string native { get; set; }
    }

    public class CoverImage
    {
        public string medium { get; set; }
        public string large { get; set; }
        public string extraLarge { get; set; }
    }

    public class FuzzyDate
    {
        public int? year { get; set; }
        public int? month { get; set; }
        public int? day { get; set; }

        public DateTime? ToDateTime()
        {
            if (day is null || month is null || year is null)
            {
                return null;
            }

            return new DateTime(year.Value, month.Value, day.Value);
        }
    }

    public class Page
    {
        public List<MediaSearchResult> media { get; set; }
        public List<Staff> staff { get; set; }
    }

    public class Data
    {
        public Page Page { get; set; }
        public Media Media { get; set; }
        public Staff Staff { get; set; }
    }

    /// <summary>
    /// A slimmed down version of Media to avoid confusion and reduce
    /// the size of responses when searching.
    /// </summary>
    public class MediaSearchResult
    {
        public int id { get; set; }
        public Title title { get; set; }
        public FuzzyDate startDate { get; set; }
        public CoverImage coverImage { get; set; }

        /// <summary>
        /// Get the title in configured language
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        public string GetPreferredTitle(TitlePreferenceType preference, string language)
        {
            if (preference == TitlePreferenceType.Localized)
            {
                if (language == "en")
                {
                    return title.english;
                }
                if (language == "jap")
                {
                    return title.native;
                }
            }
            if (preference == TitlePreferenceType.Japanese)
            {
                return title.native;
            }

            return title.romaji;
        }

        /// <summary>
        /// Get the highest quality image url
        /// </summary>
        /// <returns></returns>
        public string GetImageUrl()
        {
            return coverImage.extraLarge ?? coverImage.large ?? coverImage.medium;
        }

        /// <summary>
        /// Convert a Media/MediaSearchResult object to a RemoteSearchResult
        /// </summary>
        /// <returns></returns>
        public RemoteSearchResult ToSearchResult()
        {
            PluginConfiguration config = Plugin.Instance.Configuration;
            return new RemoteSearchResult
            {
                Name = GetPreferredTitle(config.TitlePreference, "en"),
                ProductionYear = startDate.year,
                PremiereDate = startDate?.ToDateTime(),
                ImageUrl = GetImageUrl(),
                SearchProviderName = ProviderNames.AniList,
                ProviderIds = new Dictionary<string, string>() {{ProviderNames.AniList, id.ToString(CultureInfo.InvariantCulture)}}
            };
        }
    }

    public class Media : MediaSearchResult
    {
        public int? averageScore { get; set; }
        public string bannerImage { get; set; }
        public object chapters { get; set; }
        public CharacterConnection characters { get; set; }
        public string description { get; set; }
        public int? duration { get; set; }
        public FuzzyDate endDate { get; set; }
        public int? episodes { get; set; }
        public string format { get; set; }
        public List<string> genres { get; set; }
        public object hashtag { get; set; }
        public bool isAdult { get; set; }
        public int? meanScore { get; set; }
        public object nextAiringEpisode { get; set; }
        public int? popularity { get; set; }
        public string season { get; set; }
        public int? seasonYear { get; set; }
        public string status { get; set; }
        public StudioConnection studios { get; set; }
        public List<object> synonyms { get; set; }
        public List<Tag> tags { get; set; }
        public string type { get; set; }
        public object volumes { get; set; }

        /// <summary>
        /// Get the rating, normalized to 1-10
        /// </summary>
        /// <returns></returns>
        public float GetRating()
        {
            return (averageScore ?? 0) / 10f;
        }

        /// <summary>
        /// Returns a list of studio names
        /// </summary>
        /// <returns></returns>
        public List<string> GetStudioNames()
        {
            PluginConfiguration config = Plugin.Instance.Configuration;

            List<string> results = [];
            foreach (StudioEdge edge in studios.edges)
            {
                if (
                  !results.Contains(edge.node.name) &&
                  (
                    config.StudioFilterPreference == StudioFilterType.All ||
                    (config.StudioFilterPreference == StudioFilterType.MainOnly && edge.isMain) ||
                    (config.StudioFilterPreference == StudioFilterType.AnimationStudioOnly && edge.node.isAnimationStudio)
                  )
                )
                {
                    results.Add(edge.node.name);
                }
            }
            return results;
        }

        /// <summary>
        /// Returns a list of PersonInfo for voice actors
        /// </summary>
        /// <returns></returns>
        public List<PersonInfo> GetPeopleInfo()
        {
            PluginConfiguration config = Plugin.Instance.Configuration;
            List<PersonInfo> lpi = [];

            foreach (CharacterEdge edge in characters.edges)
            {
                foreach (Staff va in edge.voiceActors)
                {
                    if (config.PersonLanguageFilterPreference != LanguageFilterType.All)
                    {
                        if (config.PersonLanguageFilterPreference == LanguageFilterType.Japanese
                            && !va.language.Equals("Japanese", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                        if (config.PersonLanguageFilterPreference == LanguageFilterType.Localized
                            && va.language.Equals("Japanese", StringComparison.OrdinalIgnoreCase))
                        {
                            continue;
                        }
                    }

                    PeopleHelper.AddPerson(lpi, new PersonInfo {
                        Name = va.name.full,
                        ImageUrl = va.image.GetBestImage(),
                        Role = edge.node.name.full,
                        Type = PersonKind.Actor,
                        ProviderIds = new Dictionary<string, string>() {{ProviderNames.AniList, va.id.ToString(CultureInfo.InvariantCulture)}},
                    });
                }
            }

            if (config.MaxPeople > 0)
            {
                lpi = lpi.Take(config.MaxPeople).ToList();
            }

            return lpi;
        }

        /// <summary>
        /// Returns a list of tag names
        /// </summary>
        /// <returns></returns>
        public List<string> GetTagNames()
        {
            PluginConfiguration config = Plugin.Instance.Configuration;

            tags = tags.Where(o => o.rank >= config.MinTagRank).OrderBy(o => o.rank).ToList();
            if (config.AniListShowSpoilerTags)
            {
                tags = tags.Where(o => !o.isMediaSpoiler).ToList();
            }
            if (config.MaxTags > 0)
            {
                tags = tags.Take(config.MaxTags).ToList();
            }

            return (from tag in tags select tag.name).ToList();
        }

        /// <summary>
        /// Returns a list of genres
        /// </summary>
        /// <returns></returns>
        public List<string> GetGenres()
        {
            PluginConfiguration config = Plugin.Instance.Configuration;

            if (config.AnimeDefaultGenre != AnimeDefaultGenreType.None)
            {
                genres = genres
                    .Except(["Animation", "Anime"])
                    .Prepend(config.AnimeDefaultGenre.ToString())
                    .ToList();
            }

            if (config.MaxGenres > 0)
            {
                genres = genres.Take(config.MaxGenres).ToList();
            }

            return genres.OrderBy(i => i).ToList();
        }

        /// <summary>
        /// Convert a Media object to a Series
        /// </summary>
        /// <returns></returns>
        public Series ToSeries()
        {
            PluginConfiguration config = Plugin.Instance.Configuration;
            var result = new Series {
                Name = GetPreferredTitle(config.TitlePreference, "en"),
                OriginalTitle = GetPreferredTitle(config.OriginalTitlePreference, "en"),
                Overview = description,
                ProductionYear = startDate.year,
                PremiereDate = startDate?.ToDateTime(),
                EndDate = endDate?.ToDateTime(),
                CommunityRating = GetRating(),
                RunTimeTicks = duration.HasValue ? TimeSpan.FromMinutes(duration.Value).Ticks : null,
                Genres = GetGenres().ToArray(),
                Tags = GetTagNames().ToArray(),
                Studios = GetStudioNames().ToArray(),
                ProviderIds = new Dictionary<string, string>() {{ProviderNames.AniList, id.ToString(CultureInfo.InvariantCulture)}}
            };

            if (status == "FINISHED" || status == "CANCELLED")
            {
                result.Status = SeriesStatus.Ended;
            }
            else if (status == "RELEASING")
            {
                result.Status = SeriesStatus.Continuing;
            }
            else if (status == "NOT_YET_RELEASED")
            {
                result.Status = SeriesStatus.Unreleased;
            }

            return result;
        }

        /// <summary>
        /// Convert a Media object to a Movie
        /// </summary>
        /// <returns></returns>
        public Movie ToMovie()
        {
            PluginConfiguration config = Plugin.Instance.Configuration;
            return new Movie {
                Name = GetPreferredTitle(config.TitlePreference, "en"),
                OriginalTitle = GetPreferredTitle(config.OriginalTitlePreference, "en"),
                Overview = description,
                ProductionYear = startDate.year,
                PremiereDate = startDate?.ToDateTime(),
                EndDate = endDate?.ToDateTime(),
                CommunityRating = GetRating(),
                Genres = GetGenres().ToArray(),
                Tags = GetTagNames().ToArray(),
                Studios = GetStudioNames().ToArray(),
                ProviderIds = new Dictionary<string, string>() {{ProviderNames.AniList, id.ToString(CultureInfo.InvariantCulture)}}
            };
        }
    }
    public class PageInfo
    {
        public int total { get; set; }
        public int perPage { get; set; }
        public bool hasNextPage { get; set; }
        public int currentPage { get; set; }
        public int lastPage { get; set; }
    }

    public class Name
    {
        public string first { get; set; }
        public string last { get; set; }
    }

    public class Image
    {
        public string medium { get; set; }
        public string large { get; set; }

        public string GetBestImage()
        {
            if (IsValidImage(large))
            {
                return large;
            }

            if (IsValidImage(medium))
            {
                return medium;
            }

            return null;
        }

        private static bool IsValidImage(string imageUrl)
        {
            // Filter out the default "No image" picture.
            return !string.IsNullOrEmpty(imageUrl) && !imageUrl.EndsWith("default.jpg", StringComparison.OrdinalIgnoreCase);
        }
    }

    public class Character
    {
        public int id { get; set; }
        public CharacterName name { get; set; }
        public Image image { get; set; }
    }

    public class CharacterName
    {
        public string first { get; set; }
        public string last { get; set; }
        public string full { get; set; }
        public string native { get; set; }
    }

    public class Staff
    {
        public int id { get; set; }
        public StaffName name { get; set; }
        public string language { get; set; }
        public Image image { get; set; }
        public string description { get; set; }
        public FuzzyDate dateOfBirth { get; set; }
        public FuzzyDate dateOfDeath { get; set; }
        public string homeTown { get; set; }

        public Person ToPerson()
        {
            Person person = new Person {
                Name = name.full,
                OriginalTitle = name.native,
                Overview = description,
                PremiereDate = dateOfBirth?.ToDateTime(),
                EndDate = dateOfDeath?.ToDateTime(),
                ProviderIds = new Dictionary<string, string>() {{ProviderNames.AniList, id.ToString(CultureInfo.InvariantCulture)}}
            };

            if (!string.IsNullOrWhiteSpace(homeTown))
            {
                person.ProductionLocations = [homeTown];
            }

            return person;
        }

        public RemoteSearchResult ToSearchResult()
        {
            return new RemoteSearchResult() {
                SearchProviderName = ProviderNames.AniList,
                Name = name.full,
                ImageUrl = image.GetBestImage(),
                ProviderIds = new Dictionary<string, string>() {{ProviderNames.AniList, id.ToString(CultureInfo.InvariantCulture)}}
            };
        }
    }

    public class StaffName
    {
        public string first { get; set; }
        public string last { get; set; }
        public string full { get; set; }
        public string native { get; set; }
    }

    public class CharacterEdge
    {
        public Character node { get; set; }
        public string role { get; set; }
        public List<Staff> voiceActors { get; set; }
    }

    public class CharacterConnection
    {
        public List<CharacterEdge> edges { get; set; }
    }

    public class Tag
    {
        public int id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public int rank { get; set; }
        public string category { get; set; }
        public bool isMediaSpoiler { get; set; }
    }

    public class Studio
    {
        public int id { get; set; }
        public string name { get; set; }
        public bool isAnimationStudio { get; set; }
    }

    public class StudioEdge
    {
        public Studio node { get; set; }
        public bool isMain { get; set; }
    }

    public class StudioConnection
    {
        public List<StudioEdge> edges { get; set; }
    }

    public class RootObject
    {
        public Data data { get; set; }
    }
}
