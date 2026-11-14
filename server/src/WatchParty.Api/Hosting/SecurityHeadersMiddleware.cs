namespace WatchParty.Api.Hosting;

/// <summary>Headers for a JSON API that is never framed and never renders HTML.</summary>
internal sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    public Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers.XContentTypeOptions = "nosniff";
            headers.XFrameOptions = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
            headers.ContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";
            return Task.CompletedTask;
        });
        return next(context);
    }
}
