namespace PureOrigin.API
{
    public static class OriginURLs
    {
        public const string FID_URL = "https://accounts.ea.com/connect/auth?response_type=code&client_id=ORIGIN_SPA_ID&display=originXWeb/login&locale=en_US&release_type=prod&redirect_uri=https://www.origin.com/views/login.html";
        public const string LOGIN_URL = "https://accounts.ea.com/connect/auth?client_id=ORIGIN_JS_SDK&response_type=token&redirect_uri=nucleus:rest&prompt=none&release_type=prod";
        public const string LOGOUT_URL = "https://accounts.ea.com/connect/logout?client_id=ORIGIN_JS_SDK&access_token=";
        public const string USER_IDENTITY_LOOKUP = "https://gateway.ea.com/proxy/identity/pids/me";
        public const string ORIGIN_USER_SEARCH = "https://api2.origin.com/xsearch/users?userId=&searchTerm=&start=0";
        public const string ORIGIN_USER_ID_SEARCH = "https://api1.origin.com/atom/users?userIds=";
    }
}
