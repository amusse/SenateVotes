using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;

namespace SenateVotes
{
    public static class SenateVotes
    {
        [FunctionName("SenateVotes")]
        public static void Run([TimerTrigger("0 0 23 * * 1-5")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }
    }
}
