using System.Text;
using Microsoft.AspNetCore.Http.Extensions;

var builder = WebApplication.CreateBuilder(args);

// Helpful if you're behind Azure/AppService proxies
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedFor |
        Microsoft.AspNetCore.HttpOverrides.ForwardedHeaders.XForwardedProto;
});

var app = builder.Build();

app.UseForwardedHeaders();

// Basic home
app.MapGet("/", () => Results.Text("OK - Mastercard redirect receiver is running."));

// This is the URL you should use as redirectResponseUrl
app.MapMethods("/mastercard/redirect", new[] { "POST", "GET" }, async (HttpRequest req) =>
{
    var sb = new StringBuilder();

    sb.AppendLine($"Time (UTC): {DateTime.UtcNow:O}");
    sb.AppendLine($"Method: {req.Method}");
    sb.AppendLine($"Full URL: {req.GetDisplayUrl()}");
    sb.AppendLine($"Content-Type: {req.ContentType}");
    sb.AppendLine();

    // Log query string (GET redirects often use query params)
    if (req.Query.Any())
    {
        sb.AppendLine("Query parameters:");
        foreach (var kv in req.Query)
            sb.AppendLine($"  {kv.Key} = {kv.Value}");
        sb.AppendLine();
    }

    // Log form fields (common for POST redirects)
    if (req.HasFormContentType)
    {
        var form = await req.ReadFormAsync();
        sb.AppendLine("Form fields:");
        foreach (var kv in form)
            sb.AppendLine($"  {kv.Key} = {kv.Value}");
        sb.AppendLine();
    }
    else if (req.ContentLength > 0)
    {
        // If they POST JSON instead of form-encoded
        req.EnableBuffering();
        using var reader = new StreamReader(req.Body, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        req.Body.Position = 0;

        sb.AppendLine("Raw body:");
        sb.AppendLine(body);
        sb.AppendLine();
    }

    // Write to logs (Azure App Service -> Log stream)
    app.Logger.LogInformation("Mastercard redirect received:\n{Payload}", sb.ToString());

    // Return a simple HTML page
    var html = """
    <!DOCTYPE html>
    <html lang="en">
    <head>
      <meta charset="UTF-8">
      <meta name="viewport" content="width=device-width, initial-scale=1.0">
      <title>Purchase Successful</title>
      <style>
        * {
          margin: 0;
          padding: 0;
          box-sizing: border-box;
        }

        body {
          font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
          background-color: #0f172a;
          color: #e2e8f0;
          min-height: 100vh;
          display: flex;
          align-items: center;
          justify-content: center;
          padding: 20px;
          line-height: 1.6;
        }

        .container {
          text-align: center;
          max-width: 500px;
          width: 100%;
          padding: 40px 30px;
          background-color: #1e293b;
          border-radius: 12px;
          box-shadow: 0 4px 6px rgba(0, 0, 0, 0.3);
        }

        .icon {
          width: 64px;
          height: 64px;
          margin: 0 auto 24px;
          background-color: #0cbd07;
          border-radius: 50%;
          display: flex;
          align-items: center;
          justify-content: center;
        }

        .icon::after {
          content: '✓';
          color: #ffffff;
          font-size: 32px;
          font-weight: bold;
        }

        h1 {
          font-size: 24px;
          margin-bottom: 12px;
          color: #f1f5f9;
          font-weight: 600;
        }

        p {
          font-size: 16px;
          color: #cbd5e1;
          margin-bottom: 32px;
        }

        .close-button {
          background-color: #0cbd07;
          color: #ffffff;
          border: none;
          padding: 14px 32px;
          font-size: 16px;
          font-weight: 500;
          border-radius: 8px;
          cursor: pointer;
          transition: all 0.2s ease;
          width: 100%;
          max-width: 280px;
        }

        .close-button:hover {
          background-color: #0cbd07;
          transform: translateY(-1px);
          box-shadow: 0 4px 12px rgba(59, 130, 246, 0.4);
        }

        .close-button:active {
          transform: translateY(0);
        }

        @media (max-width: 480px) {
          .container {
            padding: 32px 24px;
          }

          h1 {
            font-size: 22px;
          }

          p {
            font-size: 15px;
          }
        }
      </style>
    </head>
    <body>
      <div class="container">
        <div class="icon"></div>
        <h1>Your purchase has been successful.</h1>
        <p>Click the X in the top right corner to close this page</p>        
      </div>     
    </body>
    </html>
    """;

    return Results.Content(html, "text/html");
});

app.Run();
