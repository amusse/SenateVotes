using System;
using SenateVotes.Models;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Tweetinvi;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Linq;

namespace SenateVotes
{
    public static class SenateVotes
    {
        public const string API_KEY = "";
        public const string BASE_URL = "https://api.propublica.org";


        [FunctionName("SenateVotes")]
        public static async Task Run([TimerTrigger("0 0 19 * * *")]TimerInfo myTimer, ILogger log)
        {
            await GetVotes();
        }

        public static async Task GetVotes()
        {
            // Create a New HttpClient object and dispose it when done, so the app doesn't leak resources
            using (HttpClient client = new HttpClient() { BaseAddress = new Uri(BASE_URL)})
            {
                try
                {
                    client.DefaultRequestHeaders.Add("X-API-KEY", API_KEY);
                    await GetVotesForDate(client, "2019", "04");

                }
                catch (HttpRequestException e)
                {
                    Console.WriteLine("\nException Caught!");
                    Console.WriteLine("Message :{0} ", e.Message);
                }
            }
        }

        public static async Task GetVotesForDate(HttpClient client, string year, string month)
        {
            var uri = $"/congress/v1/senate/votes/{year}/{month}.json";
            var responseBody = await client.GetStringAsync(uri);
            var res = JsonConvert.DeserializeObject<Response>(responseBody);
            var votes = res.Results.Votes;
            var tweets = new List<string>();
            foreach (var vote in votes)
            {
                var voteUri = vote.VoteUri;
                uri = voteUri.Replace("https://api.propublica.org", "");
                var voteResp = await client.GetStringAsync(uri);
                var voteData = JsonConvert.DeserializeObject<dynamic>(voteResp);
                var positionsArray = voteData["results"]["votes"]["vote"]["positions"] as JArray;
                var memberPositions = positionsArray.ToObject<IEnumerable<Member>>();
                tweets.Add(CreateTweet(vote, memberPositions));
            }

            if (tweets.Count > 0)
            {
                int index = RandomNumber(0, tweets.Count - 1);
                if (!string.IsNullOrEmpty(tweets[index]))
                {
                    SendTweet(tweets[index]);
                }
            }
        }

        public static int RandomNumber(int min, int max)
        {
            Random random = new Random();
            return random.Next(min, max);
        }

        public static string CreateTweet(Vote vote, IEnumerable<Member> memberPositions)
        {
            // Format will look like this
            // {PASSED}{AGREED}{REJECTED}{CONFIRMED} (date): {document_title} (sponsor name, state, party) {url}
            var result = vote.Result.ToLower();
            if (result.Contains("passed"))
            {
                result = "PASSED";
            } else if (result.Contains("agreed"))
            {
                result = "AGREED";
            } else if (result.Contains("rejected"))
            {
                result = "REJECTED";
            } else if (result.Contains("confirmed"))
            {
                result = "CONFIRMED";
            } else if (result.Contains("not sustained"))
            {
                result = "NOT SUSTAINED";
            }

            var tweetText = "";
            if (vote.Bill != null && !string.IsNullOrEmpty(vote.Bill.SponsorId))
            {
                var sponsor = memberPositions.FirstOrDefault(i => i.MemberId == vote.Bill.SponsorId);
                if (sponsor != null)
                {
                    var sponsorName = sponsor.Name;
                    var sponsorState = sponsor.State;
                    var sponsorParty = sponsor.Party;

                    tweetText = $"{result} ({vote.Date}): {vote.DocumentTitle} ({sponsorName}, {sponsorParty}-{sponsorState}) {vote.Url}";
                    return tweetText;
                }
            }
            tweetText = $"{result} ({vote.Date}): {vote.DocumentTitle} {vote.Url}";

            return tweetText;
        }

        public static void AuthenticateTwitter()
        {
            var consumerKey = "";
            var consumerSecret = "";
            var accessToken = "";
            var accessTokenSecret = "";
            Auth.SetUserCredentials(consumerKey, consumerSecret, accessToken, accessTokenSecret);
        }

        public static void SendTweet(string tweet)
        {
            AuthenticateTwitter();
            Tweetinvi.Tweet.PublishTweet(tweet);
        }
    }
}
