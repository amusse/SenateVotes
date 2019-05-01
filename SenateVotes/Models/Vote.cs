using Newtonsoft.Json;

namespace SenateVotes.Models
{
    public class Vote
    {
        public int Congress { get; set; }

        public string Chamber { get; set; }

        public int Session { get; set; }

        [JsonProperty("roll_call")]
        public int RollCall { get; set; }

        public string Source { get; set; }

        public string Url { get; set; }

        [JsonProperty("vote_uri")]
        public string VoteUri { get; set; }

        public Bill Bill { get; set; }

        public string Question { get; set; }

        [JsonProperty("question_text")]
        public string QuestionText { get; set; }

        public string Description { get; set; }

        [JsonProperty("vote_type")]
        public string VoteType { get; set; }

        public string Date { get; set; }

        public string Time { get; set; }

        public string Result { get; set; }

        [JsonProperty("tie_breaker")]
        public string TieBreaker { get; set; }

        [JsonProperty("tie_breaker_vote")]
        public string TieBreakerVote { get; set; }

        [JsonProperty("document_number")]
        public string DocumentNumber { get; set; }

        [JsonProperty("document_title")]
        public string DocumentTitle { get; set; }

        public Distribution Democratic { get; set; }

        public Distribution Republican { get; set; }

        public Distribution Independent { get; set; }

        public Distribution Total { get; set; }

    }

    public class Distribution
    {
        public int Yes { get; set; }

        public int No { get; set; }

        public int Present { get; set; }

        [JsonProperty("not_voting")]
        public int NotVoting { get; set; }

        [JsonProperty("majority_position")]
        public string MajorityPosition { get; set; }

    }
}
