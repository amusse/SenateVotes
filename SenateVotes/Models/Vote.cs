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

        private string _result;

        public string Result 
        { 
            get 
            {
                return _result;
            } 
            set 
            {
                _result = value;
                if (value != null)
                {
                    _result = value.ToUpper();
                }
            }
        }

        private string _shortResult;

        public string ShortResult 
        { 
            get
            {
                if (_result != null)
                {
                    var res = _result.ToLower();
                    if (res.Contains("passed"))
                    {
                        _shortResult = "PASSED";
                    }
                    else if (res.Contains("agreed"))
                    {
                        _shortResult = "AGREED";
                    }
                    else if (res.Contains("rejected"))
                    {
                        _shortResult = "REJECTED";
                    }
                    else if (res.Contains("confirmed"))
                    {
                        _shortResult = "CONFIRMED";
                    }
                    else if (res.Contains("not sustained"))
                    {
                        _shortResult = "NOT SUSTAINED";
                    }
                    else if (res.Contains("failed"))
                    {
                        _shortResult = "FAILED";
                    }
                    else if (res.Contains("succeeded"))
                    {
                        _shortResult = "SUCCEEDED";
                    }
                    else if (res.Contains("veto sustained"))
                    {
                        _shortResult = "VETO SUSTAINED";
                    } 
                }
                return _shortResult;
            } 
            set
            {
                _shortResult = value;
            } 
        }

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
