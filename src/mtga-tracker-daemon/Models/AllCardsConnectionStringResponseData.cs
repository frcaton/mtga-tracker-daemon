using Newtonsoft.Json;

namespace MTGATrackerDaemon.Models
{
    public class AllCardsConnectionStringResponseData
    {
        [JsonProperty("connectionString")]
        public string ConnectionString { get; set; }

        [JsonProperty("elapsedTime")]
        public int ElapsedTime { get; set; }
    }
}
