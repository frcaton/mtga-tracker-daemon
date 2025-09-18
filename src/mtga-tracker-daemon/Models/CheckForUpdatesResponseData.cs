using Newtonsoft.Json;

namespace MTGATrackerDaemon.Models
{
    public class CheckForUpdatesResponseData
    {
        [JsonProperty("updatesAvailable")]
        public bool UpdatesAvailable;
    }
}