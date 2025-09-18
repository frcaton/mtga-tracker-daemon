using Newtonsoft.Json;

namespace MTGATrackerDaemon.Models
{
    public class EventsResponseData
    {
        [JsonProperty("events")]
        public string[] Events { get; set; }

        [JsonProperty("elapsedTime")]
        public int ElapsedTime { get; set; }
    }
}
