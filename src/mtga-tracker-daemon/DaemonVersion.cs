using System.Collections.Generic;
using Newtonsoft.Json;

namespace MTGATrackerDaemon
{
    public class DaemonVersion
    {
        [JsonProperty(PropertyName = "tag_name")]
        public string TagName { get; set; }

        [JsonProperty(PropertyName = "assets")]
        public List<Asset> Assets { get; set; }
    }
}