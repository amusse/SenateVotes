using System;
using System.Collections.Generic;

namespace SenateVotes.Models
{
    public class Tweet
    {
        public Tweet()
        {
        }

        public string VoteName { get; set; }

        public string VoteDescription { get; set; }

        public string Result { get; set; }

        public IEnumerable<Tuple<Member, Member>> VotingMembers { get; set; }

        public IEnumerable<Member> MemberPositions { get; set; }

        public string VoteUrl { get; set; }

        public string TweetText { get; set; }

        public string BillSummary { get; set; }

        public string VoteType { get; set; }

        public string VoteDate { get; set; }

        public string ShortResult { get; set; }
    }
}
