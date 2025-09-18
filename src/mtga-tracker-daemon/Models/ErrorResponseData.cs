using Newtonsoft.Json;

namespace MTGATrackerDaemon.Models
{
    public class ErrorResponseData
    {
        [JsonProperty("error")]
        public string Error { get; set; }
    }
}
