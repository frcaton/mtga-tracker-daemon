using Newtonsoft.Json;

namespace MTGATrackerDaemon.Models
{
    public class MatchStateResponseData
    {
        [JsonProperty("matchId")]
        public string MatchId { get; set; }

        [JsonProperty("playerRank")]
        public PlayerRank PlayerRank { get; set; }

        [JsonProperty("opponentRank")]
        public PlayerRank OpponentRank { get; set; }

        [JsonProperty("elapsedTime")]
        public int ElapsedTime { get; set; }
    }

    public class PlayerRank
    {
        [JsonProperty("mythicPercentile")]
        public float MythicPercentile { get; set; }

        [JsonProperty("mythicPlacement")]
        public int MythicPlacement { get; set; }

        [JsonProperty("class")]
        public int Class { get; set; }

        [JsonProperty("tier")]
        public int Tier { get; set; }
    }
}
