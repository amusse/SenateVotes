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
using SixLabors.Shapes;
using System.Threading;

namespace SenateVotes
{
    public static class SenateVotes
    {
        [FunctionName("SenateVotes")]
        public static async Task Run([TimerTrigger("0 0 20 * * *")]TimerInfo myTimer, ILogger log)
        {
            TwitterHelpers.AuthenticateTwitter(log);
            await GetVotes(log);
        }

        public static async Task GetVotes(ILogger log)
        {
            // Create a New HttpClient object and dispose it when done, so the app doesn't leak resources
            using (HttpClient client = new HttpClient() { BaseAddress = new Uri(HelperMethods.GetEnvironmentVariable("PRO_PUBLICA_BASE_URL")) })
            {
                try
                {
                    client.DefaultRequestHeaders.Add("X-API-KEY", HelperMethods.GetEnvironmentVariable("PRO_PUBLICA_API_KEY"));
                    await GetVotesForDate(log, client, DateTime.Now);

                }
                catch (HttpRequestException e)
                {
                    log.LogError($"Exception when making request to API. Message: {e.Message}");
                }
            }
        }

        public static async Task GetVotesForDate(ILogger log, HttpClient client, DateTime currentDate)
        {
            var uri = $"/congress/v1/senate/votes/{currentDate.Year}/{currentDate.Month}.json";
            var responseBody = await client.GetStringAsync(uri);
            Response res = null;
            try
            {
                res = JsonConvert.DeserializeObject<Response>(responseBody);
            }
            catch (Exception e)
            {
                log.LogError($"Exception when deserializing response body. Message: {e.Message}");
                return;
            }
            IEnumerable<Vote> votes = null;
            if (res != null && res.Results != null && res.Results.Votes != null)
            {
                votes = res.Results.Votes;
                log.LogInformation($"Retrieved {votes.Count()} total votes for date: {currentDate}");
            }
            else
            {
                log.LogInformation($"No votes found in response body for date: {currentDate}. Response: {res}");
                return;
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
            votes = votes.OrderBy(o => o.RollCall).ToList();

            var tweets = new List<Models.Tweet>();
            var statesDict = HelperMethods.BuildStateDictionary();

            foreach (var vote in votes)
            {
                var voteUri = vote.VoteUri;
                uri = voteUri.Replace(HelperMethods.GetEnvironmentVariable("PRO_PUBLICA_BASE_URL"), "");
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
                    return;
                }

                var memberPositions = positionsArray.ToObject<IEnumerable<Member>>();

                var newTweet = new Models.Tweet
                {
                    VoteDescription = vote.Description,
                    Result = vote.Result,
                    ShortResult = vote.ShortResult,
                    MemberPositions = memberPositions,
                    TweetText = CreateTweetText(log, vote, memberPositions),
                    VoteUrl = vote.Url,
                    VoteType = vote.VoteType,
                    VoteDate = vote.Date
                };

                if (!string.IsNullOrEmpty(vote.Bill.ApiUri))
                {
                    var billUri = vote.Bill.ApiUri;
                    uri = billUri.Replace(HelperMethods.GetEnvironmentVariable("PRO_PUBLICA_BASE_URL"), "");
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
            var tweetIndex = 0;
            var publishedTweets = 0;
            if (tweets.Count > 0)
            {
                foreach (var tweet in tweets)
                {
                    log.LogInformation($"TweetIndex: {tweetIndex}, VoteUrl: {tweet.VoteUrl}, TweetText: {tweet.TweetText}");
                    var published = SendTweet(log, tweet, statesDict);
                    Thread.Sleep(2000);
                    if (published == true)
                    {
                        publishedTweets++;
                    }
                    tweetIndex++;
                }
            }

            log.LogInformation($"Number of tweets published: {publishedTweets}");
        }

        public static string CreateTweetText(ILogger log, Vote vote, IEnumerable<Member> memberPositions)
        {
            // Format will look like this
            // {PASSED}{AGREED}{REJECTED}{CONFIRMED} (date) (sponsor name, state, party) : {document_title} {url}
            var tweetText = "";
            if (vote.Bill != null && !string.IsNullOrEmpty(vote.Bill.SponsorId))
            {
                var description = "";
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

            tweetText = HelperMethods.TruncateString(tweetText, 165);
            tweetText = $"{tweetText} {vote.Url}";

            if (string.IsNullOrEmpty(tweetText))
            {
                log.LogError("Tweet text is empty");
            }
            return tweetText;
        }

        public static bool SendTweet(ILogger log, Models.Tweet tweet, Dictionary<string, string> statesDict)
        {
            tweet.VotingMembers = GetMemberVotes(tweet.MemberPositions.ToList(), statesDict);
            log.LogInformation($"{tweet.VotingMembers.Count()} voting members for tweet.");

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
                    log.LogInformation($"Tweet published! TweeetId: {tweetResponse.Id}");
                    return true;
                }
            }
            return false;
        }

        public static IEnumerable<Tuple<Member, Member>> GetMemberVotes(IEnumerable<Member> members, Dictionary<string, string> statesDict)
        {
            var memberVotes = new List<Tuple<Member, Member>>();

            if (members.Count() == 100)
            {
                members = members.Select(c => { c.State = statesDict[c.State]; return c; }).ToList();

                var sortedMembers = members.OrderBy(o => o.State).ToList();
                var count = sortedMembers.Count();
                for (int i = 0; i < count; i += 2)
                {
                    var member1 = sortedMembers[i];
                    var member2 = sortedMembers[i + 1];

                    var replyText = $"{member1.State}: {member1.Name} ({member1.Party}) [{member1.VotePosition}], {member2.Name} ({member2.Party}) [{member2.VotePosition}]";
                    memberVotes.Add(Tuple.Create(member1, member2));
                }
            }
            return memberVotes;
        }

        public static string CreateImage(ILogger log, Models.Tweet tweet)
        {
            log.LogInformation($"Creating image for tweet...");
            var imageWidth = 1230;
            var imageHeight = 1625;
            var regularFont = SystemFonts.CreateFont("Arial", 25, FontStyle.Regular);
            var boldFont = SystemFonts.CreateFont("Arial", 25, FontStyle.Bold);

            var numberOfLines = 0;
            if (!string.IsNullOrEmpty(tweet.BillSummary))
            {
                imageHeight += 30;
                numberOfLines = tweet.BillSummary.Length / 110;
                imageHeight += numberOfLines * 30;
                imageHeight += 30;
            }
            else if (!string.IsNullOrEmpty(tweet.VoteDescription))
            {
                imageHeight += 30;
                numberOfLines = tweet.VoteDescription.Length / 110;
                imageHeight += numberOfLines * 30;
                imageHeight += 30;
            }
            log.LogInformation($"Image width: {imageWidth} Image height: {imageHeight}");
            using (Image<Rgba32> img = new Image<Rgba32>(imageWidth, imageHeight))
            {
                img.Mutate(ctx => ctx.Fill(Rgba32.White));

                var verticalOffset = 5;
                var totalYes = 0;
                var totalNo = 0;
                var totalNotVoting = 0;

                if (!string.IsNullOrEmpty(tweet.BillSummary))
                {
                    img.Mutate(ctx => ctx.DrawText($"Bill Summary:", boldFont, Rgba32.Black, new PointF(5, verticalOffset)));
                    verticalOffset += 30;
                    var textGraphicOptions = new TextGraphicsOptions(true)
                    {
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        WrapTextWidth = imageWidth
                    };
                    img.Mutate(ctx => ctx.DrawText(textGraphicOptions, tweet.BillSummary, regularFont, Rgba32.Black, new PointF(5, verticalOffset)));
                    verticalOffset += 30 * numberOfLines;
                    verticalOffset += 30;
                }
                else if (!string.IsNullOrEmpty(tweet.VoteDescription))
                {
                    img.Mutate(ctx => ctx.DrawText($"Vote Description:", boldFont, Rgba32.Black, new PointF(5, verticalOffset)));
                    verticalOffset += 30;
                    var textGraphicOptions = new TextGraphicsOptions(true)
                    {
                        HorizontalAlignment = HorizontalAlignment.Left,
                        VerticalAlignment = VerticalAlignment.Top,
                        WrapTextWidth = imageWidth
                    };
                    img.Mutate(ctx => ctx.DrawText(textGraphicOptions, tweet.VoteDescription, regularFont, Rgba32.Black, new PointF(5, verticalOffset)));
                    verticalOffset += 30 * numberOfLines;
                    verticalOffset += 30;
                }
                foreach (var memberVote in tweet.VotingMembers)
                {
                    if (memberVote.Item1.VotePosition.ToLower() == "yes")
                    {
                        totalYes += 1;
                    }
                    else if (memberVote.Item1.VotePosition.ToLower() == "no")
                    {
                        totalNo += 1;
                    }
                    else
                    {
                        totalNotVoting += 1;
                    }

                    if (memberVote.Item2.VotePosition.ToLower() == "yes")
                    {
                        totalYes += 1;
                    }
                    else if (memberVote.Item2.VotePosition.ToLower() == "no")
                    {
                        totalNo += 1;
                    }
                    else
                    {
                        totalNotVoting += 1;
                    }

                    var verticalPosition = verticalOffset;
                    var horizontalPosition = 5;
                    img.Mutate(ctx => ctx.DrawText($"{memberVote.Item1.State}:", boldFont, Rgba32.Black, new PointF(horizontalPosition, verticalPosition)));

                    horizontalPosition = 225;
                    var color = Rgba32.Black;
                    switch (memberVote.Item1.Party.ToLower())
                    {
                        case "d":
                            color = Rgba32.Blue;
                            break;
                        case "r":
                            color = Rgba32.Red;
                            break;
                        case "id":
                            color = Rgba32.Green;
                            break;
                        default:
                            color = Rgba32.Black;
                            break;
                    }
                    img.Mutate(ctx => ctx.DrawText($"{memberVote.Item1.Name}  ({memberVote.Item1.Party})", regularFont, color, new PointF(horizontalPosition, verticalPosition)));

                    horizontalPosition = 575;
                    img.Mutate(ctx => ctx.DrawText($"{memberVote.Item1.VotePosition}", boldFont, Rgba32.Black, new PointF(horizontalPosition, verticalPosition)));
                    switch (memberVote.Item2.Party.ToLower())
                    {
                        case "d":
                            color = Rgba32.Blue;
                            break;
                        case "r":
                            color = Rgba32.Red;
                            break;
                        case "id":
                            color = Rgba32.Green;
                            break;
                        default:
                            color = Rgba32.Black;
                            break;
                    }
                    horizontalPosition = 725;
                    img.Mutate(ctx => ctx.DrawText($"{memberVote.Item2.Name}  ({memberVote.Item2.Party})", regularFont, color, new PointF(horizontalPosition, verticalPosition)));

                    horizontalPosition = 1075;
                    img.Mutate(ctx => ctx.DrawText($"{memberVote.Item2.VotePosition}", boldFont, Rgba32.Black, new PointF(horizontalPosition, verticalPosition)));

                    verticalOffset += 30;
                }

                img.Mutate(ctx => ctx.DrawText($"Yes: {totalYes}     No: {totalNo}     Not Voting: {totalNotVoting}", boldFont, Rgba32.Black, new PointF(425, verticalOffset + 30)));
                verticalOffset += 30;
                img.Mutate(ctx => ctx.DrawText($"Majority Position Required: {tweet.VoteType}", boldFont, Rgba32.Black, new PointF(425, verticalOffset + 30)));
                verticalOffset += 30;
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
                img.Mutate(ctx => ctx.DrawText($"{tweet.ShortResult}", boldFont, resultColor, new PointF(543, verticalOffset + 30)));

                var filePath = "./tweet-image.png";
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
}
