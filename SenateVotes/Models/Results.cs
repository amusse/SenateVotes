using System;
using System.Collections.Generic;

namespace SenateVotes.Models
{
    public class Results
    {
        public Results()
        {
        }

        public string Chamber { get; set; }

        public string Year { get; set; }

        public string Month { get; set; }

        public IEnumerable<Vote> Votes { get; set; }
        
    }
}
