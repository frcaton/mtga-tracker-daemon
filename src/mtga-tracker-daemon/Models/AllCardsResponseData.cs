using Newtonsoft.Json;

namespace MTGATrackerDaemon.Models
{
    public class AllCardsResponseData
    {
        [JsonProperty("cards")]
        public CardInfo[] Cards { get; set; }

        [JsonProperty("elapsedTime")]
        public int ElapsedTime { get; set; }
    }

    public class CardInfo
    {
        [JsonProperty("grpId")]
        public int GrpId { get; set; }

        [JsonProperty("title")]
        public string Title { get; set; }

        [JsonProperty("expansionCode")]
        public string ExpansionCode { get; set; }
    }
}
