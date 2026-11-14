namespace CampusGear.WebApp.Infrastructure
{
    public static class AuthResponses
    {
        /// <summary>A browser loading a page, as opposed to fetch/XHR calls and form posts.</summary>
        public static bool IsPageNavigation(HttpRequest request)
        {
            return (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method))
                && !request.Headers.ContainsKey("X-Requested-With")
                && request.Headers.Accept.ToString().Contains("text/html", StringComparison.OrdinalIgnoreCase);
        }
    }
}
