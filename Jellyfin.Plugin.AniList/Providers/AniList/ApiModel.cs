﻿using System;
using System.Collections.Generic;
using System.Linq;
using Jellyfin.Data.Enums;
using System.Text.Json.Serialization;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Entities.Movies;
using Jellyfin.Plugin.AniList.Configuration;

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
            if (this.day == null || this.month == null || this.year == null)
            {
                return null;
            }

            return new DateTime(this.year.Value, this.month.Value, this.day.Value);
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
                    return this.title.english;
                }
                if (language == "jap")
                {
                    return this.title.native;
                }
            }
            if (preference == TitlePreferenceType.Japanese)
            {
                return this.title.native;
            }

            return this.title.romaji;
        }

        /// <summary>
        /// Get the highest quality image url
        /// </summary>
        /// <returns></returns>
        public string GetImageUrl()
        {
            return this.coverImage.extraLarge ?? this.coverImage.large ?? this.coverImage.medium;
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
                Name = this.GetPreferredTitle(config.TitlePreference, "en"),
                ProductionYear = this.startDate.year,
                PremiereDate = this.startDate?.ToDateTime(),
                ImageUrl = this.GetImageUrl(),
                SearchProviderName = ProviderNames.AniList,
                ProviderIds = new Dictionary<string, string>() {{ProviderNames.AniList, this.id.ToString()}}
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
            return (this.averageScore ?? 0) / 10f;
        }

        /// <summary>
        /// Returns a list of studio names
        /// </summary>
        /// <returns></returns>
        public List<string> GetStudioNames()
        {
            List<string> results = new List<string>();
            foreach (Studio node in this.studios.nodes)
            {
                results.Add(node.name);
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
            List<PersonInfo> lpi = new List<PersonInfo>();

            foreach (CharacterEdge edge in this.characters.edges)
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
                        ProviderIds = new Dictionary<string, string>() {{ProviderNames.AniList, va.id.ToString()}},
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
            return (from tag in this.tags where config.AniListShowSpoilerTags || !tag.isMediaSpoiler select tag.name).ToList();
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
                this.genres = this.genres
                    .Except(new[] { "Animation", "Anime" })
                    .Prepend(config.AnimeDefaultGenre.ToString())
                    .ToList();
            }

            if (config.MaxGenres > 0)
            {
                this.genres = this.genres.Take(config.MaxGenres).ToList();
            }

            return this.genres.OrderBy(i => i).ToList();
        }

        /// <summary>
        /// Convert a Media object to a Series
        /// </summary>
        /// <returns></returns>
        public Series ToSeries()
        {
            PluginConfiguration config = Plugin.Instance.Configuration;
            var result = new Series {
                Name = this.GetPreferredTitle(config.TitlePreference, "en"),
                OriginalTitle = this.GetPreferredTitle(config.OriginalTitlePreference, "en"),
                Overview = this.description,
                ProductionYear = this.startDate.year,
                PremiereDate = this.startDate?.ToDateTime(),
                EndDate = this.endDate?.ToDateTime(),
                CommunityRating = this.GetRating(),
                RunTimeTicks = this.duration.HasValue ? TimeSpan.FromMinutes(this.duration.Value).Ticks : null,
                Genres = this.GetGenres().ToArray(),
                Tags = this.GetTagNames().ToArray(),
                Studios = this.GetStudioNames().ToArray(),
                ProviderIds = new Dictionary<string, string>() {{ProviderNames.AniList, this.id.ToString()}}
            };

            if (this.status == "FINISHED" || this.status == "CANCELLED")
            {
                result.Status = SeriesStatus.Ended;
            }
            else if (this.status == "RELEASING")
            {
                result.Status = SeriesStatus.Continuing;
            }
            else if (this.status == "NOT_YET_RELEASED")
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
                Name = this.GetPreferredTitle(config.TitlePreference, "en"),
                OriginalTitle = this.GetPreferredTitle(config.OriginalTitlePreference, "en"),
                Overview = this.description,
                ProductionYear = this.startDate.year,
                PremiereDate = this.startDate?.ToDateTime(),
                EndDate = this.endDate?.ToDateTime(),
                CommunityRating = this.GetRating(),
                Genres = this.GetGenres().ToArray(),
                Tags = this.GetTagNames().ToArray(),
                Studios = this.GetStudioNames().ToArray(),
                ProviderIds = new Dictionary<string, string>() {{ProviderNames.AniList, this.id.ToString()}}
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
            if (IsValidImage(this.large))
            {
                return this.large;
            }

            if (IsValidImage(this.medium))
            {
                return this.medium;
            }

            return null;
        }

        private static bool IsValidImage(string imageUrl)
        {
            // Filter out the default "No image" picture.
            return !string.IsNullOrEmpty(imageUrl) && !imageUrl.EndsWith("default.jpg");
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
                Name = this.name.full,
                OriginalTitle = this.name.native,
                Overview = this.description,
                PremiereDate = this.dateOfBirth?.ToDateTime(),
                EndDate = this.dateOfDeath?.ToDateTime(),
                ProviderIds = new Dictionary<string, string>() {{ProviderNames.AniList, this.id.ToString()}}
            };

            if (!string.IsNullOrWhiteSpace(this.homeTown))
            {
                person.ProductionLocations = new[] { this.homeTown };
            }

            return person;
        }

        public RemoteSearchResult ToSearchResult()
        {
            return new RemoteSearchResult() {
                SearchProviderName = ProviderNames.AniList,
                Name = this.name.full,
                ImageUrl = this.image.GetBestImage(),
                ProviderIds = new Dictionary<string, string>() {{ProviderNames.AniList, this.id.ToString()}}
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
        public string category { get; set; }
        public bool isMediaSpoiler { get; set; }
    }

    public class Studio
    {
        public int id { get; set; }
        public string name { get; set; }
        public bool isAnimationStudio { get; set; }
    }

    public class StudioConnection
    {
        public List<Studio> nodes { get; set; }
    }

    public class RootObject
    {
        public Data data { get; set; }
    }
}
