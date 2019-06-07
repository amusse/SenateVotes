using System;
using System.IO;
using SenateVotes.Models;
using SenateVotes.Helpers;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Tweetinvi;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using System.Linq;
using Tweetinvi.Parameters;
using SixLabors.Fonts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using SixLabors.Primitives;
using System.Threading;

namespace SenateVotes
{
    public static class SenateVotes
    {
        // TODO: Move these constants to a constants file
        private const string PRO_PUBLICA_BASE_URL = "PRO_PUBLICA_BASE_URL";
        private const string PRO_PUBLICA_API_KEY = "PRO_PUBLICA_API_KEY";
        private const int TIME_TO_WAIT_BETWEEN_TWEETS = 2000; // In milliseconds
        private const int MAX_TWEET_BODY_LENGTH = 165;

        [FunctionName("SenateVotes")]
        public static async Task Run([TimerTrigger("0 0 20 * * *")]TimerInfo myTimer, ILogger log)
        {
            TwitterHelpers.AuthenticateTwitter(log);
            await TweetVotes(log);
        }

        private static async Task TweetVotes(ILogger log)
        {
            // Create a New HttpClient object and dispose it when done, so the app doesn't leak resources
            using (HttpClient client = new HttpClient() { BaseAddress = new Uri(HelperMethods.GetEnvironmentVariable(PRO_PUBLICA_BASE_URL)) })
            {
                try
                {
                    client.DefaultRequestHeaders.Add("X-API-KEY", HelperMethods.GetEnvironmentVariable(PRO_PUBLICA_API_KEY));

                    // Get all the votes for the current date
                    var votes = await GetVotesForDate(log, client, DateTime.Now);

                    // Formulate tweet body for each individual vote
                    var tweets = await CreateTweets(log, client, votes);

                    // Publish the tweets
                    PublishTweets(log, tweets);
                }
                catch (HttpRequestException e)
                {
                    log.LogError($"Exception when making request to API. Message: {e.Message}");
                }
            }
        }

        public static async Task<List<Vote>> GetVotesForDate(ILogger log, HttpClient client, DateTime currentDate)
        {
            var uri = $"/congress/v1/senate/votes/{currentDate.Year}/{currentDate.Month}.json";
            var responseBody = await client.GetStringAsync(uri);

            IEnumerable<Vote> votes = new List<Vote>();
            Response res = null;
            try
            {
                res = JsonConvert.DeserializeObject<Response>(responseBody);
            }
            catch (Exception e)
            {
                log.LogError($"Exception when deserializing response body. Message: {e.Message}");
                return votes.ToList();
            }
            if (res != null && res.Results != null && res.Results.Votes != null)
            {
                votes = res.Results.Votes;
                log.LogInformation($"Retrieved {votes.Count()} total votes for date: {currentDate}");
            }
            else
            {
                log.LogInformation($"No votes found in response body for date: {currentDate}. Response: {res}");
                return votes.ToList();
            }

            votes = votes.Where(vote =>
            {
                try
                {
                    var dayOfVote = Convert.ToDateTime(vote.Date).Day;
                    return dayOfVote >= currentDate.Day;
                }
                catch (FormatException e)
                {
                    log.LogError($"Exception when converting date to DateTime. Message: {e.Message}");
                    return false;
                }

            });

            log.LogInformation($"After filtering by day, retrieved {votes.Count()} total votes for day: {currentDate.Day}");
            votes = votes.OrderBy(o => o.RollCall);
            return votes.ToList();
        }

        public static async Task<List<Models.Tweet>> CreateTweets(ILogger log, HttpClient client, List<Vote> votes)
        {
            var tweets = new List<Models.Tweet>();
            foreach (var vote in votes)
            {
                var voteUri = vote.VoteUri;
                var uri = voteUri.Replace(HelperMethods.GetEnvironmentVariable(PRO_PUBLICA_BASE_URL), "");
                var voteResp = await client.GetStringAsync(uri);
                var voteData = JsonConvert.DeserializeObject<dynamic>(voteResp);
                JArray positionsArray = null;
                try
                {
                    positionsArray = voteData["results"]["votes"]["vote"]["positions"] as JArray;
                }
                catch (Exception e)
                {
                    log.LogError($"Exception when converting senate positions to JArray. Message: {e.Message}");
                    return tweets;
                }

                var memberPositions = positionsArray.ToObject<IEnumerable<Member>>();

                // Create body of tweet
                var tweetText = CreateTweetText(log, vote, memberPositions);

                var newTweet = new Models.Tweet
                {
                    VoteDescription = vote.Description,
                    Result = vote.Result,
                    ShortResult = vote.ShortResult,
                    MemberPositions = memberPositions,
                    TweetText = tweetText,
                    VoteUrl = vote.Url,
                    VoteType = vote.VoteType,
                    VoteDate = vote.Date
                };

                if (!string.IsNullOrEmpty(vote.Bill.ApiUri))
                {
                    var billUri = vote.Bill.ApiUri;
                    uri = billUri.Replace(HelperMethods.GetEnvironmentVariable(PRO_PUBLICA_BASE_URL), "");
                    var billResp = await client.GetStringAsync(uri);
                    var billData = JsonConvert.DeserializeObject<dynamic>(billResp);
                    if (billData != null)
                    {
                        if (billData["results"] is JArray billArray && billArray.Count > 0)
                        {
                            vote.Bill.Summary = billArray[0]["summary"].ToObject<string>();
                            vote.Bill.ShortSummary = billArray[0]["summary_short"].ToObject<string>();
                            newTweet.BillSummary = vote.Bill.Summary;
                        }
                    }
                }

                tweets.Add(newTweet);
            }

            log.LogInformation($"Number of tweets attempting to be published: {tweets.Count}");
            return tweets;
        }

