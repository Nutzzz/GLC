using System;
using System.Web;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Collections.Generic;

namespace PureOrigin.API
{
    public class BaseAPIManager
    {
        static readonly CookieContainer cookieContainer = new CookieContainer();
        static readonly HttpClientHandler clientHandler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            CookieContainer = cookieContainer
        };
        protected static OAuthResponse OAuth;
        protected static HttpClient httpClient = new HttpClient(clientHandler);

        public static HttpRequestMessage CreateRequest(HttpMethod httpMethod, string requestUrl, params KeyValuePair<string, string>[] UrlParameters) => CreateRequest(httpMethod, new Uri(requestUrl), UrlParameters);
        public static HttpRequestMessage CreateRequest(HttpMethod httpMethod, Uri requestUri, params KeyValuePair<string, string>[] UrlParameters)
        {
            var UriBuilder = new UriBuilder(requestUri);
            var QueryBuilder = HttpUtility.ParseQueryString(UriBuilder.Query);
            foreach (var urlParam in UrlParameters)
            {
                QueryBuilder[urlParam.Key] = urlParam.Value;
            }
            UriBuilder.Query = QueryBuilder.ToString();

            var request = new HttpRequestMessage(httpMethod, UriBuilder.Uri);

            if (OAuth != null)
            {
                request.Headers.Add("authtoken", OAuth.AccessToken);
            }

            return request;
        }

        public static async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
        {
            return await httpClient.SendAsync(request);            
        }
    }
}
