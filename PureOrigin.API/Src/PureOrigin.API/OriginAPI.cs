using System;
using System.Web;
using System.Net;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using PureOrigin.API.Extensions;
using PureOrigin.API.Classes.User;
using PureOrigin.API.Classes.Search;

namespace PureOrigin.API
{
    public class OriginAPI : BaseAPIManager
    {
        private string fId = "";
        private string jSessionId = "";
        private string sId = "";
        private string code = "";

        public OriginInternalUser InternalUser;

        private readonly string Email, Password;
        public OriginAPI(string email, string password)
        {
            Email = email;
            Password = password;
        }

        public virtual async Task<bool> LoginAsync()
        {
            var location = await CreateSessionFId();
            if (location == null || !location.IsWellFormedOriginalString())
            {
                return false;
            }

            location = await CreateJSessionId(location);
            if (location == null || !location.IsWellFormedOriginalString())
            {
                return false;
            }

            await CreateAuthLogin(location);
            location = await AuthoriseLogin(location);
            if (location == null || !location.IsWellFormedOriginalString())
            {
                return false;
            }

            location = await CreateSId(location);
            if (location == null || !location.IsWellFormedOriginalString())
            {
                return false;
            }

            OAuth = await GetAccessToken();
            if (OAuth == null)
            {
                return false;
            }

            InternalUser = await GetInternalUser();
            if (InternalUser == null)
            {
                return false;
            }

            return true;
        }

        public virtual async Task<bool> LogoutAsync()
        {
            var request = CreateRequest(HttpMethod.Get, OriginURLs.LOGOUT_URL, new KeyValuePair<string, string>("access_token", OAuth.AccessToken));
            var response = await SendAsync(request);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                return true;
            }
            return false;
        }

        protected async Task<Uri> CreateSessionFId()
        {
            var request = CreateRequest(HttpMethod.Get, OriginURLs.FID_URL);
            var response = await SendAsync(request);
            if (response.StatusCode == HttpStatusCode.Redirect)
            {
                fId = HttpUtility.ParseQueryString(response.Headers.Location.Query).Get("fid");
                return response.Headers.Location;
            }
            return null;
        }

        protected async Task<Uri> CreateJSessionId(Uri url)
        {
            var request = CreateRequest(HttpMethod.Get, url);
            var response = await SendAsync(request);
            if (response.StatusCode == HttpStatusCode.Redirect)
            {
                var result = response.Headers.TryGetValues("Set-Cookie", out var cookies);
                if (result)
                {
                    jSessionId = Regex.Split(cookies.ElementAt(0), @"\=(.*?)\;")[1];
                }
                return new Uri($"https://signin.ea.com{response.Headers.Location}");
            }
            return null;
        }

        protected async Task CreateAuthLogin(Uri url)
        {
            var request = CreateRequest(HttpMethod.Get, url);
            request.Headers.Add("Cookie", $"JSESSIONID={jSessionId}");
            var response = await SendAsync(request);
            if (response.StatusCode == HttpStatusCode.Redirect)
            {
                var result = response.Headers.TryGetValues("Set-Cookie", out var cookies);
                if (result)
                {
                    jSessionId = Regex.Split(cookies.ElementAt(0), @"\=(.*?)\;")[1];
                }
            }
        }

        protected async Task<Uri> AuthoriseLogin(Uri url)
        {
            var pairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("email", Email),
                new KeyValuePair<string, string>("password", Password),
                new KeyValuePair<string, string>("_eventId", "submit"),
                new KeyValuePair<string, string>("cid", GeneralExtensions.RandomString(32)),
                new KeyValuePair<string, string>("showAgeUp", "true"),
                new KeyValuePair<string, string>("googleCaptchaResponse", ""),
                new KeyValuePair<string, string>("_rememberMe", "on"),
            };
            var content = new FormUrlEncodedContent(pairs);

