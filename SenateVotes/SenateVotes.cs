using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

namespace SenateVotes
{
    public static class SenateVotes
    {
        [FunctionName("SenateVotes")]
        public static void Run([TimerTrigger("0 0 19 * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"SenateVotes function executed");
        }
    }
}