        public static void PublishTweets(ILogger log, List<Models.Tweet> tweets)
        {
            // Create a mapping between abbreviated state names to full state names
            var statesDict = HelperMethods.BuildStateDictionary();

            log.LogInformation($"Number of tweets attempting to be published: {tweets.Count}");
            var tweetIndex = 0;
            var numPublishedTweets = 0;
            foreach (var tweet in tweets)
            {
                log.LogInformation($"TweetIndex: {tweetIndex}, VoteUrl: {tweet.VoteUrl}, TweetText: {tweet.TweetText}");
                var didPublish = SendTweet(log, tweet, statesDict);

                // Wait until tweet hits twiter before moving on to the next tweet
                Thread.Sleep(TIME_TO_WAIT_BETWEEN_TWEETS);
                if (didPublish == true)
                {
                    numPublishedTweets++;
                }
                tweetIndex++;
            }
            log.LogInformation($"Number of tweets published: {numPublishedTweets}");
        }

        private static string CreateTweetText(ILogger log, Vote vote, IEnumerable<Member> memberPositions)
        {
            // Format will look like this
            // PASSED (date) (sponsor name, state, party) : {document_title} {url}
            var tweetText = string.Empty;
            if (vote.Bill != null && !string.IsNullOrEmpty(vote.Bill.SponsorId))
            {
                var description = string.Empty;
                if (!string.IsNullOrEmpty(vote.Bill.Title))
                {
                    description = vote.Bill.Title;
                }
                var sponsor = memberPositions.FirstOrDefault(i => i.MemberId == vote.Bill.SponsorId);
                if (sponsor != null)
                {
                    var sponsorName = sponsor.Name;
                    var sponsorState = sponsor.State;
                    var sponsorParty = sponsor.Party;
                    tweetText = $"{vote.Result} ({vote.Date}) (Spon. by: {sponsorName}, {sponsorParty}-{sponsorState}): {description}";
                }
                else
                {
                    tweetText = $"{vote.Result} ({vote.Date}): {description}";
                }
            }
            else
            {
                tweetText = $"{vote.Result} ({vote.Date}): {vote.DocumentTitle}";
            }

            tweetText = HelperMethods.TruncateString(tweetText, MAX_TWEET_BODY_LENGTH);
            tweetText = $"{tweetText} {vote.Url}";

            if (string.IsNullOrEmpty(tweetText))
            {
                log.LogError("Tweet text is empty");
            }
            return tweetText;
        }

        private static bool SendTweet(ILogger log, Models.Tweet tweet, Dictionary<string, string> statesDict)
        {
            tweet.VotingMembers = GetMemberVotes(log, tweet.MemberPositions, statesDict);

            // Creates an image of senator voting positions to upload along with the tweet
            var filePath = CreateImage(log, tweet);
            if (!string.IsNullOrEmpty(filePath))
            {
                byte[] file1 = File.ReadAllBytes(filePath);

                log.LogInformation($"Uploading image for tweet...");
                var media = Upload.UploadBinary(file1);
                log.LogInformation($"Image upload complete!");

                log.LogInformation($"Publishing tweet...");
                var tweetResponse = Tweetinvi.Tweet.PublishTweet(tweet.TweetText, new PublishTweetOptionalParameters
                {
                    Medias = new List<Tweetinvi.Models.IMedia> { media }
                });
                if (tweetResponse == null)
                {
                    log.LogError($"Tweet was unable to be published");
                    return false;
                }
                else
                {
                    log.LogInformation($"Tweet published! TweetId: {tweetResponse.Id}");
                    return true;
                }
            }
            return false;
        }

