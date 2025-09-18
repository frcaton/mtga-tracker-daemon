using Newtonsoft.Json;

namespace MTGATrackerDaemon.Models
{
    public class InventoryResponseData
    {
        [JsonProperty("gems")]
        public int Gems { get; set; }

        [JsonProperty("gold")]
        public int Gold { get; set; }

        [JsonProperty("elapsedTime")]
        public int ElapsedTime { get; set; }
    }
}
