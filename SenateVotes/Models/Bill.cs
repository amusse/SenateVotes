using Newtonsoft.Json;

namespace SenateVotes.Models
{
    public class Bill
    {
        [JsonProperty("bill_id")]
        public string BillId { get; set; }

        public string Number { get; set; }

        [JsonProperty("sponsor_id")]
        public string SponsorId { get; set; }

        [JsonProperty("api_uri")]
        public string ApiUri { get; set; }

        public string Title { get; set; }

        [JsonProperty("latest_action")]
        public string LatestAction { get; set; }

    }
}