        private static IEnumerable<Tuple<Member, Member>> GetMemberVotes(ILogger log, IEnumerable<Member> members, Dictionary<string, string> statesDict)
        {
            var memberVotes = new List<Tuple<Member, Member>>();
            var numMembers = members.Count();
            if (numMembers == 100)
            {
                members = members.Select(c => { c.State = statesDict[c.State]; return c; });

                var sortedMembers = members.OrderBy(o => o.State).ToList();
                var count = sortedMembers.Count;
                for (int i = 0; i < count; i += 2)
                {
                    var member1 = sortedMembers[i];
                    var member2 = sortedMembers[i + 1];
                    memberVotes.Add(Tuple.Create(member1, member2));
                }
            }
            log.LogInformation($"{numMembers} voting members for tweet.");
            return memberVotes;
        }

        private static Rgba32 GetPartyColor(string party)
        {
            switch (party.ToLower())
            {
                case "d":
                    return Rgba32.Blue;
                case "r":
                    return Rgba32.Red;
                case "id":
                    return Rgba32.Green;
                default:
                    return Rgba32.Black;
            }
        }

        private static void DrawMemberPositionOnCanvas(Image<Rgba32> img, Font boldFont, Font regularFont, Member member, int horizontalPosition1, int horizontalPosition2, int verticalPosition)
        {
            var memberPosition = member.VotePosition.ToLower();
            var color = GetPartyColor(member.Party);
            img.Mutate(ctx => ctx.DrawText($"{member.Name}  ({member.Party})", regularFont, color, new PointF(horizontalPosition1, verticalPosition)));
            img.Mutate(ctx => ctx.DrawText($"{member.VotePosition}", boldFont, Rgba32.Black, new PointF(horizontalPosition2, verticalPosition)));
        }

