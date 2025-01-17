﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Encodings.Web;
using System.Threading;
using System.Threading.Tasks;

using Google.Apis.Auth.OAuth2;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using LiveStandup.Shared.Models;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

//csharpfritz cheered 100 bits on July 3rd 2019

namespace LiveStandup.Web.Services
{
    /// <summary>
    /// YouTube Data API v3 sample: retrieve my uploads.
    /// Relies on the Google APIs Client Library for .NET, v1.7.0 or higher.
    /// See https://developers.google.com/api-client-library/dotnet/get_started
    /// </summary>
    public class YouTubeShowsService : IYouTubeShowsService
    {
        string YouTubeApiKey;
        string YouTubeAppName;
        string YouTubePlaylistId;
        string DefaultThumbnail;


        public YouTubeShowsService(IConfiguration configuration)
        {
            YouTubeApiKey = configuration["YouTube:Key"];
            YouTubeAppName = configuration["YouTube:AppName"];
            YouTubePlaylistId = configuration["YouTube:PlaylistId"];
            DefaultThumbnail = configuration[nameof(DefaultThumbnail)];
        }

        public async Task UpdateLiveStreamingDetails(YouTubeService youtubeService, IEnumerable<Show> shows)
        {

            var ids = string.Join(',', shows.Select(show => show.Id).ToArray());
            var request = youtubeService.Videos.List("liveStreamingDetails");
            request.Id = ids;
            request.MaxResults = 25;

            var videos = (await request.ExecuteAsync()).Items;
            

            foreach(var show in shows)
            {
                var liveData = videos.FirstOrDefault(v => v.Id == show.Id)?.LiveStreamingDetails;
                if (liveData == null)
                    continue;

                if (liveData.ScheduledStartTime.HasValue)
                    show.ScheduledStartTime = liveData.ScheduledStartTime.Value.ToUniversalTime();

                if (liveData.ActualEndTime.HasValue)
                    show.ActualEndTime = liveData.ActualEndTime.Value.ToUniversalTime();

                if (liveData.ActualStartTime.HasValue)
                    show.ActualStartTime = liveData.ActualStartTime.Value.ToUniversalTime();
            }
        }

        public async Task<IEnumerable<Show>> GetShows(int numberOfShows = 25)
        {
            var youtubeService = new YouTubeService(new BaseClientService.Initializer()
            {
                ApiKey = YouTubeApiKey,
                ApplicationName = YouTubeAppName
            });

            //var shows = new List<Show>(numberOfShows);


            var request = youtubeService.PlaylistItems.List("snippet,status");
            request.PlaylistId = YouTubePlaylistId;
            request.MaxResults = numberOfShows;
            //playlistItemsListRequest.PageToken = nextPageToken;

            // Retrieve the list of videos uploaded to the authenticated user's channel.
            var response = await request.ExecuteAsync();

            var shows = PlaylistItemsToShows(response);

            await UpdateLiveStreamingDetails(youtubeService, shows);
                        
            return shows.OrderByDescending(s => s.ScheduledStartTime);
        }

        internal List<Show> PlaylistItemsToShows(PlaylistItemListResponse response)
        {
            return response.Items
                .Where(item =>
                item.Status?.PrivacyStatus == "public" &&
                item.Snippet != null)
                .Select(item => new Show
                {
                    Id = item.Snippet.ResourceId.VideoId,
                    Title = item.Snippet.Title,
                    Description = item.Snippet.Description,
                    ThumbnailUrl = item.Snippet.Thumbnails?.Medium?.Url ?? item.Snippet.Thumbnails?.Standard?.Url ?? DefaultThumbnail,
                    Url = GetVideoUrl(item.Snippet.ResourceId.VideoId,
                    YouTubePlaylistId, item.Snippet.Position.GetValueOrDefault())
                }).ToList();
        }

        public static string GetVideoUrl(string id, string playlistId, long itemIndex)
        {
            var encodedId = UrlEncoder.Default.Encode(id);
            var encodedPlaylistId = UrlEncoder.Default.Encode(playlistId);
            var encodedItemIndex = UrlEncoder.Default.Encode(itemIndex.ToString());

            return $"https://www.youtube.com/watch?v={encodedId}&list={encodedPlaylistId}&index={encodedItemIndex}";
        }

        public static string GetPlaylistUrl(string playlistId) =>
            $"https://www.youtube.com/playlist?list={UrlEncoder.Default.Encode(playlistId)}";

    }
}