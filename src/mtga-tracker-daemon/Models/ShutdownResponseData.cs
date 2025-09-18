using Newtonsoft.Json;

namespace MTGATrackerDaemon.Models
{
    public class ShutdownResponseData
    {
        [JsonProperty("result")]
        public string Result { get; set; }
    }
}
