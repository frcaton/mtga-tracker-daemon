using Newtonsoft.Json;

namespace MTGATrackerDaemon.Models
{
    public class CardsResponseData
    {
        [JsonProperty("cards")]
        public CardOwnership[] Cards { get; set; }

        [JsonProperty("elapsedTime")]
        public int ElapsedTime { get; set; }
    }

    public class CardOwnership
    {
        [JsonProperty("grpId")]
        public uint GrpId { get; set; }

        [JsonProperty("owned")]
        public int Owned { get; set; }
    }
}