            var request = CreateRequest(HttpMethod.Post, url);
            request.Headers.Add("Cookie", $"JSESSIONID={jSessionId}");
            request.Content = content;
            var response = await SendAsync(request);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var html = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(html))
                {
                    var regex = Regex.Match(html, @"(?<=window.location = \"")\S+(?=\"";)");
                    if (regex.Success)
                    {
                        return new Uri(regex.Value);
                    }
                }
            }
            return null;
        }

        protected async Task<Uri> CreateSId(Uri url)
        {
            var request = CreateRequest(HttpMethod.Get, url);
            var response = await SendAsync(request);
            if (response.StatusCode == HttpStatusCode.Redirect)
            {
                var result = response.Headers.TryGetValues("Set-Cookie", out var cookies);
                if (result)
                {
                    var regex = Regex.Match(cookies.ElementAt(0), @"(?<=sid=)[\S]+?(?=;)");
                    if (regex.Success)
                    {
                        sId = regex.Value;
                    }
                }
                code = HttpUtility.ParseQueryString(response.Headers.Location.Query).Get("code");
                return response.Headers.Location;
            }
            return null;
        }

        protected async Task<OAuthResponse> GetAccessToken()
        {
            var request = CreateRequest(HttpMethod.Get, OriginURLs.LOGIN_URL);
            request.Headers.Add("Cookie", $"sid={sId}");
            var response = await SendAsync(request);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var json = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(json))
                {
                    return JsonConvert.DeserializeObject<OAuthResponse>(json);
                }
            }
            return null;
        }

        protected async Task<OriginInternalUser> GetInternalUser()
        {
            var request = CreateRequest(HttpMethod.Get, OriginURLs.USER_IDENTITY_LOOKUP);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", OAuth.AccessToken);
            var response = await SendAsync(request);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var json = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(json))
                {
                    return JObject.Parse(json)["pid"].ToObject<OriginInternalUser>();
                }
            }
            return null;
        }

        public virtual async Task<OriginUser> GetUserAsync(ulong userId) => await LookupUserAsync(userId);
        public virtual async Task<OriginUser> GetUserAsync(string username, bool explicitUsername = true)
        {
            var users = await GetUsersAsync(username);
            if (users != null && users.Count() > 0)
            {
                if (explicitUsername)
                {
                    return users.Single(x => string.Equals(x.Username, username, StringComparison.OrdinalIgnoreCase));
                }
                else
                {
                    return users.ElementAt(0);
                }
            }
            return null;
        }

        public const int MAX_USER_SEARCH = 5;
        public virtual async Task<IEnumerable<OriginUser>> GetUsersAsync(string username, int count = MAX_USER_SEARCH)
        {
            var request = CreateRequest(HttpMethod.Get, OriginURLs.ORIGIN_USER_SEARCH, new KeyValuePair<string, string>("userId", InternalUser.UserId.ToString()), new KeyValuePair<string, string>("searchTerm", username));
            var response = await SendAsync(request);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var json = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(json))
                {
                    var searchResults = JsonConvert.DeserializeObject<OriginUserSearch>(json);
                    if (searchResults.UserCount > 0)
                    {
                        var userList = searchResults.UserList.Select(x => x.UserId).Take(count.Clamp(1, MAX_USER_SEARCH));
                        if (userList.Count() > 0)
                        {
                            return await LookupUsersAsync(userList);
                        }
                    }
                }
            }
            return null;
        }

        protected virtual async Task<OriginUser> LookupUserAsync(ulong UserId)
        {
            var users = await LookupUsersAsync(UserId);
            if (users.Count() > 0)
            {
                return users.ElementAt(0);
            }
            return null;
        }

        protected virtual async Task<IEnumerable<OriginUser>> LookupUsersAsync(params ulong[] UserIds) => await LookupUsersAsync(UserIds.AsEnumerable());
        protected virtual async Task<IEnumerable<OriginUser>> LookupUsersAsync(IEnumerable<ulong> UserIds)
        {
            var request = CreateRequest(HttpMethod.Get, OriginURLs.ORIGIN_USER_ID_SEARCH, new KeyValuePair<string, string>("userIds", string.Join(",", UserIds)));
            var response = await SendAsync(request);
            if (response.StatusCode == HttpStatusCode.OK)
            {
                var xml = await response.Content.ReadAsStringAsync();
                if (!string.IsNullOrWhiteSpace(xml))
                {
                    var lookupResults = XMLSerializerExtensions.XmlDeserializeFromString<OriginUserLookup>(xml);
                    if (lookupResults != null)
                    {
                        return lookupResults.Users;
                    }
                }
            }
            return null;
        }
    }
}
