using System.Collections.Generic;
using Newtonsoft.Json;

namespace MTGATrackerDaemon
{
    public class Asset
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        [JsonProperty(PropertyName = "browser_download_url")]
        public string BrowserDownloadUrl { get; set; }
    }
}