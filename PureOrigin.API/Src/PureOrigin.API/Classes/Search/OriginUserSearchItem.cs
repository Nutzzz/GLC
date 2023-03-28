using Newtonsoft.Json;

namespace PureOrigin.API.Classes.Search
{
    internal class OriginUserSearchItem
    {
        [JsonProperty("friendUserId")]
        public ulong UserId { get; internal set; }
    }
}
