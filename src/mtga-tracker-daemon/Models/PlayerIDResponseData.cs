using Newtonsoft.Json;

namespace MTGATrackerDaemon.Models
{
    public class PlayerIDResponseData
    {
        [JsonProperty("playerId")]
        public string PlayerID { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("personaId")]
        public string PersonaID { get; set; }

        [JsonProperty("elapsedTime")]
        public int ElapsedTime { get; set; }
    }
}
