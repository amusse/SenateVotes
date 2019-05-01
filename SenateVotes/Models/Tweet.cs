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

        public Dictionary<string, Member> VotingMembers { get; set; }

        public string VoteUrl { get; set; }

    }
}
