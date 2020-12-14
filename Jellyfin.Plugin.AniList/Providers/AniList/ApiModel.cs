using System;
using System.Collections.Generic;
using System.Text;
using MediaBrowser.Model.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Entities.Movies;
using Jellyfin.Plugin.Anime.Configuration;

namespace Jellyfin.Plugin.Anime.Providers.AniList
{
    using System.Collections.Generic;
    
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

    public class ApiDate
    {
        public int? year { get; set; }
        public int? month { get; set; }
        public int? day { get; set; }
    }

    public class Page
    {
        public List<MediaSearchResult> media { get; set; }
    }

    public class Data
    {
        public Page Page { get; set; }
        public Media Media { get; set; }
    }

    /// <summary>
    /// A slimmed down version of Media to avoid confusion and reduce
    /// the size of responses when searching.
    /// </summary>
    public class MediaSearchResult
    {
        public int id { get; set; }
        public Title title { get; set; }
        public ApiDate startDate { get; set; }
        public CoverImage coverImage { get; set; }

        /// <summary>
        /// Get the title in configured language
        /// </summary>
        /// <param name="language"></param>
        /// <returns></returns>
        public string GetPreferredTitle(string language)
        {
            PluginConfiguration config = Plugin.Instance.Configuration;
            if (config.TitlePreference == TitlePreferenceType.Localized)
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
            if (config.TitlePreference == TitlePreferenceType.Japanese)
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
        /// Returns the start date as a DateTime object or null if not available
        /// </summary>
        /// <returns></returns>
        public DateTime? GetStartDate()
        {
            if (this.startDate.year == null || this.startDate.month == null || this.startDate.day == null)
            {
                return null;
            }
            return new DateTime(this.startDate.year.Value, this.startDate.month.Value, this.startDate.day.Value);
        }

        /// <summary>
        /// Convert a Media/MediaSearchResult object to a RemoteSearchResult
        /// </summary>
        /// <returns></returns>
        public RemoteSearchResult ToSearchResult()
        {
            return new RemoteSearchResult
            {
                Name = this.GetPreferredTitle("en"),
                ProductionYear = this.startDate.year,
                PremiereDate = this.GetStartDate(),
                ImageUrl = this.GetImageUrl(),
                SearchProviderName = ProviderNames.AniList,
                ProviderIds = new Dictionary<string, string>() {{ProviderNames.AniList, this.id.ToString()}}
            };
        }
    }

    public class Media: MediaSearchResult
    {
        public int? averageScore { get; set; }
        public string bannerImage { get; set; }
        public object chapters { get; set; }
        public Characters characters { get; set; }
        public string description { get; set; }
        public int? duration { get; set; }
        public ApiDate endDate { get; set; }
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
        /// Returns the end date as a DateTime object or null if not available
        /// </summary>
        /// <returns></returns>
        public DateTime? GetEndDate()
        {
            if (this.endDate.year == null || this.endDate.month == null || this.endDate.day == null)
            {
                return null;
            }
            return new DateTime(this.endDate.year.Value, this.endDate.month.Value, this.endDate.day.Value);
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
            List<PersonInfo> lpi = new List<PersonInfo>();
            foreach (CharacterEdge edge in this.characters.edges)
            {
                foreach (VoiceActor va in edge.voiceActors)
                {
                    PeopleHelper.AddPerson(lpi, new PersonInfo {
                        Name = va.name.full,
                        ImageUrl = va.image.large ?? va.image.medium,
                        Role = edge.node.name.full,
                        Type = PersonType.Actor,
                        ProviderIds = new Dictionary<string, string>() {{ProviderNames.AniList, this.id.ToString()}}
                    });
                }
            }
            return lpi;
        }

        /// <summary>
        /// Returns a list of tag names
        /// </summary>
        /// <returns></returns>
        public List<string> GetTagNames()
        {
            List<string> results = new List<string>();
            foreach (Tag tag in this.tags)
            {
                results.Add(tag.name);
            }
            return results;
        }

        /// <summary>
        /// Convert a Media object to a Series
        /// </summary>
        /// <returns></returns>
        public Series ToSeries()
        {
            var result = new Series {
                Name = this.GetPreferredTitle("en"),
                Overview = this.description,
                ProductionYear = this.startDate.year,
                PremiereDate = this.GetStartDate(),
                EndDate = this.GetStartDate(),
                CommunityRating = this.GetRating(),
                RunTimeTicks = this.duration.HasValue ? TimeSpan.FromMinutes(this.duration.Value).Ticks : (long?)null,
                Genres = this.genres.ToArray(),
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

            return result;
        }

        /// <summary>
        /// Convert a Media object to a Movie
        /// </summary>
        /// <returns></returns>
        public Movie ToMovie()
        {
            return new Movie {
                Name = this.GetPreferredTitle("en"),
                Overview = this.description,
                ProductionYear = this.startDate.year,
                PremiereDate = this.GetStartDate(),
                EndDate = this.GetStartDate(),
                CommunityRating = this.GetRating(),
                Genres = this.genres.ToArray(),
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
    }

    public class Character
    {
        public int id { get; set; }
        public Name2 name { get; set; }
        public Image image { get; set; }
    }

    public class Name2
    {
        public string first { get; set; }
        public string last { get; set; }
        public string full { get; set; }
        public string native { get; set; }
    }

    public class VoiceActor
    {
        public int id { get; set; }
        public Name2 name { get; set; }
        public Image image { get; set; }
        public string language { get; set; }
    }

    public class CharacterEdge
    {
        public Character node { get; set; }
        public string role { get; set; }
        public List<VoiceActor> voiceActors { get; set; }
    }

    public class Characters
    {
        public List<CharacterEdge> edges { get; set; }
    }

    public class Tag
    {
        public int id { get; set; }
        public string name { get; set; }
        public string description { get; set; }
        public string category { get; set; }
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