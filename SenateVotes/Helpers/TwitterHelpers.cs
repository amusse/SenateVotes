using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Tweetinvi;
using Tweetinvi.Models;

namespace SenateVotes.Helpers
{
    public static class TwitterHelpers
    {
        public static void AuthenticateTwitter(ILogger log)
        {

            var credentials = Auth.SetUserCredentials(HelperMethods.GetEnvironmentVariable("CONSUMER_KEY"), 
                                                      HelperMethods.GetEnvironmentVariable("CONSUMER_SECRET"),
                                                      HelperMethods.GetEnvironmentVariable("ACCESS_TOKEN"),
                                                      HelperMethods.GetEnvironmentVariable("ACCESS_TOKEN_SECRET"));
            if (credentials == null)
            {
                log.LogError($"Twitter credentials came back empty. Unable to authenticate.");
            }
            else
            {
                log.LogInformation($"Twitter account authenticated successfully!");
            }
        }

        public static void DeleteAllTweets()
        {
            var tweets = GetMyTweets(HelperMethods.GetEnvironmentVariable("TWITTER_USERNAME"));
            DeleteTweets(tweets);
        }

        public static IList<ITweet> GetMyTweets(string username)
        {
            return Timeline.GetUserTimeline(username).ToList();
        }

        public static void DeleteTweets(IList<ITweet> tweets)
        {
            foreach (var tweet in tweets)
            {
                var success = Tweet.DestroyTweet(tweet);
            }
        }
    }
}
