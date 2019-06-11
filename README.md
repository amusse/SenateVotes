# SenateVotes ([@usasenatevotes](https://twitter.com/usasenatevotes))
SenateVotes is a Twitter application that posts daily automated tweets of votes in the United States Senate. Follow [@usasenatevotes](https://twitter.com/usasenatevotes) to get updates.

## Motivation
In 2016, **61.4%** of the citizen voting-age population reported voting in the Presidential Election, according to the [Census Bureau](https://www.census.gov/newsroom/blogs/random-samplings/2017/05/voting_in_america.html). Voter turnout in the last midterm election (2018) was about **53.4% of the voting-age U.S. population**, according to the [Census Bureau](https://www.census.gov/library/stories/2019/04/behind-2018-united-states-midterm-election-turnout.html). Although the midterm election turnout was record-breaking, it is not enough. What this means is that the decision that approximately **half** of the American population made is **impacting 100% of the people in the United States** (we all have to follow the laws). A common complaint by American citizens is that they do not understand or know enough about the government or politics to make a decision on who to vote for. The motivation behind this project is to make what goes on in congress simple to understand for the common citizen on a commonly used platform like [Twitter](https://twitter.com/). 

## How does it work?

The application runs on top of [Azure Functions](https://azure.microsoft.com/en-us/services/functions/). 

The Function makes requests to the [ProPublica Congress API](https://projects.propublica.org/api-docs/congress-api/) on a daily basis to get votes for the given day. The function then combines the vote summary along with each Senator's vote position and creates an image using the [SixLabors](https://github.com/SixLabors) library. The image and tweet body are then uploaded to twitter using the [Tweetinvi](https://github.com/linvi/tweetinvi/wiki) library.

## Some improvements that could be made

Right now, I am creating a simple image to visualize the votes. Though, perhaps this is not the best way to display the votes? Maybe people would engage more if the vote data was overlayed on a map of the United States? Instead of having an image, maybe represent the vote results as replies in the tweet thread, so people can like or retweet specific senator positions?
