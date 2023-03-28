using System;

using Newtonsoft.Json;

namespace PureOrigin.API.Classes.User
{
    public class OriginInternalUser
    {
        internal OriginInternalUser() { }

        [JsonProperty("pidId")]
        public ulong UserId { get; internal set; }
        [JsonProperty]
        public string Email { get; internal set; }
        [JsonProperty]
        public string DOB { get; internal set; }
        [JsonProperty]
        public string Country { get; internal set; }
        [JsonProperty]
        public string Language { get; internal set; }
        [JsonProperty]
        public string Locale { get; internal set; }
        //[JsonProperty]
        //public DateTime DateCreated { get; internal set; }
        //[JsonProperty]
        //public DateTime DateModified { get; internal set; }
        //[JsonProperty]
        //public DateTime LastAuthDate { get; internal set; }
    }
}
