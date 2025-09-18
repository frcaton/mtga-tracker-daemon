using Newtonsoft.Json;

namespace MTGATrackerDaemon.Models
{
    public class StatusResponseData
    {
        [JsonProperty("isRunning")]
        public bool IsRunning { get; set; }

        [JsonProperty("daemonVersion")]
        public string DaemonVersion { get; set; }

        [JsonProperty("updating")]
        public bool Updating { get; set; }

        [JsonProperty("processId")]
        public int ProcessId { get; set; }
    }
}
