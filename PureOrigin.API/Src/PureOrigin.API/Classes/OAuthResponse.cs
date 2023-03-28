using Newtonsoft.Json;

public class OAuthResponse
{
    internal OAuthResponse() { }

    [JsonProperty("access_token")]
    public string AccessToken { get; internal set; }
    [JsonProperty("token_type")]
    public string TokenType { get; internal set; }
    [JsonProperty("expires_in")]
    public int ExpiresIn { get; internal set; }
}
