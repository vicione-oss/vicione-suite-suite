using System.Net.Mime;
using System.Text;

namespace Core.OS.Hosting;

internal static class HttpResultFactory
{
    public static IResult VersionDowngradeDetected(string currentVersion, string persistedVersion)
    {
        return new HtmlResult(@$"<!doctype html>
            <html lang=""en"">
            <head>
                <meta charset=""UTF-8"">
                <title>Startup Error</title>
                <style>
                    body {{
                        margin: 0;
                        padding: 0;
                        background-color: #2a2a2a;
                        color: #dedede;
                        font-family: 'Noto Sans', 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif;
                        display: flex;
                        flex-direction: column;
                        align-items: center;
                        justify-content: center;
                        min-height: 100vh;
                    }}

                    h1 {{
                        color: #ff9600;
                        font-size: 2rem;
                        margin-bottom: 1rem;
                    }}

                    p {{
                        max-width: 600px;
                        text-align: center;
                        line-height: 1.6;
                        margin: 0.5rem 0;
                    }}

                    a {{
                        display: inline-block;
                        margin: 0.5rem;
                        padding: 0.6rem 1.2rem;
                        text-decoration: none;
                        color: #dedede;
                        background-color: #333333;
                        border: 1px solid #555555;
                        border-radius: 6px;
                        transition: background-color 0.3s ease;
                    }}

                    a:hover {{
                        background-color: #555555;
                    }}
                </style>
            </head>
            <body>
                <h1>Version mismatch detected</h1>
                <p>Detected a software downgrade from version <strong>{persistedVersion}</strong> to <strong>{currentVersion}</strong>.<br> 
                The persisted data is not compatible with <strong>{currentVersion}</strong>.<br> 
                You can reset your data to factory settings if you want to continue working with version <strong>{currentVersion}</strong>.</p>
                <p><a href=""/reset"">Reset &amp; Restart</a></p>
                <p><a href=""/exit"">Cancel &amp; Exit</a></p>
            </body>
            </html>");
    }

    private class HtmlResult(string html) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = MediaTypeNames.Text.Html;
            httpContext.Response.ContentLength = Encoding.UTF8.GetByteCount(html);
            return httpContext.Response.WriteAsync(html);
        }
    }
}