        private static string CreateImage(ILogger log, Models.Tweet tweet)
        {
            log.LogInformation($"Creating image for tweet...");

            // TODO: Move these constants to a constants file
            var DEFAULT_TWEET_IMAGE_WIDTH = 1230;
            var DEFAULT_TWEET_IMAGE_HEIGHT = 1625;
            var DEFAULT_TWEET_IMAGE_FONT_SIZE = 25;
            var DEFAULT_LINE_HEIGHT = 30;
            var ESTIMATED_NUMBER_OF_CHARS_PER_LINE = 110;
            var MEMBER1_NAME_START_X = 225;
            var MEMBER1_VOTE_START_X = 575;
            var MEMBER2_NAME_START_X = 725;
            var MEMBER2_VOTE_START_X = 1075;
            var VOTE_RESULTS_START_X = 425;
            var FINAL_RESULT_START_X = 543;
            var imageWidth = DEFAULT_TWEET_IMAGE_WIDTH;
            var imageHeight = DEFAULT_TWEET_IMAGE_HEIGHT;
            var regularFont = SystemFonts.CreateFont("Arial", DEFAULT_TWEET_IMAGE_FONT_SIZE, FontStyle.Regular);
            var boldFont = SystemFonts.CreateFont("Arial", DEFAULT_TWEET_IMAGE_FONT_SIZE, FontStyle.Bold);

            var numberOfLines = 0;
            if (!string.IsNullOrEmpty(tweet.BillSummary))
            {
                // If there is a bill summary, make additional room for the "Bill Summary:" header, the Bill Summary
                // text, and a trailing new line on the image canvas
                numberOfLines = tweet.BillSummary.Length / ESTIMATED_NUMBER_OF_CHARS_PER_LINE;
                imageHeight += numberOfLines * DEFAULT_LINE_HEIGHT + (DEFAULT_LINE_HEIGHT * 2);
            }
            else if (!string.IsNullOrEmpty(tweet.VoteDescription))
            {
                // If there is a vote description, make additional room for the "Vote Description:" header, the Vote Description
                // text, and a trailing new line on the image canvas
                numberOfLines = tweet.VoteDescription.Length / ESTIMATED_NUMBER_OF_CHARS_PER_LINE;
                imageHeight += numberOfLines * DEFAULT_LINE_HEIGHT + (DEFAULT_LINE_HEIGHT * 2);
            }

            log.LogInformation($"Image width: {imageWidth} Image height: {imageHeight}");
            using (Image<Rgba32> img = new Image<Rgba32>(imageWidth, imageHeight))
            {
                // Set the background color of the image to white
                img.Mutate(ctx => ctx.Fill(Rgba32.White));

                // The height to draw on the canvas
                var y = 5;

                // The width to draw on the canvas
                var x = 5;

                // Populate image with header text
                if (!string.IsNullOrEmpty(tweet.BillSummary))
                {
                    img.Mutate(ctx => ctx.DrawText($"Bill Summary:", boldFont, Rgba32.Black, new PointF(x, y)));
                    var textGraphicOptions = new TextGraphicsOptions(true)
                    {
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        WrapTextWidth = imageWidth
                    };
                    y += DEFAULT_LINE_HEIGHT;
                    img.Mutate(ctx => ctx.DrawText(textGraphicOptions, tweet.BillSummary, regularFont, Rgba32.Black, new PointF(x, y)));
                    y += DEFAULT_LINE_HEIGHT * numberOfLines + DEFAULT_LINE_HEIGHT;
                }
                else if (!string.IsNullOrEmpty(tweet.VoteDescription))
                {
                    img.Mutate(ctx => ctx.DrawText($"Vote Description:", boldFont, Rgba32.Black, new PointF(x, y)));
                    var textGraphicOptions = new TextGraphicsOptions(true)
                    {
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        WrapTextWidth = imageWidth
                    };
                    y += DEFAULT_LINE_HEIGHT;
                    img.Mutate(ctx => ctx.DrawText(textGraphicOptions, tweet.VoteDescription, regularFont, Rgba32.Black, new PointF(x, y)));
                    y += DEFAULT_LINE_HEIGHT * numberOfLines + DEFAULT_LINE_HEIGHT;
                }

                // Populate canvas with senate vote positions
                var totalYes = 0;
                var totalNo = 0;
                var totalNotVoting = 0;
                foreach (var memberVote in tweet.VotingMembers)
                {
                    var member1 = memberVote.Item1;
                    var member2 = memberVote.Item2;
                    img.Mutate(ctx => ctx.DrawText($"{member1.State}:", boldFont, Rgba32.Black, new PointF(x, y)));

                    var member1Position = member1.VotePosition.ToLower();
                    // NOTE: _1 variable is unused. Used here so terinary operand can be used to reduce code size
                    var _1 = member1Position == "yes" ? totalYes += 1 : member1Position == "no" ? totalNo += 1 : totalNotVoting += 1;

                    var member2Position = member2.VotePosition.ToLower();
                    // NOTE: _2 variable is unused. Used here so terinary operand can be used to reduce code size
                    var _2 = member2Position == "yes" ? totalYes += 1 : member2Position == "no" ? totalNo += 1 : totalNotVoting += 1;

                    // Draw Member 1 results on canvas
                    DrawMemberPositionOnCanvas(img, boldFont, regularFont, member1, MEMBER1_NAME_START_X, MEMBER1_VOTE_START_X, y);

                    // Draw Member 2 results on canvas
                    DrawMemberPositionOnCanvas(img, boldFont, regularFont, member2, MEMBER2_NAME_START_X, MEMBER2_VOTE_START_X, y);

                    y += DEFAULT_LINE_HEIGHT;
                }

                // Populate canvas with vote results
                y += DEFAULT_LINE_HEIGHT;
                img.Mutate(ctx => ctx.DrawText($"Yes: {totalYes}     No: {totalNo}     Not Voting: {totalNotVoting}", boldFont, Rgba32.Black, new PointF(VOTE_RESULTS_START_X, y)));
                y += DEFAULT_LINE_HEIGHT;
                img.Mutate(ctx => ctx.DrawText($"Majority Position Required: {tweet.VoteType}", boldFont, Rgba32.Black, new PointF(VOTE_RESULTS_START_X, y)));
                y += DEFAULT_LINE_HEIGHT;

                var resultColor = Rgba32.Black;
                switch (tweet.ShortResult)
                {
                    case "AGREED":
                    case "CONFIRMED":
                    case "SUCCEEDED":
                    case "PASSED":
                        resultColor = Rgba32.Green;
                        break;
                    case "FAILED":
                    case "NOT SUSTAINED":
                    case "VETO SUSTAINED":
                    case "REJECTED":
                        resultColor = Rgba32.Red;
                        break;
                    default:
                        resultColor = Rgba32.Black;
                        break;
                }

                img.Mutate(ctx => ctx.DrawText($"{tweet.ShortResult}", boldFont, resultColor, new PointF(FINAL_RESULT_START_X, y)));

                // Save as image
                return SaveImage(log, img);
            }
        }

        private static string SaveImage(ILogger log, Image<Rgba32> img)
        {

            var filePath = "D:\\home\\site\\wwwroot\\SenateVotes\\tweet-image.png";
            try
            {
                img.Save(filePath);
            }
            catch (Exception e)
            {
                log.LogError($"Exception when saving image for tweet. File path: {filePath}. Message: {e.Message}");
                return string.Empty;

            }
            log.LogInformation($"Saved image for tweet. File path: {filePath}");
            return filePath;
        }
    }
}
