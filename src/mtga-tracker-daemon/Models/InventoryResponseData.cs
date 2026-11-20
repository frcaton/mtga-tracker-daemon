using System.Collections.Generic;
using Newtonsoft.Json;

namespace MTGATrackerDaemon.Models
{
    public class InventoryResponseData
    {
        [JsonProperty("gems")]
        public int Gems { get; set; }

        [JsonProperty("gold")]
        public int Gold { get; set; }

        // every plain number field of the inventory object (wildcards, vault progress, ...)
        [JsonProperty("fields")]
        public Dictionary<string, object> Fields { get; set; }

        [JsonProperty("elapsedTime")]
        public int ElapsedTime { get; set; }
    }
}
