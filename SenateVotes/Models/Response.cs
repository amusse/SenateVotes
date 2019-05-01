using System;
namespace SenateVotes.Models
{
    public class Response
    {
        public Response()
        {
        }

        public string Status { get; set; }

        public string Copyright { get; set; }

        public Results Results { get; set; }
    }
}
