using Newtonsoft.Json;

namespace SenateVotes.Models
{
    public class Member
    {
        [JsonProperty("member_id")]
        public string MemberId { get; set; }

        public string Name { get; set; }

        public string Party { get; set; }

        public string State { get; set; }

        [JsonProperty("vote_position")]
        public string VotePosition { get; set; }

        [JsonProperty("dw_nominate")]
        public string DwNominate { get; set; }


    }
}
